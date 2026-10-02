using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class AdministrationTests
{
    private static void As(ApiHost host, int? id) => host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token(userId: id));
    private static void Company(ApiHost host, string uuid)
    { host.Client.DefaultRequestHeaders.Remove("configUuid"); host.Client.DefaultRequestHeaders.Add("configUuid", uuid); }
    private static string Criteria(string path, int company) => path + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(new { userConfigId = company }));

    private static async Task<(User Admin, User Member, UserRole AdminRole, UserRole MemberRole)> SeedAsync(BillPaymentTests.Fixture f)
    {
        var admin = await f.Db.Users.SingleAsync();
        var adminRole = new UserRole { UserConfigId = f.Company.Id, Name = "Admin", IsAdmin = true };
        var role = new UserRole { UserConfigId = f.Company.Id, Name = "Member %_", Permission = "[]" };
        f.Db.UserRoles.AddRange(adminRole, role); await f.Db.SaveChangesAsync(); admin.UserRoleId = adminRole.Id;
        var member = new User { ConfigId = f.Company.Id, UserRoleId = role.Id, Name = "Member", Email = "member@example.test", Password = "secret-hash", Status = true };
        f.Db.Users.Add(member); await f.Db.SaveChangesAsync();
        return (admin, member, adminRole, role);
    }

    [MySqlFact]
    public async Task Shared_filter_requires_signed_active_membership_and_handles_session_headers_safely()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var user = await f.Db.Users.SingleAsync();
        const string route = "/__compatibility/config";
        Assert.Equal(HttpStatusCode.OK, (await f.Host.Client.GetAsync(route)).StatusCode);
        As(f.Host, null); Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync(route)).StatusCode);
        As(f.Host, int.MaxValue); Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync(route)).StatusCode);
        As(f.Host, user.Id); Company(f.Host, f.Other.Uuid);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync(route)).StatusCode);
        Company(f.Host, f.Company.Uuid);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync(Criteria("/api/accounts", f.Other.Id))).StatusCode);
        user.Status = false; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync(route)).StatusCode);
        user.Status = true; await f.Db.SaveChangesAsync();
        foreach (var header in new[] { "{broken", "null", JsonSerializer.Serialize(new { id = 1, userId = int.MaxValue }) })
        {
            f.Host.Client.DefaultRequestHeaders.Remove("X-UserLog"); f.Host.Client.DefaultRequestHeaders.Add("X-UserLog", header);
            Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync(route)).StatusCode);
        }
        var log = new UserLog { UserId = user.Id, Platform = "web", SignInDate = DateTime.UtcNow };
        f.Db.UserLogs.Add(log); await f.Db.SaveChangesAsync();
        f.Host.Client.DefaultRequestHeaders.Remove("X-UserLog"); f.Host.Client.DefaultRequestHeaders.Add("X-UserLog", JsonSerializer.Serialize(new { id = log.Id, userId = user.Id }));
        Assert.Equal(HttpStatusCode.OK, (await f.Host.Client.GetAsync(route)).StatusCode);
        f.Db.UserLogs.Add(new UserLog { UserId = user.Id, Platform = "web", SignInDate = DateTime.UtcNow.AddSeconds(1) }); await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync(route)).StatusCode);
        using var parallel = new ApiHost(f.Db.Database.GetConnectionString(), enforceSingleWebSession: false);
        As(parallel, user.Id); Company(parallel, f.Company.Uuid); parallel.Client.DefaultRequestHeaders.Add("X-UserLog", "{broken");
        Assert.Equal(HttpStatusCode.OK, (await parallel.Client.GetAsync(route)).StatusCode);
        Company(parallel, f.Other.Uuid); Assert.Equal(HttpStatusCode.Forbidden, (await parallel.Client.GetAsync(route)).StatusCode);
    }

    [MySqlFact]
    public async Task Administration_lists_dtos_permissions_and_guarded_writes_preserve_legacy_calls()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var (admin, member, adminRole, role) = await SeedAsync(f);
        var shared = new UserRole { Name = "Shared system role", IsAdmin = true };
        var foreign = new UserRole { Name = "Foreign", UserConfigId = f.Other.Id };
        f.Db.UserRoles.AddRange(shared, foreign); await f.Db.SaveChangesAsync();
        var users = await f.Host.Client.GetFromJsonAsync<JsonElement>("/api/users");
        Assert.Equal(2, users.GetArrayLength()); Assert.False(users[0].TryGetProperty("password", out _));
        Assert.Equal(12, users[0].EnumerateObject().Count());
        foreach (var path in new[] { "/api/users", Criteria("/api/users/config", f.Company.Id), Criteria("/api/user-roles", f.Company.Id), Criteria("/api/users/roles", f.Company.Id), "/api/configs" })
        {
            var join = path.Contains('?') ? "&" : "?";
            var list = await f.Host.Client.GetFromJsonAsync<JsonElement>(path); Assert.Equal(JsonValueKind.Array, list.ValueKind);
            var page = await f.Host.Client.GetFromJsonAsync<JsonElement>(path + join + "pageNumber=1&pageSize=1");
            Assert.Equal(list.GetArrayLength(), page.GetProperty("totalCount").GetInt32()); Assert.Single(page.GetProperty("items").EnumerateArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(path + join + "sortBy=invalid")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(path + join + "pageNumber=1")).StatusCode);
            var sorts = path == "/api/configs" ? ConfigService.SortFields.Keys
                : path.Contains("roles") ? UserRoleService.SortFields.Keys : UserService.SortFields.Keys;
            foreach (var sort in sorts)
                foreach (var direction in new[] { "asc", "desc" })
                {
                    var sorted = await f.Host.Client.GetAsync(path + join + $"sortBy={sort}&sortDirection={direction}&pageNumber=1&pageSize=1");
                    Assert.True(sorted.StatusCode == HttpStatusCode.OK, await sorted.Content.ReadAsStringAsync());
                }
        }
        Assert.Single((await f.Host.Client.GetFromJsonAsync<JsonElement>(Criteria("/api/user-roles", f.Company.Id) + "&search=" + Uri.EscapeDataString("%_"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"/api/user-roles/{foreign.Id}")).StatusCode);
        var sharedDetail = await f.Host.Client.GetFromJsonAsync<JsonObject>($"/api/user-roles/{shared.Id}"); sharedDetail["userConfigId"] = f.Company.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"/api/user-roles/{shared.Id}", sharedDetail)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.DeleteAsync($"/api/user-roles/{shared.Id}")).StatusCode);
        var adminDetail = await f.Host.Client.GetFromJsonAsync<JsonObject>($"/api/user-roles/{adminRole.Id}"); adminDetail["isAdmin"] = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"/api/user-roles/{adminRole.Id}", adminDetail)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsync($"/api/users/update-role?id={admin.Id}&userRoleId={role.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsync($"/api/users/update-role?id={member.Id}&userRoleId={foreign.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"/api/users/{admin.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"/api/user-roles/{role.Id}")).StatusCode);

        var config = await f.Host.Client.GetFromJsonAsync<JsonObject>($"/api/configs/{f.Company.Id}");
        var uuid = config["uuid"].GetValue<string>(); config["companyName"] = "Updated company";
        config["uuid"] = "forged"; config["maxUserCount"] = 255; config["isTemplate"] = true; config["trialEndDate"] = "2099-01-01";
        config["apTradeAccount"]["name"] = "Overposted";
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"/api/configs/{f.Company.Id}", config)).StatusCode);
        var after = await f.Host.Client.GetFromJsonAsync<JsonObject>($"/api/configs/{f.Company.Id}");
        Assert.Equal(uuid, after["uuid"].GetValue<string>()); Assert.Null(after["maxUserCount"]); Assert.Null(after["isTemplate"]); Assert.Null(after["trialEndDate"]);
        Assert.Equal(f.Account.Name, after["apTradeAccount"]["name"].GetValue<string>());
        config["arTradeAccountId"] = f.ForeignAccount.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"/api/configs/{f.Company.Id}", config)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"/api/configs/{f.Other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"/api/configs/{f.Company.Id}")).StatusCode);

        As(f.Host, member.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync($"/api/users/{admin.Id}")).StatusCode);
        var profile = await f.Host.Client.GetFromJsonAsync<JsonObject>($"/api/users/{member.Id}"); Assert.False(profile.ContainsKey("password"));
        profile["name"] = "Self updated";
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"/api/users/{member.Id}", profile)).StatusCode);
        profile["userRoleId"] = shared.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"/api/users/{member.Id}", profile)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"/api/users/update-ui-config?id={admin.Id}", new { userUIConfigString = "forged" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"/api/users/update-ui-config?id={member.Id}", new { userUIConfigString = "mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"/api/configs/{f.Company.Id}", after)).StatusCode);
        As(f.Host, admin.Id);
        var create = new UserRoleCreateRequest { UserConfigId = f.Company.Id, Name = "New", Permission = "[]" };
        var response = await f.Host.Client.PostAsJsonAsync("/api/user-roles", create); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserRoleDetailDto>(); Assert.Equal(admin.Id, created.CreatedByUserId);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"/api/user-roles/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"/api/users/{member.Id}")).StatusCode);
        As(f.Host, member.Id); Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync($"/api/users/{member.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task Registration_uses_stored_invitation_grants_enforces_expiry_and_consumes_tokens_once()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var (_, _, _, role) = await SeedAsync(f);
        f.Host.Client.DefaultRequestHeaders.Authorization = null;
        var log = new EmailLog { Uuid = Guid.NewGuid().ToString(), Email = "invite@example.test", ConfigId = f.Company.Id,
            Action = "member-invite", Status = 1, CreatedDate = DateTime.Now, ExpiryDate = DateTime.Now.AddDays(1),
            Data = JsonSerializer.Serialize(new { configId = f.Company.Id, userRoleId = role.Id }) };
        f.Db.EmailLogs.Add(log); await f.Db.SaveChangesAsync();
        var input = new UserCreateRequest { Identifier = log.Uuid, Name = "Invited", Email = log.Email, Password = "secret", ConfigId = f.Other.Id, UserRoleId = role.Id };
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/users", input)).StatusCode);
        input.ConfigId = f.Company.Id;
        var created = await f.Host.Client.PostAsJsonAsync("/api/users", input); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonObject>(); Assert.False(body.ContainsKey("password"));
        Assert.Equal(role.Id, body["userRoleId"].GetValue<int>());
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/users", input)).StatusCode);
        var activation = new EmailLog { Uuid = Guid.NewGuid().ToString(), Email = "new@example.test", Action = "new-account", Status = 1,
            CreatedDate = DateTime.Now, ExpiryDate = DateTime.Now.AddDays(1), Data = JsonSerializer.Serialize(new { password = new string('a', 64) }) };
        f.Db.EmailLogs.Add(activation); await f.Db.SaveChangesAsync();
        input.Identifier = activation.Uuid; input.Email = activation.Email; input.ConfigId = null; input.UserRoleId = null; input.Password = "client-forged";
        var activated = await f.Host.Client.PostAsJsonAsync("/api/users/new-account", input); Assert.Equal(HttpStatusCode.Created, activated.StatusCode);
        Assert.Equal(new string('a', 64), await f.Db.Users.Where(u => u.Email == activation.Email).Select(u => u.Password).SingleAsync());
        Assert.Null((await activated.Content.ReadFromJsonAsync<JsonObject>())["configId"]);
        var expired = new EmailLog { Uuid = Guid.NewGuid().ToString(), Email = "expired@example.test", Action = "new-account", Status = 1,
            CreatedDate = DateTime.Now, ExpiryDate = DateTime.Now.AddDays(-1), Data = activation.Data };
        f.Db.EmailLogs.Add(expired); await f.Db.SaveChangesAsync(); input.Identifier = expired.Uuid; input.Email = expired.Email;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/users/new-account", input)).StatusCode);
        Assert.Equal(1, await f.Db.EmailLogs.AsNoTracking().Where(e => e.Id == expired.Id).Select(e => e.Status).SingleAsync());
    }

    [PagePreferenceTests.MySqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Company_setup_keeps_procedure_contract_and_binds_only_the_unconfigured_caller(bool templateRoute)
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var user = await f.Db.Users.SingleAsync(); user.ConfigId = null;
        var sharedAdmin = new UserRole { Name = "System administrator", IsAdmin = true };
        var template = new Config { CompanyName = "Setup template", IsTemplate = true, Uuid = Guid.NewGuid().ToString() };
        f.Db.UserRoles.Add(sharedAdmin); f.Db.Configs.Add(template); await f.Db.SaveChangesAsync();
        f.Host.Client.DefaultRequestHeaders.Remove("configUuid");
        // Stub only the routine's integration contract. Production routine bodies are not asserted here.
        var routine = templateRoute ? "CreateUserConfigFromTemplate" : "CreateUserConfig";
        var routineSql = $@"CREATE PROCEDURE {routine}(
            IN p_user VARCHAR(10), IN p_role VARCHAR(10), IN p_template VARCHAR(10), IN p_name VARCHAR(150),
            IN p_address VARCHAR(150), IN p_phone VARCHAR(50), IN p_email VARCHAR(150), IN p_website VARCHAR(150),
            IN p_tin VARCHAR(50), IN p_industry VARCHAR(10), IN p_about TEXT, IN p_country VARCHAR(10), IN p_taxes LONGTEXT,
            IN p_plan VARCHAR(10), IN p_subscription VARCHAR(10), IN p_trial VARCHAR(10), IN p_trialend VARCHAR(10),
            IN p_billing VARCHAR(10), IN p_limit VARCHAR(10))
            BEGIN
              DECLARE new_id INT;
              INSERT INTO config (CompanyName, Uuid, Trial, TrialEndDate, MaxUserCount,
                ARAgingBaseDate, ARAgingShowCurrent, ARAgingPeriod1, ARAgingPeriod2, ARAgingPeriod3, ARAgingPeriod4,
                APAgingBaseDate, APAgingShowCurrent, APAgingPeriod1, APAgingPeriod2, APAgingPeriod3, APAgingPeriod4,
                ShowAccountCodeInList, RequireAccountCode)
              VALUES (p_name, UUID(), p_trial, p_trialend, p_limit, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
              SET new_id = LAST_INSERT_ID();
              UPDATE user SET ConfigId = new_id, UserRoleId = p_role WHERE Id = p_user;
              SELECT * FROM config WHERE Id = new_id;
            END";
        await f.Db.Database.ExecuteSqlRawAsync(routineSql);
        var path = "/api/users/" + (templateRoute ? "update-config-template" : "update-config") + "?id=";
        var request = new { id = template.Id, companyName = "New company", trial = true, maxUserCount = 255, trialEndDate = "2099-01-01", uuid = "forged" };
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync(path + int.MaxValue, request)).StatusCode);
        var response = await f.Host.Client.PutAsJsonAsync(path + user.Id, request);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(user.Id, body["id"].GetValue<int>()); Assert.Equal(1, body["config"]["maxUserCount"].GetValue<int>());
        Assert.NotEqual("forged", body["config"]["uuid"].GetValue<string>());
        Assert.Equal(sharedAdmin.Id, body["userRoleId"].GetValue<int>());
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync(path + user.Id, request)).StatusCode);
    }

    [Fact]
    public async Task Openapi_uses_safe_administration_contracts_without_password_or_privilege_overposting()
    {
        using var host = new ApiHost();
        using var doc = JsonDocument.Parse(await host.Client.GetStringAsync("/swagger/v1/swagger.json"));
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.False(schemas.GetProperty("UserDetailDto").GetProperty("properties").TryGetProperty("password", out _));
        var writes = schemas.GetProperty("ConfigUpdateRequest").GetProperty("properties");
        foreach (var name in new[] { "uuid", "isTemplate", "maxUserCount", "trialEndDate", "subscriptionPlanId", "arTradeAccount" })
            Assert.False(writes.TryGetProperty(name, out _));
        Assert.False(schemas.GetProperty("UserUpdateRequest").GetProperty("properties").TryGetProperty("configId", out _));
    }
}
