using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class MaintenanceTests
{
    private static string List(string module, int company, bool showInactive = false, int? type = null) => "/api/" + module + "?criteria=" +
        Uri.EscapeDataString(JsonSerializer.Serialize(new { userConfigId = company, showInactive, responsibilityCenterTypeId = type }));

    private static async Task<JsonObject> Request(BillPaymentTests.Fixture f, string module, string name = "Alpha %_")
    {
        var input = new JsonObject { ["userConfigId"] = f.Company.Id, ["name"] = name };
        switch (module)
        {
            case "tax-rates": input["rate"] = 12.3456m; input["taxAccountId"] = f.Account.Id; input["salesAccountId"] = f.Account.Id; break;
            case "discount-types": input["rate"] = 5.4321m; input["discountAmount"] = 1.2345m; input["discountAccountId"] = f.Account.Id; break;
            case "responsibility-center-types": input["isActive"] = true; input["requiredBy"] = "SPECIFIC"; input["requiredByTags"] = $"[{f.Account.Id}]"; break;
            case "responsibility-centers":
                var type = new ResponsibilityCenterType { UserConfigId = f.Company.Id, Name = "Departments", RequiredBy = "SPECIFIC", IsActive = true };
                f.Db.ResponsibilityCenterTypes.Add(type); await f.Db.SaveChangesAsync();
                input["responsibilityCenterTypeId"] = type.Id; input["status"] = true; break;
            case "inventory-locations": input["code"] = "LOC-1"; input["status"] = true; break;
        }
        return input;
    }
    private static async Task<JsonObject> Create(HttpClient client, string module, JsonObject input)
    {
        var response = await client.PostAsJsonAsync("/api/" + module, input);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Assert.NotNull(response.Headers.Location);
        return await response.Content.ReadFromJsonAsync<JsonObject>();
    }

    [PagePreferenceTests.MySqlTheory]
    [InlineData("tax-rates")]
    [InlineData("discount-types")]
    [InlineData("responsibility-centers")]
    [InlineData("responsibility-center-types")]
    [InlineData("inventory-locations")]
    public async Task Legacy_lists_optional_paging_sorting_crud_and_company_scope(string module)
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        var request = await Request(f, module); request["createdByUserId"] = int.MaxValue; request["createdDate"] = "2000-01-01";
        var first = await Create(client, module, request); request["name"] = "Zulu";
        var second = await Create(client, module, request); var id = first["id"].GetValue<int>(); var route = $"/api/{module}/{id}";
        Assert.Equal((await f.Db.Users.SingleAsync()).Id, first["createdByUserId"].GetValue<int>());
        Assert.NotEqual(2000, first["createdDate"].GetValue<DateTime>().Year);
        var listUrl = List(module, f.Company.Id);
        var list = await client.GetFromJsonAsync<JsonArray>(listUrl); Assert.Equal(2, list.Count);
        if (module == "inventory-locations") Assert.Equal(4, list[0].AsObject().Count);
        var page = await client.GetFromJsonAsync<JsonObject>(listUrl + "&pageNumber=1&pageSize=1&sortBy=name&sortDirection=desc");
        Assert.Equal(2, page["totalCount"].GetValue<int>()); Assert.Equal("Zulu", page["items"][0]["name"].GetValue<string>());
        Assert.Single((await client.GetFromJsonAsync<JsonArray>(listUrl + "&search=" + Uri.EscapeDataString("%_"))));
        Assert.Empty((await client.GetFromJsonAsync<JsonObject>(listUrl + "&pageNumber=99&pageSize=10"))["items"].AsArray());
        foreach (var invalid in new[] { "pageNumber=1", "pageSize=10", "pageNumber=0&pageSize=10", "pageNumber=1&pageSize=201", "sortBy=Name;DROP", "sortDirection=sideways" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(listUrl + "&" + invalid)).StatusCode);
        IEnumerable<string> sorts = module switch
        {
            "tax-rates" => new TaxRateService(f.Db).SortFields.Keys,
            "discount-types" => new DiscountTypeService(f.Db).SortFields.Keys,
            "responsibility-centers" => new ResponsibilityCenterService(f.Db).SortFields.Keys,
            "responsibility-center-types" => new ResponsibilityCenterTypeService(f.Db).SortFields.Keys,
            _ => new InventoryLocationService(f.Db).SortFields.Keys
        };
        foreach (var sort in sorts) foreach (var direction in new[] { "asc", "desc" })
        {
            var response = await client.GetAsync(listUrl + $"&sortBy={sort}&sortDirection={direction}&pageNumber=1&pageSize=1");
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(List(module, f.Other.Id))).StatusCode);
        // Turn the second row into a foreign-company record to exercise scoped IDs.
        var model = module switch { "tax-rates" => typeof(TaxRate), "discount-types" => typeof(DiscountType), "responsibility-centers" => typeof(ResponsibilityCenter), "responsibility-center-types" => typeof(ResponsibilityCenterType), _ => typeof(InventoryLocation) };
        // Only the scalar company property is modified; nested DTOs are not attached.
        var foreignId = second["id"].GetValue<int>();
        var tracked = await f.Db.FindAsync(model, foreignId);
        f.Db.Entry(tracked).Property("UserConfigId").CurrentValue = f.Other.Id; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/{module}/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/{module}/{foreignId}", second)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/{module}/{foreignId}")).StatusCode);
        request["userConfigId"] = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/" + module, request)).StatusCode);
        request["userConfigId"] = f.Company.Id; request["name"] = " ";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/" + module, request)).StatusCode);
        var originalCreated = first["createdDate"].DeepClone(); first["name"] = "Updated"; first["createdDate"] = "2000-01-01";
        foreach (var nav in new[] { "taxAccount", "salesAccount", "discountAccount" }) if (first[nav] != null) first[nav]["name"] = "Overposted";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(route, first)).StatusCode);
        var updated = await client.GetFromJsonAsync<JsonObject>(route);
        Assert.Equal("Updated", updated["name"].GetValue<string>()); Assert.True(JsonNode.DeepEquals(originalCreated, updated["createdDate"]));
        Assert.Equal("Receivables", await f.Db.Accounts.AsNoTracking().Where(a => a.Id == f.Account.Id).Select(a => a.Name).SingleAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(route + "0", first)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(route)).StatusCode);
    }

    [PagePreferenceTests.MySqlTheory]
    [InlineData("tax-rates")]
    [InlineData("discount-types")]
    [InlineData("responsibility-center-types")]
    public async Task Bulk_calls_are_atomic_and_keep_legacy_array_response(string module)
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client; var route = $"/api/{module}/many";
        var first = await Create(client, module, await Request(f, module)); var second = await Create(client, module, await Request(f, module, "Second"));
        first["name"] = "Updated"; var bad = second.DeepClone().AsObject(); bad["userConfigId"] = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(route, new[] { first, bad })).StatusCode);
        bad["userConfigId"] = f.Company.Id; bad["id"] = int.MaxValue;
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(route, new[] { first, bad })).StatusCode);
        bad["id"] = second["id"].DeepClone(); bad["name"] = " ";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new[] { first, bad })).StatusCode);
        Assert.Equal("Alpha %_", (await client.GetFromJsonAsync<JsonObject>($"/api/{module}/{first["id"]}"))["name"].GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new[] { first, first })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route, new JsonObject[] { first, null })).StatusCode);
        var empty = await client.PostAsJsonAsync(route, Array.Empty<JsonObject>()); Assert.Equal(2, (await empty.Content.ReadFromJsonAsync<JsonArray>()).Count);
        second = new JsonObject { ["id"] = second["id"].DeepClone(), ["userConfigId"] = f.Company.Id, ["deleted"] = true };
        var created = await Request(f, module, "Third");
        var saved = await client.PostAsJsonAsync(route, new[] { first, second, created });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var rows = await saved.Content.ReadFromJsonAsync<JsonArray>(); Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r["name"].GetValue<string>() == "Updated"); Assert.Contains(rows, r => r["name"].GetValue<string>() == "Third");
    }

    [MySqlFact]
    public async Task Status_defaults_and_type_lists_remain_compatible()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        foreach (var module in new[] { "responsibility-centers", "responsibility-center-types", "inventory-locations" })
        {
            var request = await Request(f, module); await Create(client, module, request);
            request[module == "responsibility-center-types" ? "isActive" : "status"] = false; request["name"] = "Inactive";
            await Create(client, module, request);
            Assert.Equal(module == "inventory-locations" ? 1 : 2, (await client.GetFromJsonAsync<JsonArray>(List(module, f.Company.Id))).Count);
            Assert.Equal(2, (await client.GetFromJsonAsync<JsonArray>(List(module, f.Company.Id, true))).Count);
            Assert.Single(await client.GetFromJsonAsync<JsonArray>(List(module, f.Company.Id) + "&status=false"));
            if (module == "responsibility-centers")
            {
                var type = request["responsibilityCenterTypeId"].GetValue<int>();
                Assert.Single(await client.GetFromJsonAsync<JsonArray>(List(module + "/type", f.Company.Id, type: type)));
                var paged = await client.GetFromJsonAsync<JsonObject>(List(module + "/type", f.Company.Id, true, type) + "&pageNumber=1&pageSize=1");
                Assert.Equal(2, paged["totalCount"].GetValue<int>());
                // Remove the supporting type so the following iteration starts with no types.
                f.Db.ResponsibilityCenters.RemoveRange(await f.Db.ResponsibilityCenters.ToListAsync()); await f.Db.SaveChangesAsync();
                f.Db.ResponsibilityCenterTypes.RemoveRange(await f.Db.ResponsibilityCenterTypes.ToListAsync()); await f.Db.SaveChangesAsync();
            }
        }
    }

    [MySqlFact]
    public async Task Related_ids_json_tags_and_referenced_deletes_are_validated()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        foreach (var (module, property) in new[] { ("tax-rates", "taxAccountId"), ("tax-rates", "salesAccountId"), ("discount-types", "discountAccountId") })
        {
            var request = await Request(f, module); request[property] = f.ForeignAccount.Id;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/" + module, request)).StatusCode);
        }
        var typeRequest = await Request(f, "responsibility-center-types");
        foreach (var tags in new[] { "{bad", "{}", "[0]", "[1.5]", "[999999999999999999999999999999999999]", $"[{f.ForeignAccount.Id}]" })
        {
            typeRequest["requiredByTags"] = tags;
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/responsibility-center-types", typeRequest)).StatusCode);
        }
        var tax = await Create(client, "tax-rates", await Request(f, "tax-rates"));
        Assert.Equal(12.3456m, tax["rate"].GetValue<decimal>());
        var discountRequest = await Request(f, "discount-types"); discountRequest["taxRateId"] = tax["id"].DeepClone();
        var discount = await Create(client, "discount-types", discountRequest);
        Assert.Equal(1.2345m, discount["discountAmount"].GetValue<decimal>());
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/tax-rates/{tax["id"]}")).StatusCode);
        var replacement = await Create(client, "tax-rates", await Request(f, "tax-rates", "Unchanged")); replacement["name"] = "Should rollback"; tax["deleted"] = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/tax-rates/many", new[] { replacement, tax })).StatusCode);
        Assert.Equal("Unchanged", (await client.GetFromJsonAsync<JsonObject>($"/api/tax-rates/{replacement["id"]}"))["name"].GetValue<string>());
        var center = await Create(client, "responsibility-centers", await Request(f, "responsibility-centers"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/responsibility-center-types/{center["responsibilityCenterTypeId"]}")).StatusCode);
        var location = await Create(client, "inventory-locations", await Request(f, "inventory-locations"));
        f.Db.GeneralJournals.Add(new GeneralJournal { UserConfigId = f.Company.Id, ReferenceNo = "DRAFT", ResponsibilityCenterEntry = $"[{{\"id\":{center["id"]}}}]" });
        f.Db.StockTransfers.Add(new StockTransfer { UserConfigId = f.Company.Id, ReferenceNo = "DRAFT", FromInventoryLocationId = location["id"].GetValue<int>(), ToInventoryLocationId = location["id"].GetValue<int>() });
        await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/responsibility-centers/{center["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/inventory-locations/{location["id"]}")).StatusCode);
    }

    [MySqlFact]
    public async Task Onboarding_can_read_template_tax_rates_but_cannot_write_or_read_other_companies()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var client = f.Host.Client;
        var template = new Config { CompanyName = "Template", Uuid = Guid.NewGuid().ToString(), IsTemplate = true };
        f.Db.Configs.Add(template); await f.Db.SaveChangesAsync();
        f.Db.TaxRates.Add(new TaxRate { UserConfigId = template.Id, Name = "Template VAT", Rate = 12 });
        var actor = await f.Db.Users.SingleAsync(); actor.ConfigId = null; await f.Db.SaveChangesAsync();
        client.DefaultRequestHeaders.Remove("configUuid");
        Assert.Single(await client.GetFromJsonAsync<JsonArray>(List("tax-rates", template.Id)));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(List("tax-rates", f.Other.Id))).StatusCode);
        client.DefaultRequestHeaders.Add("configUuid", template.Uuid);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/tax-rates", new { userConfigId = template.Id, name = "Forged" })).StatusCode);
    }
}
