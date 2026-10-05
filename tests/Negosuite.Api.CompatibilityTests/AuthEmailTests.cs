using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class AuthEmailTests
{
    private sealed class MailSink : IEmailService
    {
        public readonly List<OutgoingEmail> Messages = new();
        public bool Fail;
        public Func<Task> BeforeSend;
        public async Task SendAsync(OutgoingEmail email, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (BeforeSend != null) await BeforeSend();
            if (Fail) throw new InvalidOperationException("private SMTP diagnostic");
            Messages.Add(email);
        }
    }
    private static ApiHost Host(BillPaymentTests.Fixture f, MailSink sink, bool notify = false)
    {
        var host = new ApiHost(f.Db.Database.GetConnectionString(), enforceSingleWebSession: false,
            configureServices: services => services.AddSingleton<IEmailService>(sink),
            settings: new Dictionary<string, string> { ["AppUrl"] = "https://client.example.test/app/", ["NotificationRecipients"] = notify ? "ops@example.test" : "",
                ["Metabase:DashboardKey"] = "test-only-metabase-signing-key-at-least-32-characters" });
        return host;
    }
    private static async Task<User> Actor(BillPaymentTests.Fixture f, ApiHost host, bool admin = false)
    {
        var user = await f.Db.Users.SingleAsync();
        user.Name = "<b>Actor</b>"; user.Email = "actor@example.test"; user.Password = PasswordSecurity.Hash("Original123!");
        if (admin)
        {
            var role = new UserRole { Name = "Admin", IsAdmin = true, UserConfigId = f.Company.Id };
            f.Db.UserRoles.Add(role); await f.Db.SaveChangesAsync(); user.UserRoleId = role.Id;
        }
        await f.Db.SaveChangesAsync();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token(userId: user.Id));
        return user;
    }
    private static async Task Ok(HttpResponseMessage response) => Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    [MySqlFact]
    public async Task Sign_in_refresh_preserve_shape_claims_session_policy_and_handle_missing_version()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); using var host = Host(f, new()); var user = await Actor(f, host);
        async Task<JsonObject> Login()
        {
            var response = await host.Client.PostAsJsonAsync("/api/auth/sign-in", new { email = user.Email.ToUpperInvariant(), password = "Original123!", platform = "web" });
            await Ok(response); return await response.Content.ReadFromJsonAsync<JsonObject>();
        }
        var first = await Login(); var second = await Login();
        Assert.Equal(6, first.Count); Assert.Equal(15, first["user"].AsObject().Count); Assert.False(first["user"].AsObject().ContainsKey("password"));
        Assert.Equal(user.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(first["accessToken"].GetValue<string>()).Claims.Single(c => c.Type == "negosuite_user_id").Value);
        Assert.Equal(32, Convert.FromBase64String(first["refreshToken"].GetValue<string>()).Length);
        Assert.Null(first["appVersion"]["latestVersion"]); Assert.Equal(1, first["appVersion"]["updateMode"].GetValue<int>());
        host.Client.DefaultRequestHeaders.Add("configUuid", f.Company.Uuid);
        host.Client.DefaultRequestHeaders.Add("X-UserLog", first["userLog"].ToJsonString());
        await Ok(await host.Client.GetAsync("/__compatibility/config"));
        var refresh = await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken = first["refreshToken"].GetValue<string>() });
        await Ok(refresh); var refreshed = await refresh.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(5, refreshed.Count); Assert.False(refreshed.ContainsKey("userLog")); Assert.Equal(first["refreshToken"].GetValue<string>(), refreshed["refreshToken"].GetValue<string>());
        foreach (var payload in new object[] { new { password = "Original123!" }, new { email = user.Email }, new { email = user.Email, password = "x", platformVersion = new string('x', 21) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/sign-in", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.PostAsJsonAsync("/api/auth/sign-in", new { email = user.Email, password = "bad" })).StatusCode);
        var log = await f.Db.UserLogs.SingleAsync(l => l.RefreshToken == first["refreshToken"].GetValue<string>());
        log.ExpiryDate = null; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken = log.RefreshToken })).StatusCode);
        await Ok(await host.Client.PostAsJsonAsync("/api/auth/change-password", new { email = user.Email, password = "Original123!", newPassword = "Changed123!" }));
        Assert.All(await f.Db.UserLogs.AsNoTracking().ToListAsync(), l => Assert.True(l.IsRevoked));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken = second["refreshToken"].GetValue<string>() })).StatusCode);
    }

    [MySqlFact]
    public async Task Reset_requires_server_issued_matching_unexpired_token_and_consumes_it_once()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var sink = new MailSink(); using var host = Host(f, sink); var user = await Actor(f, host);
        host.Client.DefaultRequestHeaders.Authorization = null;
        var guessed = Guid.NewGuid().ToString();
        sink.BeforeSend = async () => Assert.Equal(2, (await f.Db.EmailLogs.AsNoTracking().SingleAsync()).Status);
        await Ok(await host.Client.PostAsJsonAsync("/api/email/password-reset", new { recipients = new[] { user.Email }, identifier = guessed, expiryDate = DateTime.Now.AddYears(20) }));
        var log = await f.Db.EmailLogs.AsNoTracking().SingleAsync(); Assert.NotEqual(guessed, log.Uuid);
        Assert.InRange((log.ExpiryDate.Value - DateTime.Now).TotalMinutes, 119, 121);
        Assert.Single(sink.Messages); Assert.Single(sink.Messages[0].Recipients); Assert.Contains("reset-password?id=" + log.Uuid, sink.Messages[0].HtmlBody);
        Assert.DoesNotContain("<b>Actor</b>", sink.Messages[0].HtmlBody); Assert.Contains("&lt;b&gt;Actor&lt;/b&gt;", sink.Messages[0].HtmlBody);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, password = "Changed123!" })).StatusCode);
        async Task<HttpResponseMessage> Reset(string identifier, string email) => await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { identifier, email, password = "Changed123!" });
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(guessed, user.Email)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(log.Uuid, "other@example.test")).StatusCode);
        f.Db.UserLogs.Add(new UserLog { UserId = user.Id, Email = user.Email, RefreshToken = "reset-session", ExpiryDate = DateTime.UtcNow.AddDays(1) }); await f.Db.SaveChangesAsync();
        // Concurrent attempts using the same capability must have exactly one winner.
        var responses = await Task.WhenAll(Reset(log.Uuid, user.Email), Reset(log.Uuid, user.Email));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(0, await f.Db.EmailLogs.Where(l => l.Id == log.Id).Select(l => l.Status).SingleAsync());
        Assert.Equal(PasswordSecurity.Hash("Changed123!"), await f.Db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.Password).SingleAsync());
        Assert.True(await f.Db.UserLogs.Where(l => l.UserId == user.Id).Select(l => l.IsRevoked).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/email/log/" + log.Uuid)).StatusCode);
        foreach (var (action, expiry) in new[] { ("new-account", DateTime.Now.AddHours(1)), ("password-reset", DateTime.Now.AddMinutes(-1)) })
        {
            var invalid = new EmailLog { Uuid = Guid.NewGuid().ToString(), Email = user.Email, Status = 1, CreatedDate = DateTime.Now, ExpiryDate = expiry, Action = action };
            f.Db.EmailLogs.Add(invalid); await f.Db.SaveChangesAsync(); Assert.Equal(HttpStatusCode.BadRequest, (await Reset(invalid.Uuid, user.Email)).StatusCode);
        }
    }

    [MySqlFact]
    public async Task Confirmation_redacts_hash_keeps_activation_working_and_sends_separate_safe_notification()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var sink = new MailSink(); using var host = Host(f, sink, notify: true);
        var guessed = Guid.NewGuid().ToString(); const string email = "new@example.test";
        await Ok(await host.Client.PostAsJsonAsync("/api/email/email-confirmation", new { recipients = new[] { email }, recipientName = "<script>new</script>", password = "Original123!",
            identifier = guessed, configId = f.Other.Id, userRoleId = 99999, expiryDate = DateTime.Now.AddYears(50) }));
        var log = await f.Db.EmailLogs.AsNoTracking().SingleAsync(); Assert.NotEqual(guessed, log.Uuid); Assert.Null(log.ConfigId);
        Assert.InRange((log.ExpiryDate.Value - DateTime.Now).TotalHours, 23.9, 24.1);
        Assert.Equal(2, sink.Messages.Count); Assert.Single(sink.Messages[0].Recipients); Assert.Equal(email, sink.Messages[0].Recipients[0]);
        Assert.DoesNotContain(log.Uuid, sink.Messages[1].HtmlBody); Assert.DoesNotContain(PasswordSecurity.Hash("Original123!"), sink.Messages[1].HtmlBody);
        Assert.Contains("&lt;script&gt;new&lt;/script&gt;", sink.Messages[0].HtmlBody);
        var response = await host.Client.GetFromJsonAsync<JsonObject>("/api/email/log/" + log.Uuid); var data = JsonNode.Parse(response["data"].GetValue<string>()).AsObject();
        Assert.False(data.ContainsKey("password")); Assert.False(data.ContainsKey("message")); Assert.Null(data["userRoleId"]);
        var activated = await host.Client.PostAsJsonAsync("/api/users/new-account", new { name = "New", email, identifier = log.Uuid });
        Assert.True(activated.StatusCode == HttpStatusCode.Created, await activated.Content.ReadAsStringAsync());
        Assert.Equal(PasswordSecurity.Hash("Original123!"), await f.Db.Users.Where(u => u.Email == email).Select(u => u.Password).SingleAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/users/new-account", new { name = "New", email, identifier = log.Uuid })).StatusCode);
    }

    [MySqlFact]
    public async Task Invitation_binds_company_role_sender_and_retains_acceptance_contract()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var sink = new MailSink(); using var host = Host(f, sink); var user = await Actor(f, host, admin: true);
        var request = new JsonObject { ["recipients"] = new JsonArray("invited@example.test"), ["recipientName"] = "Invitee", ["configId"] = f.Other.Id,
            ["senderName"] = "Forged", ["companyName"] = "Forged", ["expiryDate"] = "2099-01-01", ["identifier"] = Guid.NewGuid().ToString() };
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/api/email/member-invite", request)).StatusCode);
        request["configId"] = f.Company.Id; request["userRoleId"] = int.MaxValue;
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/email/member-invite", request)).StatusCode);
        request["userRoleId"] = user.UserRoleId; await Ok(await host.Client.PostAsJsonAsync("/api/email/member-invite", request));
        var log = await f.Db.EmailLogs.AsNoTracking().SingleAsync(); var payload = JsonNode.Parse(log.Data);
        Assert.Equal(f.Company.Id, log.ConfigId); Assert.Equal(user.Name, payload["senderName"].GetValue<string>()); Assert.Equal(f.Company.CompanyName, payload["companyName"].GetValue<string>());
        Assert.InRange((log.ExpiryDate.Value - DateTime.Now).TotalDays, 6.99, 7.01);
        var accepted = await host.Client.PostAsJsonAsync("/api/users", new { name = "Invited", email = log.Email, identifier = log.Uuid, configId = f.Company.Id, userRoleId = user.UserRoleId, password = "Accepted123!" });
        Assert.True(accepted.StatusCode == HttpStatusCode.Created, await accepted.Content.ReadAsStringAsync());
        user.UserRoleId = null; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PostAsJsonAsync("/api/email/member-invite", request)).StatusCode);
    }

    [MySqlFact]
    public async Task Invalid_recipients_never_send_and_failed_delivery_never_activates_a_link()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); var sink = new MailSink(); using var host = Host(f, sink); var user = await Actor(f, host);
        foreach (var recipients in new string[][] { null, Array.Empty<string>(), new[] { "bad" }, new[] { user.Email, "extra@example.test" }, new[] { "victim@example.test\r\nBcc:extra@example.test" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/email/password-reset", new { recipients })).StatusCode);
        Assert.Empty(sink.Messages); Assert.False(await f.Db.EmailLogs.AnyAsync());
        sink.Fail = true;
        var failure = await host.Client.PostAsJsonAsync("/api/email/password-reset", new { recipients = new[] { user.Email } });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode); Assert.DoesNotContain("private SMTP diagnostic", await failure.Content.ReadAsStringAsync());
        var failed = await f.Db.EmailLogs.AsNoTracking().SingleAsync(); Assert.Equal(0, failed.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/api/email/log/" + failed.Uuid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PostAsJsonAsync("/api/auth/reset-password", new { email = user.Email, password = "Changed123!", identifier = failed.Uuid })).StatusCode);
        sink.Fail = false;
        await Ok(await host.Client.PostAsJsonAsync("/api/email", new { recipients = new[] { "one@example.test", "ONE@example.test" }, subject = "Test", message = "<p>Body</p>" }));
        Assert.Single(sink.Messages); Assert.Single(sink.Messages[0].Recipients);
        host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/email", new { recipients = new[] { "one@example.test" }, subject = "Test", message = "Body" })).StatusCode);
    }

    [MySqlFact]
    public async Task Metabase_claims_use_only_the_authenticated_company()
    {
        await using var f = await BillPaymentTests.Fixture.Start(); using var host = Host(f, new()); var user = await Actor(f, host);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/auth/metabase-token?userConfigId=" + f.Other.Id)).StatusCode);
        var response = await host.Client.GetFromJsonAsync<JsonObject>("/api/auth/metabase-token?userConfigId=" + f.Company.Id);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response["metabaseToken"].GetValue<string>());
        Assert.Equal(4, JsonNode.Parse(token.Claims.Single(c => c.Type == "resource").Value)["dashboard"].GetValue<int>());
        Assert.Equal(f.Company.Id, JsonNode.Parse(token.Claims.Single(c => c.Type == "params").Value)["config_id"][0].GetValue<int>());
        user.Status = false; await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/auth/metabase-token?userConfigId=" + f.Company.Id)).StatusCode);
    }
}
