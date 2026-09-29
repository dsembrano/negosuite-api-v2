using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class CustomerTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 50, true)]
    [InlineData(1, 200, true)]
    [InlineData(1, null, false)]
    [InlineData(null, 50, false)]
    [InlineData(0, 50, false)]
    [InlineData(-1, 50, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 201, false)]
    [InlineData(int.MaxValue, 200, false)]
    public void Pagination_is_explicit_and_bounded(int? page, int? size, bool valid) =>
        Assert.Equal(valid, CustomerPagination.IsValid(page, size));

    [MySqlFact]
    public async Task Customer_http_contract_paging_writes_and_ownership_are_preserved()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server);
        Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_customers_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var company = new Config { CompanyName = "Customers fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other fixture", Uuid = Guid.NewGuid().ToString() };
            var country = new Country { Name = "Test country", Code = "TC" };
            var province = new StateProvince { Name = "Test province", Country = country };
            var city = new CityMunicipality { Name = "Test city", StateProvince = province, PostalCode = "1000" };
            db.Configs.AddRange(company, other);
            db.CityMunicipalities.Add(city);
            await db.SaveChangesAsync();
            var first = new Customer { UserConfigId = company.Id, Name = "Same", Tin = "ID-&%_?", Status = true, CreditLimit = 1234.5678m,
                CreatedDate = new DateTime(2020, 1, 1), CreatedByUserId = 42 };
            first.CustomerContacts.Add(new CustomerContact { Name = "Primary", IsPrimary = true, PhoneNo = "0917-123", Email = "primary@example.test" });
            first.CustomerContacts.Add(new CustomerContact { Name = "Delete me", PhoneNo = "0917-123" });
            first.CustomerAddresses.Add(new CustomerAddress { AddressLine1 = "Street", CityMunicipalityId = city.Id, IsBillingAddress = true });
            var second = new Customer { UserConfigId = company.Id, Name = "Same", Status = true };
            var inactive = new Customer { UserConfigId = company.Id, Name = "Inactive", Status = false };
            var outsider = new Customer { UserConfigId = other.Id, Name = "Other", Status = true };
            outsider.CustomerContacts.Add(new CustomerContact { Name = "Foreign contact" });
            outsider.CustomerAddresses.Add(new CustomerAddress { CityMunicipalityId = city.Id, AddressLine1 = "Foreign" });
            second.CustomerContacts.Add(new CustomerContact { Name = "Second", PhoneNo = "0917-123" });
            inactive.CustomerContacts.Add(new CustomerContact { Name = "Hidden", PhoneNo = "0917-123" });
            outsider.CustomerContacts.First().PhoneNo = "0917-123";
            db.Customers.AddRange(first, second, inactive, outsider);
            await db.SaveChangesAsync();
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
            host.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
            string ListUrl(int tenant, bool showInactive = false) => "/api/customers?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(new { userConfigId = tenant, showInactive }));
            var url = ListUrl(company.Id);
            var legacy = await host.Client.GetFromJsonAsync<JsonElement>(url);
            Assert.Equal(JsonValueKind.Array, legacy.ValueKind);
            Assert.Equal(new[] { first.Id, second.Id }, legacy.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
            Assert.Equal(1234.5678m, legacy[0].GetProperty("creditLimit").GetDecimal());
            Assert.Equal("Test province", legacy[0].GetProperty("customerAddresses")[0].GetProperty("cityMunicipality").GetProperty("stateProvince").GetProperty("name").GetString());
            Assert.Equal(2, legacy[0].GetProperty("customerContacts").GetArrayLength());
            Assert.Equal(3, (await host.Client.GetFromJsonAsync<JsonElement>(ListUrl(company.Id, true))).GetArrayLength());
            var page = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=1");
            Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
            Assert.Equal(second.Id, page.GetProperty("items")[0].GetProperty("id").GetInt32());
            var searched = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=" + Uri.EscapeDataString("ID-&%_?"));
            Assert.Equal(1, searched.GetProperty("totalCount").GetInt32());
            Assert.Equal(first.Id, searched.GetProperty("items")[0].GetProperty("id").GetInt32());
            // Related matches search every child and preserve one result per customer before paging.
            foreach (var term in new[] { "Primary", "primary@example.test", "Street", "Test city", "Test province", "1000" })
            {
                var related = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=" + Uri.EscapeDataString(term));
                Assert.Equal(1, related.GetProperty("totalCount").GetInt32());
                Assert.Equal(first.Id, related.GetProperty("items")[0].GetProperty("id").GetInt32());
            }
            var phonePage = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=1&search=0917-123");
            Assert.Equal(2, phonePage.GetProperty("totalCount").GetInt32());
            Assert.Equal(second.Id, phonePage.GetProperty("items")[0].GetProperty("id").GetInt32());
            var phoneExport = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=0917-123");
            Assert.Equal(new[] { first.Id, second.Id }, phoneExport.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
            var allPhones = await host.Client.GetFromJsonAsync<JsonElement>(ListUrl(company.Id, true) + "&pageNumber=1&pageSize=25&search=0917-123");
            Assert.Equal(3, allPhones.GetProperty("totalCount").GetInt32());
            var foreignContact = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=Foreign");
            Assert.Equal(0, foreignContact.GetProperty("totalCount").GetInt32());
            var hiddenInactive = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=Inactive");
            Assert.Equal(0, hiddenInactive.GetProperty("totalCount").GetInt32());
            var visibleInactive = await host.Client.GetFromJsonAsync<JsonElement>(ListUrl(company.Id, true) + "&pageNumber=1&pageSize=1&search=Inactive");
            Assert.Equal(1, visibleInactive.GetProperty("totalCount").GetInt32());
            var foreignSearch = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=Other");
            Assert.Equal(0, foreignSearch.GetProperty("totalCount").GetInt32());
            var empty = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=1");
            Assert.Equal(0, empty.GetProperty("items").GetArrayLength());
            foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=abc&pageSize=1" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url + suffix)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/customers?criteria=%7Bbroken")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/customers")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(ListUrl(other.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/customers/{outsider.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"/api/customers/{outsider.Id}")).StatusCode);
            var foreignUpdate = new CustomerWriteRequest { Id = outsider.Id, UserConfigId = company.Id, Name = "Rejected", Status = true };
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PutAsJsonAsync($"/api/customers/{outsider.Id}", foreignUpdate)).StatusCode);
            foreignUpdate.Id = first.Id;
            foreignUpdate.UserConfigId = other.Id;
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", foreignUpdate)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/api/customers", new { UserConfigId = other.Id, Name = "Rejected" })).StatusCode);

            // Reject foreign child IDs before even a parent-name update reaches the database.
            var request = new CustomerWriteRequest { Id = first.Id, UserConfigId = company.Id, Name = "Must not save", Status = true };
            request.CustomerContacts.Add(new CustomerContactRequest { Id = outsider.CustomerContacts.Single().Id, Deleted = true });
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", request)).StatusCode);

            request.CustomerAddresses.Clear();
            request.CustomerContacts.Clear();
            request.CustomerContacts.Add(new CustomerContactRequest { Id = first.CustomerContacts.First().Id, Name = "Duplicate" });
            request.CustomerContacts.Add(new CustomerContactRequest { Id = first.CustomerContacts.First().Id, Deleted = true });
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", request)).StatusCode);
            request.CustomerContacts.Clear();
            request.PaymentTermId = int.MaxValue;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", request)).StatusCode);
            request.PaymentTermId = null;
            Assert.Equal("Same", await db.Customers.AsNoTracking().Where(c => c.Id == first.Id).Select(c => c.Name).SingleAsync());
            request.CustomerContacts.Clear();
            request.CustomerAddresses.Add(new CustomerAddressRequest { Id = outsider.CustomerAddresses.Single().Id, Deleted = true });
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", request)).StatusCode);

            // Angular must omit new child IDs: explicit null cannot bind to an integer ID.
            var nullContactId = await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", new {
                id = first.Id, userConfigId = company.Id, name = "Must not save", status = true,
                customerContacts = new[] { new { id = (int?)null, customerId = first.Id, name = "New contact", email = "new@example.test", phoneNo = "0123", isPrimary = false, deleted = false } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, nullContactId.StatusCode);
            var validation = await nullContactId.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(validation.GetProperty("errors").EnumerateObject(), error => error.Name.Contains("id", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("Same", await db.Customers.AsNoTracking().Where(c => c.Id == first.Id).Select(c => c.Name).SingleAsync());

            // Round-trip a full legacy detail payload with nested lookups and audit fields.
            var detail = await host.Client.GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>($"/api/customers/{first.Id}");
            Assert.Equal(JsonValueKind.Null, JsonSerializer.SerializeToElement(detail).GetProperty("taxRate").ValueKind);
            detail["name"] = "Updated";
            detail["createdDate"] = "1990-01-01T00:00:00";
            detail["createdByUserId"] = 999;
            detail["customerContacts"][0]["name"] = "Changed contact";
            detail["customerContacts"][1]["deleted"] = true;
            detail["customerAddresses"][0]["deleted"] = true;
            detail["customerContacts"].AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["customerId"] = first.Id, ["name"] = "New contact", ["email"] = "new@example.test", ["phoneNo"] = "0123", ["isPrimary"] = false, ["deleted"] = false });
            var updatedResponse = await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", detail);
            Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
            var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Updated", updated.GetProperty("name").GetString());
            Assert.Equal(2020, updated.GetProperty("createdDate").GetDateTime().Year);
            Assert.Equal(42, updated.GetProperty("createdByUserId").GetInt32());
            Assert.Equal(2, updated.GetProperty("customerContacts").GetArrayLength());
            var addedContact = updated.GetProperty("customerContacts").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "New contact");
            Assert.True(addedContact.GetProperty("id").GetInt32() > 0);
            Assert.Equal(first.Id, addedContact.GetProperty("customerId").GetInt32());
            Assert.Equal("0123", addedContact.GetProperty("phoneNo").GetString());
            Assert.Equal(0, updated.GetProperty("customerAddresses").GetArrayLength());
            var nullCollections = new CustomerWriteRequest { Id = first.Id, UserConfigId = company.Id, Name = "Updated", Status = true, CustomerContacts = null, CustomerAddresses = null };
            var preserved = await host.Client.PutAsJsonAsync($"/api/customers/{first.Id}", nullCollections);
            Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
            Assert.Equal(2, (await preserved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("customerContacts").GetArrayLength());

            var create = new CustomerWriteRequest { UserConfigId = company.Id, Name = "Created", Status = true,
                CustomerContacts = new() { new() { Name = "Created contact" } },
                CustomerAddresses = new() { new() { CityMunicipalityId = city.Id, AddressLine1 = "New address" } } };
            var createdResponse = await host.Client.PostAsJsonAsync("/api/customers", create);
            Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
            var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
            var createdId = created.GetProperty("id").GetInt32();
            Assert.Equal(createdId, created.GetProperty("customerContacts")[0].GetProperty("customerId").GetInt32());
            Assert.Equal(createdId, created.GetProperty("customerAddresses")[0].GetProperty("customerId").GetInt32());
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"/api/customers/{createdId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/customers/{createdId}")).StatusCode);
            Assert.False(await db.CustomerContacts.AsNoTracking().AnyAsync(c => c.CustomerId == createdId));

            // A restrictive FK simulates a transaction referencing this customer: deletion must roll back child removals.
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE customer_guard (id INT PRIMARY KEY, customer_id INT NOT NULL, FOREIGN KEY (customer_id) REFERENCES customer(Id))");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO customer_guard (id, customer_id) VALUES (1, {first.Id})");
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.DeleteAsync($"/api/customers/{first.Id}")).StatusCode);
            Assert.True(await db.Customers.AsNoTracking().AnyAsync(c => c.Id == first.Id));
            Assert.Equal(2, await db.CustomerContacts.AsNoTracking().CountAsync(c => c.CustomerId == first.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
