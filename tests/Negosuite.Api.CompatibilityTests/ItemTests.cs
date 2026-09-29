using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Items;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class ItemTests
{
    public static IEnumerable<object[]> SortCases() => from field in new[] { "code", "name", "itemCategoryName", "type", "typeName", "unit", "rate", "cost", "status", "toSell", "toPurchase", "trackInventory", "reorderPoint" }
        from direction in new[] { "asc", "desc" } select new object[] { field, direction };

    [Theory, MemberData(nameof(SortCases))]
    public void Sorts_translate_to_mysql_before_paging(string field, string direction)
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var sql = ItemQuery.Sort(ItemQuery.Search(db.Items.Where(i => i.UserConfigId == 42), "Goods"), field, direction)
            .Skip(10).Take(10).Select(i => i.Id).ToQueryString();
        Assert.Contains("ORDER BY", sql); Assert.Contains("LIMIT", sql); Assert.Contains("OFFSET", sql);
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    [Fact]
    public void Numeric_sort_is_numeric_and_ties_are_stable()
    {
        var rows = new[] { new Item { Id = 3, Rate = 10 }, new Item { Id = 2, Rate = 2 }, new Item { Id = 1, Rate = 2 }, new Item { Id = 4 } }.AsQueryable();
        Assert.Equal(new[] { 3, 1, 2, 4 }, ItemQuery.Sort(rows, "rate", "desc").Select(i => i.Id));
        Assert.False(ItemQuery.IsValidSort("name desc; drop table item", "asc"));
        Assert.False(ItemQuery.IsValidSort("name", "up"));
    }

    [MySqlFact]
    public async Task Item_http_contract_filters_paging_writes_and_auxiliary_routes()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_items_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            // Exercise company/SKU uniqueness; EF's generated Code column is text, unlike the existing database.
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX Code_UNIQUE ON item (UserConfigId, Code(100))");
            var company = new Config { CompanyName = "Items fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other", Uuid = Guid.NewGuid().ToString() };
            db.Configs.AddRange(company, other); await db.SaveChangesAsync();
            var category = new ItemCategory { UserConfigId = company.Id, Name = "Hardware", Status = true };
            var foreignCategory = new ItemCategory { UserConfigId = other.Id, Name = "Foreign", Status = true };
            db.ItemCategories.AddRange(category, foreignCategory); await db.SaveChangesAsync();
            var first = new Item { UserConfigId = company.Id, Name = "Same", Code = "SKU-%_&", Type = "G", Status = true, Unit = "pc", Rate = 10, ToSell = true, ToPurchase = true, TrackInventory = true,
                ItemCategoryId = category.Id, AverageCost = 7, LastPurchasedDate = new DateTime(2026, 1, 1), CreatedDate = new DateTime(2020, 1, 1), CreatedByUserId = 42 };
            var second = new Item { UserConfigId = company.Id, Name = "Same", Code = "service", Type = "S", Status = true, Unit = "hour", Rate = 2, ToSell = true };
            var inactive = new Item { UserConfigId = company.Id, Name = "Inactive", Status = false, Unit = "pc" };
            var foreign = new Item { UserConfigId = other.Id, Name = "Foreign", Status = true, Unit = "foreign-unit" };
            db.Items.AddRange(first, second, inactive, foreign); await db.SaveChangesAsync();
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
            host.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
            string Url(object criteria) => "/api/items?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(criteria));
            var url = Url(new { userConfigId = company.Id });
            var legacy = await host.Client.GetFromJsonAsync<JsonElement>(url);
            Assert.Equal(2, legacy.GetArrayLength()); Assert.Equal(15, legacy[0].EnumerateObject().Count());
            Assert.Equal("Goods", legacy[0].GetProperty("typeName").GetString());
            Assert.Equal(new[] { first.Id, second.Id }, legacy.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
            Assert.Single((await host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = company.Id, itemType = "S" }))).EnumerateArray());
            Assert.Single((await host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = company.Id, itemCategoryId = category.Id }))).EnumerateArray());
            Assert.Equal(3, (await host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = company.Id, showInactive = true }))).GetArrayLength());
            Assert.Single((await host.Client.GetFromJsonAsync<JsonElement>(url + "&toPurchase=true")).EnumerateArray());
            foreach (var term in new[] { "SKU-%_&", "Hardware", "Goods", "pc" })
                Assert.Single((await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=" + Uri.EscapeDataString(term))).EnumerateArray());
            var page = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=1&sortBy=rate&sortDirection=desc");
            Assert.Equal(2, page.GetProperty("totalCount").GetInt32()); Assert.Equal(second.Id, page.GetProperty("items")[0].GetProperty("id").GetInt32());
            Assert.Empty((await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=1")).GetProperty("items").EnumerateArray());
            foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&sortBy=unknown", "&sortDirection=up" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url + suffix)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync("/api/items?criteria=%7Bbroken")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(Url(new { userConfigId = other.Id }))).StatusCode);
            foreach (var path in new[] { $"/api/items/{foreign.Id}", $"/api/items/average-cost/{foreign.Id}" })
                Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"/api/items/{foreign.Id}")).StatusCode);
            var units = await host.Client.GetFromJsonAsync<JsonElement>(url.Replace("/api/items?", "/api/items/units?"));
            Assert.Equal(new[] { "hour", "pc" }, units.EnumerateArray().Select(u => u.GetProperty("name").GetString()).OrderBy(x => x));
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/items/average-cost/{first.Id}")).StatusCode);
            var supplier = new Supplier { UserConfigId = company.Id, Name = "Test supplier", Status = true };
            var bill = new Bill { UserConfigId = company.Id, BillNo = "fixture", BillDate = DateTime.Today, DueDate = DateTime.Today, Supplier = supplier };
            bill.BillDetails.Add(new BillDetail { ItemId = first.Id, Quantity = 2, Rate = 10 });
            bill.BillDetails.Add(new BillDetail { ItemId = first.Id, Quantity = 3, Rate = 20 });
            bill.BillDetails.Add(new BillDetail { ItemId = second.Id, Quantity = 0, Rate = 20 });
            db.Bills.Add(bill); await db.SaveChangesAsync();
            Assert.Equal(16m, await host.Client.GetFromJsonAsync<decimal>($"/api/items/average-cost/{first.Id}"));
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/items/average-cost/{second.Id}")).StatusCode);
            var create = new ItemCreateRequest { UserConfigId = company.Id, Name = "Created", Code = "new", Status = true, ItemCategoryId = foreignCategory.Id };
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/items", create)).StatusCode);
            create.ItemCategoryId = category.Id; create.Code = first.Code;
            create.InventoryAccountId = int.MaxValue; create.Code = "new";
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/items", create)).StatusCode);
            create.InventoryAccountId = null; create.PurchaseTaxRateId = int.MaxValue;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/items", create)).StatusCode);
            create.PurchaseTaxRateId = null; create.Code = first.Code;
            var duplicate = await host.Client.PostAsJsonAsync("/api/items", create);
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode); Assert.Contains(ItemService.DuplicateSku, await duplicate.Content.ReadAsStringAsync());
            create.Code = "new";
            var createdResponse = await host.Client.PostAsJsonAsync("/api/items", create);
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode); Assert.NotNull(createdResponse.Headers.Location);
            var created = await createdResponse.Content.ReadFromJsonAsync<ItemDetailDto>();
            var detail = await host.Client.GetFromJsonAsync<System.Text.Json.Nodes.JsonObject>($"/api/items/{first.Id}");
            detail["name"] = "Updated"; detail["averageCost"] = 999; detail["lastPurchasedDate"] = "1990-01-01";
            detail["createdDate"] = "1990-01-01"; detail["createdByUserId"] = 999;
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PutAsJsonAsync($"/api/items/{first.Id}", detail)).StatusCode);
            var updated = await host.Client.GetFromJsonAsync<ItemDetailDto>($"/api/items/{first.Id}");
            Assert.Equal("Updated", updated.Name); Assert.Equal(7m, updated.AverageCost); Assert.Equal(2026, updated.LastPurchasedDate.Value.Year);
            Assert.Equal(2020, updated.CreatedDate.Value.Year); Assert.Equal(42, updated.CreatedByUserId);
            detail["code"] = "new";
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"/api/items/{first.Id}", detail)).StatusCode);
            detail["id"] = foreign.Id;
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PutAsJsonAsync($"/api/items/{foreign.Id}", detail)).StatusCode);
            // EF's generated schema uses cascades for some required links. Exercise a restrictive reference explicitly.
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE item_guard (id INT PRIMARY KEY, item_id INT NOT NULL, FOREIGN KEY (item_id) REFERENCES item(Id))");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO item_guard (id, item_id) VALUES (1, {first.Id})");
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.DeleteAsync($"/api/items/{first.Id}")).StatusCode);
            Assert.True(await db.Items.AsNoTracking().AnyAsync(i => i.Id == first.Id));
            Assert.Equal(2, await db.BillDetails.AsNoTracking().CountAsync(d => d.ItemId == first.Id));
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"/api/items/{created.Id}")).StatusCode);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
