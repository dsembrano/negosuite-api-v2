using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class ReferenceDataTests
{
    private static async Task<JsonObject> Request(BillPaymentTests.Fixture f, string module, int id = 2000)
    {
        var input = new JsonObject { ["name"] = "Alpha %_", ["isActive"] = true, ["createdByUserId"] = int.MaxValue, ["createdDate"] = "2000-01-01" };
        switch (module)
        {
            case "payment-terms": input["id"] = id; input["code"] = "NET7"; input["days"] = 7; break;
            case "currencies": input["id"] = id; input["code"] = "USD"; input["altCode"] = "DOLLAR"; input["exchangeRate"] = 56.1234m; break;
            case "countries": input["code"] = "PH"; break;
            case "city-municipalities":
                var country = new Country { Name = "Country", Code = "PH" };
                var province = new StateProvince { Name = "Province", Country = country };
                f.Db.StateProvinces.Add(province); await f.Db.SaveChangesAsync();
                input["stateProvinceId"] = province.Id; input["postalCode"] = "6000"; break;
            case "NavigationItems": input["id"] = id; input["title"] = "Alpha %_"; input["type"] = "basic"; input["link"] = "/alpha"; break;
        }
        return input;
    }
    private static async Task<JsonObject> Create(HttpClient client, string module, JsonObject request)
    {
        var response = await client.PostAsJsonAsync("/api/" + module, request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.NotNull(response.Headers.Location);
        return await response.Content.ReadFromJsonAsync<JsonObject>();
    }

    [PagePreferenceTests.MySqlTheory]
    [InlineData("payment-modes")]
    [InlineData("payment-terms")]
    [InlineData("currencies")]
    [InlineData("countries")]
    [InlineData("city-municipalities")]
    [InlineData("industries")]
    [InlineData("NavigationItems")]
    public async Task Shared_catalogues_preserve_arrays_support_paging_sorts_and_use_explicit_write_contracts(string module)
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client; var route = "/api/" + module;
        var before = await client.GetFromJsonAsync<JsonArray>(route);
        var request = await Request(f, module); var first = await Create(client, module, request);
        var name = module == "NavigationItems" ? "title" : "name";
        request[name] = "Zulu"; if (module is "payment-terms" or "currencies" or "NavigationItems") request["id"] = 2001;
        await Create(client, module, request); var id = first["id"].GetValue<int>();
        var all = await client.GetFromJsonAsync<JsonArray>(route); Assert.Equal(before.Count + 2, all.Count);
        Assert.Single(await client.GetFromJsonAsync<JsonArray>(route + "?search=" + Uri.EscapeDataString("%_")));
        var page = await client.GetFromJsonAsync<JsonObject>(route + $"?pageNumber=1&pageSize=1&sortBy={name}&sortDirection=desc");
        Assert.Equal(before.Count + 2, page["totalCount"].GetValue<int>()); Assert.Equal("Zulu", page["items"][0][name].GetValue<string>());
        Assert.Empty((await client.GetFromJsonAsync<JsonObject>(route + "?pageNumber=99&pageSize=200"))["items"].AsArray());
        foreach (var invalid in new[] { "pageNumber=1", "pageSize=1", "pageNumber=0&pageSize=10", "pageNumber=1&pageSize=201", "pageNumber=2147483647&pageSize=200", "sortBy=bad", "sortDirection=bad" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route + "?" + invalid)).StatusCode);
        IEnumerable<string> sorts = module switch
        {
            "payment-modes" => new PaymentModeService(f.Db).SortFields.Keys,
            "payment-terms" => new PaymentTermService(f.Db).SortFields.Keys,
            "currencies" => new CurrencyService(f.Db).SortFields.Keys,
            "countries" => new CountryService(f.Db).SortFields.Keys,
            "city-municipalities" => new CityMunicipalityService(f.Db).SortFields.Keys,
            "industries" => new IndustryService(f.Db).SortFields.Keys,
            _ => new NavigationItemService(f.Db).SortFields.Keys
        };
        foreach (var sort in sorts) foreach (var direction in new[] { "asc", "desc" })
        {
            var sorted = await client.GetAsync(route + $"?sortBy={sort}&sortDirection={direction}&pageNumber=1&pageSize=1");
            Assert.True(sorted.StatusCode == HttpStatusCode.OK, await sorted.Content.ReadAsStringAsync());
        }
        if (module == "industries") Assert.Equal(2, all[0].AsObject().Count);
        if (module == "city-municipalities") Assert.Equal(6, all[0].AsObject().Count);
        if (module == "countries") Assert.Empty(first["stateProvinces"].AsArray());
        if (module == "NavigationItems") { Assert.Equal(7, all[0].AsObject().Count); Assert.Equal(JsonValueKind.String, all[0]["id"].GetValueKind()); }
        if (first.ContainsKey("createdDate"))
        {
            Assert.Equal((await f.Db.Users.SingleAsync()).Id, first["createdByUserId"].GetValue<int>());
            Assert.NotEqual(2000, first["createdDate"].GetValue<DateTime>().Year);
        }
        var createdDate = first["createdDate"]?.DeepClone(); first[name] = "Updated"; first["createdDate"] = "2000-01-01";
        if (module == "city-municipalities") first["stateProvince"]["name"] = "Overposted";
        if (module == "countries") first["stateProvinces"] = new JsonArray(new JsonObject { ["name"] = "Overposted" });
        if (module == "NavigationItems") first["children"] = new JsonArray(new JsonObject { ["id"] = 3000, ["title"] = "Overposted" });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(route + "/" + id, first)).StatusCode);
        var updated = await client.GetFromJsonAsync<JsonObject>(route + "/" + id); Assert.Equal("Updated", updated[name].GetValue<string>());
        if (createdDate != null) Assert.True(JsonNode.DeepEquals(createdDate, updated["createdDate"]));
        if (module == "city-municipalities") Assert.Equal("Province", updated["stateProvince"]["name"].GetValue<string>());
        Assert.False(await f.Db.StateProvinces.AnyAsync(p => p.Name == "Overposted")); Assert.False(await f.Db.NavigationItems.AnyAsync(p => p.Title == "Overposted"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(route + "/32000", first)).StatusCode);
        first["id"] = 32000; Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(route + "/32000", first)).StatusCode);
        request[name] = " "; Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, request)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route + "/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route + "/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(route + "/" + id)).StatusCode);
    }

    [MySqlFact]
    public async Task Status_defaults_base_currency_and_shared_reads_work_during_onboarding()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        var mode = await Request(f, "payment-modes"); mode["isActive"] = false; await Create(client, "payment-modes", mode);
        Assert.Single(await client.GetFromJsonAsync<JsonArray>("/api/payment-modes"));
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonArray>("/api/payment-modes?showInactive=true")).Count);
        Assert.Single(await client.GetFromJsonAsync<JsonArray>("/api/payment-modes?status=false"));
        var term = await Request(f, "payment-terms"); term["isActive"] = false; await Create(client, "payment-terms", term);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonArray>("/api/payment-terms")).Count);
        Assert.Single(await client.GetFromJsonAsync<JsonArray>("/api/payment-terms?status=false"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/currencies/base")).StatusCode);
        var currency = await Request(f, "currencies"); currency["isBase"] = true; await Create(client, "currencies", currency);
        var baseCurrency = await client.GetFromJsonAsync<JsonObject>("/api/currencies/base");
        Assert.Equal(2000, baseCurrency["id"].GetValue<int>()); Assert.Equal(56.1234m, baseCurrency["exchangeRate"].GetValue<decimal>());
        var actor = await f.Db.Users.SingleAsync(); actor.ConfigId = f.Other.Id; await f.Db.SaveChangesAsync();
        client.DefaultRequestHeaders.Remove("configUuid"); client.DefaultRequestHeaders.Add("configUuid", f.Other.Uuid);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonArray>("/api/payment-terms")).Count);
        Assert.Equal(2, (await client.GetFromJsonAsync<JsonArray>("/api/payment-modes?showInactive=true")).Count);
        actor.ConfigId = null; await f.Db.SaveChangesAsync(); client.DefaultRequestHeaders.Remove("configUuid");
        foreach (var module in new[] { "payment-terms", "currencies", "countries", "industries", "NavigationItems" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/" + module)).StatusCode);
        foreach (var module in new[] { "payment-modes", "city-municipalities" }) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/" + module)).StatusCode);
        actor.Status = false; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/countries")).StatusCode);
    }

    [MySqlFact]
    public async Task City_filters_and_reference_guards_do_not_depend_on_missing_legacy_view()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        var request = await Request(f, "city-municipalities"); var city = await Create(client, "city-municipalities", request);
        var province = await f.Db.StateProvinces.SingleAsync(); var country = await f.Db.Countries.SingleAsync();
        var other = new Country { Name = "Other", Code = "XX" }; f.Db.Countries.Add(other); await f.Db.SaveChangesAsync();
        var cityList = await client.GetFromJsonAsync<JsonArray>($"/api/city-municipalities?countryId={country.Id}&stateProvinceId={province.Id}");
        Assert.Single(cityList); Assert.Equal("Alpha %_, Province", cityList[0]["selectOptionName"].GetValue<string>());
        Assert.Empty(await client.GetFromJsonAsync<JsonArray>($"/api/city-municipalities?countryId={other.Id}"));
        Assert.Single(await client.GetFromJsonAsync<JsonArray>("/api/city-municipalities?search=Province"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/city-municipalities?stateProvinceId=0")).StatusCode);
        request["stateProvinceId"] = int.MaxValue;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/city-municipalities", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/countries/{country.Id}")).StatusCode);
        var industry = await Create(client, "industries", await Request(f, "industries"));
        f.Company.IndustryId = industry["id"].GetValue<int>(); f.Other.CountryId = other.Id;
        var customer = new Customer { UserConfigId = f.Company.Id, Name = "Address owner", Status = true };
        f.Db.Customers.Add(customer); await f.Db.SaveChangesAsync();
        f.Db.CustomerAddresses.Add(new CustomerAddress { CustomerId = customer.Id, CityMunicipalityId = city["id"].GetValue<int>() });
        f.Supplier.PaymentTermId = f.Term.Id;
        f.Db.Payments.Add(new Payment { UserConfigId = f.Company.Id, ReferenceNo = "LOOKUP", PaymentModeId = f.Mode.Id });
        await f.Db.SaveChangesAsync();
        foreach (var (module, id) in new[] { ("countries", other.Id), ("industries", industry["id"].GetValue<int>()), ("city-municipalities", city["id"].GetValue<int>()), ("payment-terms", f.Term.Id), ("payment-modes", f.Mode.Id) })
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/{module}/{id}")).StatusCode);
    }

    [MySqlFact]
    public async Task Navigation_pages_roots_without_truncating_children_and_prevents_cycles()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        var root = await Create(client, "NavigationItems", await Request(f, "NavigationItems", 1000));
        var next = await Request(f, "NavigationItems", 1001); next["parentId"] = 1000; next["title"] = "Child needle";
        await Create(client, "NavigationItems", next); next["id"] = 1002; next["title"] = "Sibling"; await Create(client, "NavigationItems", next);
        next["id"] = 1003; next["parentId"] = 1001; next["title"] = "Grandchild"; await Create(client, "NavigationItems", next);
        await Create(client, "NavigationItems", await Request(f, "NavigationItems", 2000));
        var page = await client.GetFromJsonAsync<JsonObject>("/api/NavigationItems?pageNumber=1&pageSize=1&sortBy=id");
        Assert.Equal(2, page["totalCount"].GetValue<int>()); var children = page["items"][0]["children"].AsArray(); Assert.Equal(2, children.Count);
        Assert.Equal("1001", children[0]["id"].GetValue<string>()); Assert.Equal(6, children[0].AsObject().Count);
        var searched = await client.GetFromJsonAsync<JsonArray>("/api/NavigationItems?search=needle"); Assert.Single(searched); Assert.Equal(2, searched[0]["children"].AsArray().Count);
        foreach (var parent in new[] { 1000, 1001, 1003, 99999 })
        {
            root["parentId"] = parent;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/NavigationItems/1000", root)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync("/api/NavigationItems/1000")).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<JsonObject>("/api/NavigationItems/1000"))["parentId"]);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/NavigationItems/1003")).StatusCode);
    }

    [MySqlFact]
    public async Task Schema_lengths_manual_ids_and_numeric_ranges_are_checked_before_save()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        foreach (var module in new[] { "payment-terms", "currencies", "NavigationItems" })
        {
            var request = await Request(f, module); request["id"] = 0;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/" + module, request)).StatusCode);
        }
        foreach (var (module, field, value) in new[] { ("payment-terms", "code", new string('X', 11)), ("countries", "code", new string('X', 11)), ("currencies", "altCode", new string('X', 16)), ("NavigationItems", "link", new string('X', 101)), ("city-municipalities", "postalCode", new string('X', 21)) })
        {
            var request = await Request(f, module); request[field] = value;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/" + module, request)).StatusCode);
        }
        var term = await Request(f, "payment-terms"); term["days"] = -1;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/payment-terms", term)).StatusCode);
        foreach (var rate in new[] { 0m, -1m, 0.00001m, 1000000m })
        {
            var currency = await Request(f, "currencies"); currency["exchangeRate"] = rate;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/currencies", currency)).StatusCode);
        }
    }
}
