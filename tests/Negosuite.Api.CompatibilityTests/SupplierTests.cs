using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SupplierTests
{
    [MySqlFact]
    public async Task Supplier_http_contract_paging_writes_and_ownership_are_preserved()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server);
        Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_suppliers_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var company = new Config { CompanyName = "Suppliers fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other fixture", Uuid = Guid.NewGuid().ToString() };
            var country = new Country { Name = "Test country", Code = "TC" };
            var province = new StateProvince { Name = "Test province", Country = country };
            var city = new CityMunicipality { Name = "Test city", StateProvince = province, PostalCode = "1000" };
            db.Configs.AddRange(company, other);
            db.CityMunicipalities.Add(city);
            await db.SaveChangesAsync();
            var first = new Supplier { UserConfigId = company.Id, Name = "Same", Tin = "ID-&%_?", Status = true,
                CreatedDate = new DateTime(2020, 1, 1), CreatedByUserId = 42 };
            first.SupplierContacts.Add(new SupplierContact { Name = "Primary", IsPrimary = true, PhoneNo = "0917-123", Email = "accounts@example.test" });
            first.SupplierContacts.Add(new SupplierContact { Name = "Delete me", PhoneNo = "0917-123" });
            first.SupplierAddresses.Add(new SupplierAddress { AddressLine1 = "Street", CityMunicipalityId = city.Id, IsPrimaryAddress = true });
            var second = new Supplier { UserConfigId = company.Id, Name = "Same", Status = true };
            var inactive = new Supplier { UserConfigId = company.Id, Name = "Inactive", Status = false };
            inactive.SupplierContacts.Add(new SupplierContact { PhoneNo = "0917-123" });
            var outsider = new Supplier { UserConfigId = other.Id, Name = "Other", Status = true };
            outsider.SupplierContacts.Add(new SupplierContact { Name = "Foreign contact" });
            outsider.SupplierAddresses.Add(new SupplierAddress { CityMunicipalityId = city.Id, AddressLine1 = "Foreign" });
            db.Suppliers.AddRange(first, second, inactive, outsider);
            await db.SaveChangesAsync();
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
            host.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
            string ListUrl(int tenant, bool showInactive = false) => "/api/suppliers?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(new { userConfigId = tenant, showInactive }));
            var url = ListUrl(company.Id);
            var legacy = await host.Client.GetFromJsonAsync<JsonElement>(url);
            Assert.Equal(JsonValueKind.Array, legacy.ValueKind);
            Assert.Equal(9, legacy[0].EnumerateObject().Count());
            Assert.False(legacy[0].TryGetProperty("supplierContacts", out _));
            var expanded = await host.Client.GetFromJsonAsync<JsonElement>(url + "&includeDetails=true&pageNumber=1&pageSize=1");
            var expandedItem = expanded.GetProperty("items")[0];
            Assert.Equal(first.Id, expandedItem.GetProperty("id").GetInt32());
            Assert.Equal(2, expandedItem.GetProperty("supplierContacts").GetArrayLength());
            Assert.Equal("Primary", expandedItem.GetProperty("supplierContacts")[0].GetProperty("name").GetString());
            Assert.True(expandedItem.GetProperty("supplierAddresses")[0].GetProperty("isPrimaryAddress").GetBoolean());
            Assert.Equal("Test province", expandedItem.GetProperty("supplierAddresses")[0].GetProperty("cityMunicipality").GetProperty("stateProvince").GetProperty("name").GetString());
            var expandedSecond = await host.Client.GetFromJsonAsync<JsonElement>(url + "&includeDetails=true&pageNumber=2&pageSize=1");
            Assert.Equal(0, expandedSecond.GetProperty("items")[0].GetProperty("supplierContacts").GetArrayLength());
            var expandedExport = await host.Client.GetFromJsonAsync<JsonElement>(url + "&includeDetails=true&search=0917-123");
            Assert.Single(expandedExport.EnumerateArray());
            Assert.Equal(2, expandedExport[0].GetProperty("supplierContacts").GetArrayLength());
            Assert.Single(expandedExport[0].GetProperty("supplierAddresses").EnumerateArray());
            Assert.Equal(9, (await host.Client.GetFromJsonAsync<JsonElement>(url + "&includeDetails=false"))[0].EnumerateObject().Count());
            var searchedByName = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=Same");
            Assert.Equal(2, searchedByName.GetProperty("totalCount").GetInt32());
            var noMatches = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=NotPresent");
            Assert.Equal(0, noMatches.GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/suppliers", new { UserConfigId = company.Id, Name = new string('x', 101) })).StatusCode);
            Assert.Equal(new[] { first.Id, second.Id }, legacy.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()));
            Assert.Equal(3, (await host.Client.GetFromJsonAsync<JsonElement>(ListUrl(company.Id, true))).GetArrayLength());
            var page = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=1");
            Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
            Assert.Equal(second.Id, page.GetProperty("items")[0].GetProperty("id").GetInt32());
            var sortedPage = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&sortBy=tin&sortDirection=desc");
            Assert.Equal(first.Id, sortedPage.GetProperty("items")[0].GetProperty("id").GetInt32());
            var sortedNext = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=1&sortBy=tin&sortDirection=desc");
            Assert.Equal(second.Id, sortedNext.GetProperty("items")[0].GetProperty("id").GetInt32());
            var sortedAll = await host.Client.GetFromJsonAsync<JsonElement>(url + "&sortBy=tin&sortDirection=asc");
            Assert.Equal(new[] { second.Id, first.Id }, sortedAll.EnumerateArray().Select(s => s.GetProperty("id").GetInt32()));
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url + "&sortBy=creditLimit")).StatusCode);
            var searched = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=" + Uri.EscapeDataString("ID-&%_?"));
            Assert.Equal(1, searched.GetProperty("totalCount").GetInt32());
            Assert.Equal(first.Id, searched.GetProperty("items")[0].GetProperty("id").GetInt32());
            var hiddenInactive = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=Inactive");
            Assert.Equal(0, hiddenInactive.GetProperty("totalCount").GetInt32());
            var visibleInactive = await host.Client.GetFromJsonAsync<JsonElement>(ListUrl(company.Id, true) + "&pageNumber=1&pageSize=1&search=Inactive");
            Assert.Equal(1, visibleInactive.GetProperty("totalCount").GetInt32());
            var foreignSearch = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=Other");
            Assert.Equal(0, foreignSearch.GetProperty("totalCount").GetInt32());
            foreach (var term in new[] { "Primary", "0917-123", "accounts@example.test", "Street", "Test city", "Test province", "1000" })
            {
                var related = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&search=" + Uri.EscapeDataString(term));
                Assert.Equal(1, related.GetProperty("totalCount").GetInt32());
                Assert.Equal(first.Id, related.GetProperty("items")[0].GetProperty("id").GetInt32());
            }
            var relatedUnpaged = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=0917-123");
            Assert.Equal(1, relatedUnpaged.GetArrayLength());
            var relatedInactive = await host.Client.GetFromJsonAsync<JsonElement>(ListUrl(company.Id, true) + "&search=0917-123&pageNumber=2&pageSize=1&sortBy=status&sortDirection=desc");
            Assert.Equal(2, relatedInactive.GetProperty("totalCount").GetInt32());
            Assert.Equal(inactive.Id, relatedInactive.GetProperty("items")[0].GetProperty("id").GetInt32());
            var foreignContact = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=Foreign");
            Assert.Equal(0, foreignContact.GetArrayLength());
            var empty = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=1");
            Assert.Equal(0, empty.GetProperty("items").GetArrayLength());
            foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=abc&pageSize=1" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url + suffix)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/suppliers?criteria=%7Bbroken")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/suppliers")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(ListUrl(other.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/suppliers/{outsider.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"/api/suppliers/{outsider.Id}")).StatusCode);
            var foreignUpdate = new SupplierWriteRequest { Id = outsider.Id, UserConfigId = company.Id, Name = "Rejected", Status = true };
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PutAsJsonAsync($"/api/suppliers/{outsider.Id}", foreignUpdate)).StatusCode);
            foreignUpdate.Id = first.Id;
            foreignUpdate.UserConfigId = other.Id;
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", foreignUpdate)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/api/suppliers", new { UserConfigId = other.Id, Name = "Rejected" })).StatusCode);

            // Reject foreign child IDs before even a parent-name update reaches the database.
            var request = new SupplierWriteRequest { Id = first.Id, UserConfigId = company.Id, Name = "Must not save", Status = true };
            request.SupplierContacts.Add(new SupplierContactRequest { Id = outsider.SupplierContacts.Single().Id, Deleted = true });
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", request)).StatusCode);

            request.SupplierAddresses.Clear();
            request.SupplierContacts.Clear();
            request.SupplierContacts.Add(new SupplierContactRequest { Id = first.SupplierContacts.First().Id, Name = "Duplicate" });
            request.SupplierContacts.Add(new SupplierContactRequest { Id = first.SupplierContacts.First().Id, Deleted = true });
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", request)).StatusCode);
            request.SupplierContacts.Clear();
            request.PaymentTermId = int.MaxValue;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", request)).StatusCode);
            request.PaymentTermId = null;
            Assert.Equal("Same", await db.Suppliers.AsNoTracking().Where(c => c.Id == first.Id).Select(c => c.Name).SingleAsync());
            request.SupplierContacts.Clear();
            request.SupplierAddresses.Add(new SupplierAddressRequest { Id = outsider.SupplierAddresses.Single().Id, Deleted = true });
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", request)).StatusCode);

            // Round-trip a full legacy detail payload with nested lookups and audit fields.
            var detail = await host.Client.GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>($"/api/suppliers/{first.Id}");
            Assert.Equal(JsonValueKind.Null, JsonSerializer.SerializeToElement(detail).GetProperty("taxRate").ValueKind);
            detail["name"] = "Updated";
            detail["createdDate"] = "1990-01-01T00:00:00";
            detail["createdByUserId"] = 999;
            detail["supplierContacts"][0]["name"] = "Changed contact";
            detail["supplierContacts"][1]["deleted"] = true;
            detail["supplierAddresses"][0]["deleted"] = true;
            detail["supplierContacts"].AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["name"] = "New contact" });
            var updatedResponse = await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", detail);
            Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
            var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Updated", updated.GetProperty("name").GetString());
            Assert.Equal(2020, updated.GetProperty("createdDate").GetDateTime().Year);
            Assert.Equal(42, updated.GetProperty("createdByUserId").GetInt32());
            Assert.Equal(2, updated.GetProperty("supplierContacts").GetArrayLength());
            Assert.Equal(0, updated.GetProperty("supplierAddresses").GetArrayLength());
            var nullCollections = new SupplierWriteRequest { Id = first.Id, UserConfigId = company.Id, Name = "Updated", Status = true, SupplierContacts = null, SupplierAddresses = null };
            var preserved = await host.Client.PutAsJsonAsync($"/api/suppliers/{first.Id}", nullCollections);
            Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
            Assert.Equal(2, (await preserved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("supplierContacts").GetArrayLength());

            var create = new SupplierWriteRequest { UserConfigId = company.Id, Name = "Created", Status = true,
                SupplierContacts = new() { new() { Name = "Created contact" } },
                SupplierAddresses = new() { new() { CityMunicipalityId = city.Id, AddressLine1 = "New address" } } };
            var createdResponse = await host.Client.PostAsJsonAsync("/api/suppliers", create);
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            Assert.NotNull(createdResponse.Headers.Location);
            var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
            var createdId = created.GetProperty("id").GetInt32();
            Assert.Equal(createdId, created.GetProperty("supplierContacts")[0].GetProperty("supplierId").GetInt32());
            Assert.Equal(createdId, created.GetProperty("supplierAddresses")[0].GetProperty("supplierId").GetInt32());
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"/api/suppliers/{createdId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/suppliers/{createdId}")).StatusCode);
            Assert.False(await db.SupplierContacts.AsNoTracking().AnyAsync(c => c.SupplierId == createdId));

            // A restrictive FK simulates a transaction referencing this supplier: deletion must roll back child removals.
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE supplier_guard (id INT PRIMARY KEY, supplier_id INT NOT NULL, FOREIGN KEY (supplier_id) REFERENCES supplier(Id))");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO supplier_guard (id, supplier_id) VALUES (1, {first.Id})");
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.DeleteAsync($"/api/suppliers/{first.Id}")).StatusCode);
            Assert.True(await db.Suppliers.AsNoTracking().AnyAsync(c => c.Id == first.Id));
            Assert.Equal(2, await db.SupplierContacts.AsNoTracking().CountAsync(c => c.SupplierId == first.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
