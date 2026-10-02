using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Accounts;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class AccountTests
{
    public static IEnumerable<object[]> SortCases() =>
        from category in new[] { false, true }
        from field in category ? new[] { "name", "type", "accountCodePrefix", "orderNo", "accountCount" }
            : new[] { "code", "name", "categoryName", "parentAccountCode", "parentAccountName", "requireCustomer", "requireSupplier", "type" }
        from direction in new[] { "asc", "desc" }
        select new object[] { category, field, direction };

    [Theory, MemberData(nameof(SortCases))]
    public void All_sorts_and_search_translate_to_mysql_before_paging(bool category, string field, string direction)
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>()
            .UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var sql = category
            ? AccountCategoryQuery.Sort(AccountCategoryQuery.Search(new AccountCategoryService(db).ListQuery(42), "%_"), field, direction).Skip(10).Take(10).ToQueryString()
            : AccountQuery.Sort(AccountQuery.Search(new AccountService(db).ListQuery(42), "%_"), field, direction).Skip(10).Take(10).ToQueryString();
        Assert.Contains("ORDER BY", sql); Assert.Contains("LIMIT", sql); Assert.Contains("OFFSET", sql);
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    private static string Url(string path, int company, int? category = null) => path + "?criteria=" +
        Uri.EscapeDataString(JsonSerializer.Serialize(new { userConfigId = company, categoryId = category }));

    [MySqlFact]
    public async Task Lists_preserve_fields_and_support_search_sort_paging_and_scoped_lookups()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var category = new AccountCategory { UserConfigId = f.Company.Id, Name = "Z%_", Type = "L", OrderNo = 12, AccountCodePrefix = "200" };
        f.Db.AccountCategories.Add(category); await f.Db.SaveChangesAsync();
        var child = new Account { UserConfigId = f.Company.Id, Code = "200", Name = "Child %_", CategoryId = category.Id, ParentAccountId = f.Account.Id, IsSubAccount = true, RequireSupplier = true };
        var noCode = new Account { UserConfigId = f.Company.Id, Name = "No code", CategoryId = category.Id };
        f.Db.Accounts.AddRange(child, noCode); await f.Db.SaveChangesAsync();
        var url = Url("/api/accounts", f.Company.Id);
        var rows = await f.Host.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(3, rows.GetArrayLength());
        foreach (var field in new[] { "categoryId", "categoryName", "code", "id", "name", "parentAccountCode", "parentAccountId", "parentAccountName", "requireCustomer", "requireSupplier", "sortCode", "type" })
            Assert.True(rows[0].TryGetProperty(field, out _), field);
        Assert.Equal("", rows[0].GetProperty("code").GetString()); Assert.Equal("", rows[0].GetProperty("sortCode").GetString());
        var listedChild = rows.EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == child.Id);
        Assert.Equal(f.Account.Name, listedChild.GetProperty("parentAccountName").GetString());
        Assert.Equal(2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url("/api/accounts", f.Company.Id, category.Id))).GetArrayLength());
        Assert.Equal(3, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url("/api/accounts", f.Company.Id, 0))).GetArrayLength());
        var page = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=" + Uri.EscapeDataString(" %_ ") + "&pageNumber=2&pageSize=1&sortBy=name&sortDirection=desc");
        // Both accounts in the category match its name; LIKE metacharacters are literal.
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32()); Assert.Equal(child.Id, page.GetProperty("items")[0].GetProperty("id").GetInt32());
        foreach (var route in new[] { "header", "link" })
        {
            var lookup = await f.Host.Client.GetFromJsonAsync<JsonElement>("/api/accounts/" + route);
            Assert.Equal(3, lookup.GetArrayLength()); Assert.Equal(route == "link" ? 9 : 8, lookup[0].EnumerateObject().Count());
            Assert.All(lookup.EnumerateArray(), a => Assert.NotEqual(f.ForeignAccount.Id, a.GetProperty("id").GetInt32()));
            if (route == "link") Assert.Equal(f.Account.Id, lookup.EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == child.Id).GetProperty("parentAccount").GetProperty("id").GetInt32());
        }
        var categories = await f.Host.Client.GetFromJsonAsync<JsonElement>(Url("/api/account-categories", f.Company.Id));
        Assert.Equal(2, categories.GetArrayLength()); Assert.Equal(6, categories[0].EnumerateObject().Count());
        Assert.Equal(2, categories[1].GetProperty("accountCount").GetInt32());
        foreach (var args in SortCases())
        {
            var isCategory = (bool)args[0]; var sort = (string)args[1]; var direction = (string)args[2];
            var path = Url(isCategory ? "/api/account-categories" : "/api/accounts", f.Company.Id);
            var sorted = await f.Host.Client.GetFromJsonAsync<JsonElement>(path + $"&sortBy={sort}&sortDirection={direction}");
            var paged = await f.Host.Client.GetFromJsonAsync<JsonElement>(path + $"&sortBy={sort}&sortDirection={direction}&pageNumber=2&pageSize=1");
            Assert.Equal(sorted.GetArrayLength(), paged.GetProperty("totalCount").GetInt32());
            Assert.Equal(sorted[1].GetProperty("id").GetInt32(), paged.GetProperty("items")[0].GetProperty("id").GetInt32());
        }
        var countSorted = await f.Host.Client.GetFromJsonAsync<JsonElement>(Url("/api/account-categories", f.Company.Id) + "&sortBy=accountCount&sortDirection=desc");
        Assert.Equal(category.Id, countSorted[0].GetProperty("id").GetInt32());
        foreach (var path in new[] { "/api/accounts", "/api/account-categories" })
        {
            var listUrl = Url(path, f.Company.Id);
            foreach (var suffix in new[] { "&pageNumber=1", "&pageSize=10", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=2147483647&pageSize=200", "&sortBy=invalid", "&sortDirection=up" })
                Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(listUrl + suffix)).StatusCode);
            foreach (var criteria in new[] { "", "?criteria=null", "?criteria=%7B%7D", "?criteria=broken", "?criteria=%5B%5D" })
                Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(path + criteria)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync(Url(path, f.Other.Id))).StatusCode);
            var beyond = await f.Host.Client.GetFromJsonAsync<JsonElement>(listUrl + "&pageNumber=99&pageSize=1");
            Assert.Empty(beyond.GetProperty("items").EnumerateArray());
            var empty = await f.Host.Client.GetFromJsonAsync<JsonElement>(listUrl + "&search=absent&pageNumber=1&pageSize=1");
            Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
        }
        f.Db.Accounts.AddRange(Enumerable.Range(1, 205).Select(i => new Account { UserConfigId = f.Company.Id, Name = "Bulk " + i, CategoryId = category.Id }));
        await f.Db.SaveChangesAsync();
        Assert.Equal(208, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url)).GetArrayLength());
        f.Host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync(url)).StatusCode);
    }

    [MySqlFact]
    public async Task Writes_validate_ownership_hierarchy_and_references_and_ignore_nested_entities_and_audit_fields()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        const string accounts = "/api/accounts", categories = "/api/account-categories";
        foreach (var (path, foreignId) in new[] { (accounts, f.ForeignAccount.Id), (categories, f.ForeignAccount.CategoryId) })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{foreignId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.DeleteAsync($"{path}/{foreignId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{int.MaxValue}")).StatusCode);
        }
        var categoryInput = new AccountCategoryCreateRequest { UserConfigId = f.Company.Id, Name = "New", Type = "A", OrderNo = 3, AccountCodePrefix = "100" };
        var categoryResponse = await f.Host.Client.PostAsJsonAsync(categories, categoryInput);
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = await categoryResponse.Content.ReadFromJsonAsync<AccountCategoryDetailDto>();
        Assert.NotNull(category.CreatedDate); Assert.NotNull(categoryResponse.Headers.Location);
        var input = new AccountCreateRequest { UserConfigId = f.Company.Id, Code = "100", Name = "New account", CategoryId = category.Id, ParentAccountId = f.Account.Id, IsSubAccount = true, Notes = "Note", RequireCustomer = true };
        var response = await f.Host.Client.PostAsJsonAsync(accounts, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AccountDetailDto>(); Assert.NotNull(response.Headers.Location);
        Assert.Equal(f.Account.Id, created.ParentAccount.Id); Assert.Equal(category.Id, created.Category.Id);
        var detail = await f.Host.Client.GetFromJsonAsync<JsonObject>(response.Headers.Location);
        detail["name"] = "Updated"; detail["createdDate"] = "1990-01-01"; detail["createdByUserId"] = 999;
        detail["category"]["name"] = "Overposted category"; detail["parentAccount"]["name"] = "Overposted parent";
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"{accounts}/{created.Id}", detail)).StatusCode);
        var updated = await f.Host.Client.GetFromJsonAsync<AccountDetailDto>($"{accounts}/{created.Id}");
        Assert.Equal("Updated", updated.Name); Assert.Equal(created.CreatedDate, updated.CreatedDate); Assert.Null(updated.CreatedByUserId);
        Assert.NotNull(updated.LastUpdatedDate); Assert.Equal("New", updated.Category.Name); Assert.Equal(f.Account.Name, updated.ParentAccount.Name);
        foreach (var parent in new[] { created.Id, f.ForeignAccount.Id, int.MaxValue })
        {
            detail["parentAccountId"] = parent;
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{accounts}/{created.Id}", detail)).StatusCode);
        }
        var ancestor = await f.Host.Client.GetFromJsonAsync<JsonObject>($"{accounts}/{f.Account.Id}"); ancestor["parentAccountId"] = created.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{accounts}/{f.Account.Id}", ancestor)).StatusCode);
        detail["parentAccountId"] = null; detail["categoryId"] = f.ForeignAccount.CategoryId;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{accounts}/{created.Id}", detail)).StatusCode);
        detail["categoryId"] = category.Id; detail["userConfigId"] = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"{accounts}/{created.Id}", detail)).StatusCode);
        detail["userConfigId"] = f.Company.Id; detail["id"] = f.ForeignAccount.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.PutAsJsonAsync($"{accounts}/{f.ForeignAccount.Id}", detail)).StatusCode);
        var categoryDetail = await f.Host.Client.GetFromJsonAsync<JsonObject>(categoryResponse.Headers.Location);
        categoryDetail["name"] = "Updated category"; categoryDetail["createdDate"] = "1990-01-01";
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"{categories}/{category.Id}", categoryDetail)).StatusCode);
        Assert.Equal(category.CreatedDate, (await f.Host.Client.GetFromJsonAsync<AccountCategoryDetailDto>(categoryResponse.Headers.Location)).CreatedDate);
        categoryDetail["id"] = f.ForeignAccount.CategoryId;
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.PutAsJsonAsync($"{categories}/{f.ForeignAccount.CategoryId}", categoryDetail)).StatusCode);
        categoryDetail["userConfigId"] = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"{categories}/{f.ForeignAccount.CategoryId}", categoryDetail)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"{categories}/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"{accounts}/{f.Account.Id}")).StatusCode);
        input.UserConfigId = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(accounts, input)).StatusCode);
        categoryInput.UserConfigId = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PostAsJsonAsync(categories, categoryInput)).StatusCode);
        input.UserConfigId = f.Company.Id; input.Name = new string('x', 151);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(accounts, input)).StatusCode);
        input.Name = "Valid"; input.Code = new string('x', 21);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(accounts, input)).StatusCode);
        categoryInput.UserConfigId = f.Company.Id; categoryInput.Type = new string('x', 11);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(categories, categoryInput)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{accounts}/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{categories}/{category.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.DeleteAsync($"{categories}/{category.Id}")).StatusCode);
    }
}
