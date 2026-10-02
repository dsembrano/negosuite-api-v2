using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using negosuite_api.Controllers;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class PagePreferenceTests
{
    public sealed class MySqlTheoryAttribute : TheoryAttribute
    {
        public MySqlTheoryAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL")))
                Skip = "Run scripts/Test-Phase3MySql.ps1 to use the isolated MySQL sandbox.";
        }
    }
    private static string Token(int userId) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("phase3", "phase3",
        new[] { new Claim("negosuite_user_id", userId.ToString()) }, expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiHost.Key)), SecurityAlgorithms.HmacSha256)));

    [MySqlTheory]
    [InlineData("accounts", "3210", "account-categories")]
    [InlineData("account-categories", "3220", "accounts")]
    [InlineData("general-journals", "4310", "bills")]
    [InlineData("receiving-reports", "4405", "bills")]
    [InlineData("inventory-adjustments", "4410", "bills")]
    [InlineData("stock-transfers", "4420", "bills")]
    [InlineData("stock-issuances", "4430", "bills")]
    [InlineData("bills", "4210", "payments")]
    [InlineData("payments", "4240", "bills")]
    [InlineData("customers", "3110", "suppliers")]
    [InlineData("suppliers", "3120", "customers")]
    [InlineData("items", "3130", "customers")]
    [InlineData("item-categories", "3135", "items")]
    [InlineData("sales-invoices", "4110", "customers")]
    [InlineData("sales-receipts", "4120", "sales-invoices")]
    [InlineData("sales-invoice-payments", "4125", "sales-receipts")]
    public async Task Preferences_persist_with_authenticated_user_company_scope_versions_and_reset(string pageKey, string moduleId, string otherPage)
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_preferences_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString, enforceSingleWebSession: false);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            // Exercise the deployment SQL, not only EF's test schema, and verify idempotence.
            await db.Database.ExecuteSqlRawAsync("DROP TABLE user_page_preference");
            var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "001_user_page_preference.sql"));
            await db.Database.ExecuteSqlRawAsync(sql); await db.Database.ExecuteSqlRawAsync(sql);
            var company = new Config { CompanyName = "Preference fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other preference fixture", Uuid = Guid.NewGuid().ToString() };
            var role = new UserRole { Name = "Admin", IsAdmin = true };
            db.Configs.AddRange(company, other); db.UserRoles.Add(role); await db.SaveChangesAsync();
            User NewUser(string name) => new() { Name = name, Email = name + "@example.test", Username = name, Password = AuthController.CalculateSha256Hash("test-password"), ConfigId = company.Id, Status = true, UserRoleId = role.Id, UserUIConfig = "legacy unchanged" };
            User first = NewUser("first"), second = NewUser("second"), inactive = NewUser("inactive"), denied = NewUser("denied");
            inactive.Status = false; denied.UserRoleId = null;
            db.Users.AddRange(first, second, inactive, denied); db.AppVersions.Add(new AppVersion { Id = 1, VersionCode = "test" }); await db.SaveChangesAsync();
            var path = "/api/me/page-preferences/" + pageKey;
            var column = pageKey switch { "accounts" => "categoryName", "account-categories" => "type", "general-journals" or "receiving-reports" or "stock-transfers" or "stock-issuances" or "inventory-adjustments" => "referenceDate", "bills" => "billDate", "payments" => "referenceDate", "items" => "unit", "item-categories" => "status", "sales-invoices" => "dueDate", "sales-receipts" => "receiptDate", "sales-invoice-payments" => "referenceDate", _ => "address" };
            var secondColumn = pageKey switch { "accounts" => "parentAccountName", "account-categories" => "accountCount", "general-journals" or "receiving-reports" or "stock-transfers" or "stock-issuances" or "inventory-adjustments" => "notes", "items" => "cost", "bills" or "payments" or "sales-invoices" or "sales-receipts" or "sales-invoice-payments" => "balance", _ => "tin" };
            void As(int id, string companyUuid = null)
            {
                host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(id));
                host.Client.DefaultRequestHeaders.Remove("configUuid"); host.Client.DefaultRequestHeaders.Add("configUuid", companyUuid ?? company.Uuid);
            }
            var anonymous = await host.Client.GetAsync(path); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            As(first.Id);
            var initial = await host.Client.GetFromJsonAsync<PagePreferenceResponse>(path);
            Assert.Equal(0, initial.Version); Assert.Empty(initial.Columns);
            var put = await host.Client.PutAsJsonAsync(path, new { version = 0, columns = new Dictionary<string, bool> { [column] = true, [secondColumn] = false, ["creditLimit"] = false, ["name"] = false, ["removedColumn"] = true }, userId = second.Id, companyId = other.Id });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var saved = await put.Content.ReadFromJsonAsync<PagePreferenceResponse>();
            Assert.Equal(1, saved.Version); Assert.True(saved.Columns[column]);
            if (pageKey == "item-categories") Assert.False(saved.Columns.ContainsKey(secondColumn));
            else Assert.False(saved.Columns[secondColumn]);
            Assert.Equal(pageKey switch { "customers" => 3, "item-categories" => 1, _ => 2 }, saved.Columns.Count);
            Assert.Equal(pageKey == "customers", saved.Columns.ContainsKey("creditLimit"));
            Assert.Empty((await host.Client.GetFromJsonAsync<PagePreferenceResponse>("/api/me/page-preferences/" + otherPage)).Columns);
            Assert.Equal("legacy unchanged", await db.Users.AsNoTracking().Where(u => u.Id == first.Id).Select(u => u.UserUIConfig).SingleAsync());
            // A fresh host simulates another browser/server instance; preferences come from the database.
            using (var otherHost = new ApiHost(connection.ConnectionString, enforceSingleWebSession: false))
            {
                otherHost.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(first.Id));
                otherHost.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
                var restored = await otherHost.Client.GetFromJsonAsync<PagePreferenceResponse>(path);
                Assert.Equal(1, restored.Version); Assert.True(restored.Columns[column]);
            }
            Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync(path, new { version = 0, columns = new { address = false } })).StatusCode);
            As(second.Id); Assert.Empty((await host.Client.GetFromJsonAsync<PagePreferenceResponse>(path)).Columns);
            Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync(path, new { version = 1, columns = new { address = false } })).StatusCode);
            As(first.Id, other.Uuid); Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(path)).StatusCode);
            As(inactive.Id); Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(path)).StatusCode);
            As(denied.Id); Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(path)).StatusCode);
            As(first.Id); Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/me/page-preferences/unknown")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync(path, new { version = -1, columns = new { address = true } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync(path, new { version = 1, columns = new { address = "true" } })).StatusCode);
            var reset = await host.Client.PutAsJsonAsync(path, new { version = 1, columns = new Dictionary<string, bool>() });
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            Assert.Empty((await host.Client.GetFromJsonAsync<PagePreferenceResponse>(path)).Columns);
            Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync(path, new { version = 1, columns = new { address = true } })).StatusCode);
            // Same user moved to another company receives a separate preference record.
            first.ConfigId = other.Id; await db.SaveChangesAsync(); As(first.Id, other.Uuid);
            Assert.Empty((await host.Client.GetFromJsonAsync<PagePreferenceResponse>(path)).Columns);
            first.ConfigId = company.Id; await db.SaveChangesAsync(); As(first.Id);
            var regularRole = new UserRole { Name = "Page viewer", UserConfigId = company.Id, Permission = JsonSerializer.Serialize(new[] { new { moduleId, canView = true } }) };
            db.UserRoles.Add(regularRole); await db.SaveChangesAsync(); first.UserRoleId = regularRole.Id; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/me/page-preferences/" + otherPage)).StatusCode);
            // Legacy tokens must refresh to establish a signed identity. Caller-supplied IDs are never sufficient.
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(path)).StatusCode);
            var login = await host.Client.PostAsJsonAsync("/api/auth/sign-in", new { email = first.Email, password = "test-password", platform = "web" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var session = await login.Content.ReadFromJsonAsync<JsonElement>();
            void AssertIdentity(string token) => Assert.Equal(first.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "negosuite_user_id").Value);
            AssertIdentity(session.GetProperty("accessToken").GetString());
            var refresh = await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken = session.GetProperty("refreshToken").GetString() });
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            AssertIdentity((await refresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

}
