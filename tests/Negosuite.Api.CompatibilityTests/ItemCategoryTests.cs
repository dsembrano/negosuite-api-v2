using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.ItemCategories;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class ItemCategoryTests
{
    public static IEnumerable<object[]> SortCases() => from field in new[] { "name", "status", "createdDate", "lastUpdatedDate" }
        from direction in new[] { "asc", "desc" } select new object[] { field, direction };

    [Theory, MemberData(nameof(SortCases))]
    public void Search_and_sort_translate_to_mysql_before_paging(string field, string direction)
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var sql = ItemCategoryQuery.Sort(ItemCategoryQuery.Search(db.ItemCategories.Where(c => c.UserConfigId == 42), "Hardware"), field, direction)
            .Skip(10).Take(10).Select(c => c.Id).ToQueryString();
        Assert.Contains("ORDER BY", sql); Assert.Contains("LIMIT", sql); Assert.Contains("OFFSET", sql);
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    [MySqlFact]
    public async Task Category_http_contract_preserves_legacy_lists_and_scopes_filtered_pages_and_writes()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_categories_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var company = new Config { CompanyName = "Category fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other category fixture", Uuid = Guid.NewGuid().ToString() };
            db.Configs.AddRange(company, other); await db.SaveChangesAsync();
            var first = new ItemCategory { UserConfigId = company.Id, Name = "Z%_&", Status = true,
                CreatedDate = new DateTime(2020, 1, 1), LastUpdatedDate = new DateTime(2021, 1, 1), CreatedByUserId = 42, LastUpdatedByUserId = 43 };
            var second = new ItemCategory { UserConfigId = company.Id, Name = "Alpha", Status = true, CreatedDate = new DateTime(2022, 1, 1) };
            var inactive = new ItemCategory { UserConfigId = company.Id, Name = "Alpha", Status = false };
            var foreign = new ItemCategory { UserConfigId = other.Id, Name = "Foreign", Status = true };
            db.ItemCategories.AddRange(first, second, inactive, foreign); await db.SaveChangesAsync();
            const string path = "/api/item-categories";
            string Url(object criteria) => path + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(criteria));
            var url = Url(new { userConfigId = company.Id });
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(url)).StatusCode);
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
            host.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
            var legacy = await host.Client.GetFromJsonAsync<JsonElement>(url);
            // Same projection fields and values as the original entity-returning controller.
            var expected = JsonSerializer.Serialize(await db.ItemCategories.AsNoTracking().Where(c => c.UserConfigId == company.Id).ToListAsync(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(legacy.GetRawText()))); Assert.Equal(3, legacy.GetArrayLength());
            Assert.Equal(8, legacy[0].EnumerateObject().Count());
            Assert.Equal(3, (await host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = company.Id, showInactive = false, status = true }))).GetArrayLength());
            Assert.Equal(2, (await host.Client.GetFromJsonAsync<JsonElement>(url + "&status=true")).GetArrayLength());
            var onlyInactive = await host.Client.GetFromJsonAsync<ItemCategoryDetailDto[]>(url + "&status=false");
            Assert.Equal(inactive.Id, Assert.Single(onlyInactive).Id);
            Assert.Equal(first.Id, Assert.Single(await host.Client.GetFromJsonAsync<ItemCategoryDetailDto[]>(url + "&search=" + Uri.EscapeDataString(" %_& "))).Id);
            Assert.Equal(3, (await host.Client.GetFromJsonAsync<ItemCategoryDetailDto[]>(url + "&search=%20%20")).Length);
            var page = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=Alpha&pageNumber=2&pageSize=1");
            Assert.Equal(2, page.GetProperty("totalCount").GetInt32()); Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
            Assert.Equal(2, page.GetProperty("pageNumber").GetInt32()); Assert.Equal(1, page.GetProperty("pageSize").GetInt32());
            Assert.Equal(inactive.Id, page.GetProperty("items")[0].GetProperty("id").GetInt32());
            var filtered = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=Alpha&status=true&pageNumber=1&pageSize=1");
            Assert.Equal(1, filtered.GetProperty("totalCount").GetInt32());
            Assert.Equal(second.Id, filtered.GetProperty("items")[0].GetProperty("id").GetInt32());
            var beyond = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=1");
            Assert.Empty(beyond.GetProperty("items").EnumerateArray()); Assert.Equal(3, beyond.GetProperty("totalCount").GetInt32());
            var empty = await host.Client.GetFromJsonAsync<JsonElement>(url + "&search=absent&pageNumber=1&pageSize=1");
            Assert.Equal(0, empty.GetProperty("totalPages").GetInt32()); Assert.Empty(empty.GetProperty("items").EnumerateArray());
            foreach (var sort in SortCases())
            {
                var rows = await host.Client.GetFromJsonAsync<ItemCategoryDetailDto[]>(url + $"&sortBy={sort[0]}&sortDirection={sort[1]}");
                var sortedIds = ItemCategoryQuery.Sort(new[] { first, second, inactive }.AsQueryable(), (string)sort[0], (string)sort[1]).Select(c => c.Id);
                Assert.Equal(sortedIds, rows.Select(c => c.Id));
                var sortedPage = await host.Client.GetFromJsonAsync<JsonElement>(url + $"&sortBy={sort[0]}&sortDirection={sort[1]}&pageNumber=2&pageSize=1");
                Assert.Equal(rows[1].Id, sortedPage.GetProperty("items")[0].GetProperty("id").GetInt32());
            }
            foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=0", "&pageNumber=1&pageSize=201", "&pageNumber=2147483647&pageSize=200", "&sortBy=unknown", "&sortDirection=up", "&status=invalid" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url + suffix)).StatusCode);
            foreach (var badCriteria in new[] { "", "?criteria=null", "?criteria=%7B%7D", "?criteria=%7Bbroken", "?criteria=%5B%5D" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(path + badCriteria)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(Url(new { userConfigId = other.Id }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"{path}/{foreign.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"{path}/{foreign.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"{path}/{int.MaxValue}")).StatusCode);
            var create = new ItemCategoryCreateRequest { UserConfigId = other.Id, Name = "Created", Status = true };
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync(path, create)).StatusCode);
            create.UserConfigId = company.Id;
            foreach (var name in new[] { "", " ", new string('x', 151) })
            {
                create.Name = name;
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync(path, create)).StatusCode);
            }
            create.Name = new string('x', 150); create.Id = first.Id;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync(path, create)).StatusCode);
            create.Id = 0;
            var createdResponse = await host.Client.PostAsJsonAsync(path, create);
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode); Assert.NotNull(createdResponse.Headers.Location);
            var created = await createdResponse.Content.ReadFromJsonAsync<ItemCategoryDetailDto>();
            Assert.NotNull(created.CreatedDate); Assert.Null(created.CreatedByUserId);
            Assert.Equal(created.Id, (await host.Client.GetFromJsonAsync<ItemCategoryDetailDto>(createdResponse.Headers.Location)).Id);
            var detail = await host.Client.GetFromJsonAsync<JsonObject>($"{path}/{first.Id}");
            detail["name"] = "Updated"; detail["status"] = false;
            detail["createdDate"] = "1990-01-01"; detail["createdByUserId"] = 999;
            detail["lastUpdatedDate"] = "1990-01-01"; detail["lastUpdatedByUserId"] = 999;
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PutAsJsonAsync($"{path}/{first.Id}", detail)).StatusCode);
            var updated = await host.Client.GetFromJsonAsync<ItemCategoryDetailDto>($"{path}/{first.Id}");
            Assert.Equal("Updated", updated.Name); Assert.False(updated.Status);
            Assert.Equal(first.CreatedDate, updated.CreatedDate); Assert.Equal(42, updated.CreatedByUserId); Assert.Equal(43, updated.LastUpdatedByUserId);
            Assert.True(updated.LastUpdatedDate > first.LastUpdatedDate);
            detail["userConfigId"] = other.Id;
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsJsonAsync($"{path}/{first.Id}", detail)).StatusCode);
            detail["userConfigId"] = company.Id;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"{path}/{second.Id}", detail)).StatusCode);
            detail["id"] = foreign.Id;
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PutAsJsonAsync($"{path}/{foreign.Id}", detail)).StatusCode);
            Assert.True(await db.ItemCategories.AsNoTracking().AnyAsync(c => c.Id == foreign.Id && c.Name == "Foreign"));
            var item = new Item { UserConfigId = company.Id, Name = "Referenced item", Status = true, ItemCategoryId = first.Id };
            db.Items.Add(item); await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.DeleteAsync($"{path}/{first.Id}")).StatusCode);
            Assert.True(await db.ItemCategories.AsNoTracking().AnyAsync(c => c.Id == first.Id));
            Assert.True(await db.Items.AsNoTracking().AnyAsync(i => i.Id == item.Id && i.ItemCategoryId == first.Id));
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"{path}/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"{path}/{created.Id}")).StatusCode);
            db.ItemCategories.AddRange(Enumerable.Range(1, 205).Select(i => new ItemCategory { UserConfigId = company.Id, Name = "Bulk " + i, Status = true }));
            await db.SaveChangesAsync();
            Assert.Equal(208, (await host.Client.GetFromJsonAsync<ItemCategoryDetailDto[]>(url)).Length);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
