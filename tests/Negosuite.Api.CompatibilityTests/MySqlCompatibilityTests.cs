using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL")))
            Skip = "Run scripts/Test-Phase3MySql.ps1 to use the isolated native MySQL sandbox.";
    }
}

public class MySqlCompatibilityTests
{
    [MySqlFact]
    public async Task Synthetic_mysql_fixture_exercises_signin_refresh_queries_and_session_filter()
    {
        var builder = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        // This test cannot target the application's configured database.
        Assert.Equal("127.0.0.1", builder.Server);
        Assert.Equal(33316u, builder.Port);
        var database = "negosuite_phase3_" + Guid.NewGuid().ToString("N");
        builder.Database = database;
        using var host = new ApiHost(builder.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var config = new Config { CompanyName = "Phase 3 synthetic tenant", Uuid = Guid.NewGuid().ToString() };
            db.Configs.Add(config);
            var otherConfig = new Config { CompanyName = "Second synthetic tenant", Uuid = Guid.NewGuid().ToString() };
            db.Configs.Add(otherConfig);
            db.AppVersions.Add(new AppVersion { Id = 1, VersionCode = "test", AndroidUpdateUrl = "https://example.invalid/test" });
            db.Currencies.Add(new Currency { Code = "PHP", Name = "Peso", ExchangeRate = 1, IsBase = true });
            await db.SaveChangesAsync();
            var user = new User { Username = "phase3", Email = "PHASE3@EXAMPLE.INVALID", Name = "Synthetic User", Status = true,
                ConfigId = config.Id, Password = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-password"))).ToLowerInvariant() };
            db.Users.Add(user);
            var created = new DateTime(2026, 9, 28, 12, 34, 56).AddTicks(1234560);
            db.Customers.Add(new Customer { UserConfigId = config.Id, Name = "Synthetic Customer", CreditLimit = 1234.5678m, Status = true, CreatedDate = created });
            db.Customers.Add(new Customer { UserConfigId = otherConfig.Id, Name = "Other Tenant Customer", Status = true });
            await db.SaveChangesAsync();
            Assert.Equal(created, await db.Customers.AsNoTracking().Where(c => c.UserConfigId == config.Id).Select(c => c.CreatedDate).SingleAsync());

            var invalid = await host.Client.PostAsJsonAsync("/api/auth/sign-in", new { userName = "phase3", password = "wrong", platform = "web" });
            Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
            var login = await host.Client.PostAsJsonAsync("/api/auth/sign-in", new { userName = "phase3", password = "test-password", platform = "web" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var session = await login.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("bearer", session.GetProperty("tokenType").GetString());
            var token = session.GetProperty("accessToken").GetString();
            var refreshToken = session.GetProperty("refreshToken").GetString();
            var logId = session.GetProperty("userLog").GetProperty("id").GetInt32();
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/__compatibility/secured")).StatusCode);

            host.Client.DefaultRequestHeaders.Add("configUuid", config.Uuid);
            host.Client.DefaultRequestHeaders.Add("X-UserLog", JsonSerializer.Serialize(new { Id = logId, UserId = user.Id }));
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/__compatibility/config")).StatusCode);
            var criteria = Uri.EscapeDataString(JsonSerializer.Serialize(new { UserConfigId = config.Id }));
            var customers = await host.Client.GetAsync("/api/customers?criteria=" + criteria);
            Assert.Equal(HttpStatusCode.OK, customers.StatusCode);
            var rows = await customers.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, rows.GetArrayLength());
            Assert.Equal(1234.5678m, rows[0].GetProperty("creditLimit").GetDecimal());

            var refreshed = await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken });
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
            var refreshedBody = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(refreshToken, refreshedBody.GetProperty("refreshToken").GetString());
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedBody.GetProperty("accessToken").GetString());
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/__compatibility/secured")).StatusCode);

            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken = "unknown" })).StatusCode);
            var log = await db.UserLogs.SingleAsync(l => l.Id == logId);
            log.IsRevoked = true;
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken })).StatusCode);
            log.IsRevoked = false;
            log.ExpiryDate = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { refreshToken })).StatusCode);

            db.UserLogs.Add(new UserLog { UserId = user.Id, Platform = "web", SignInDate = DateTime.UtcNow.AddMinutes(1) });
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/__compatibility/config")).StatusCode);
            // Local migration testing can keep an older web login active without bypassing authentication or company validation.
            using (var parallelHost = new ApiHost(builder.ConnectionString, enforceSingleWebSession: false))
            {
                parallelHost.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
                parallelHost.Client.DefaultRequestHeaders.Add("configUuid", config.Uuid);
                parallelHost.Client.DefaultRequestHeaders.Add("X-UserLog", JsonSerializer.Serialize(new { Id = logId, UserId = user.Id }));
                Assert.Equal(HttpStatusCode.OK, (await parallelHost.Client.GetAsync("/__compatibility/config")).StatusCode);
                parallelHost.Client.DefaultRequestHeaders.Remove("configUuid");
                parallelHost.Client.DefaultRequestHeaders.Add("configUuid", "unknown");
                Assert.Equal(HttpStatusCode.Unauthorized, (await parallelHost.Client.GetAsync("/__compatibility/config")).StatusCode);
                parallelHost.Client.DefaultRequestHeaders.Authorization = null;
                Assert.Equal(HttpStatusCode.Unauthorized, (await parallelHost.Client.GetAsync("/__compatibility/secured")).StatusCode);
            }
            host.Client.DefaultRequestHeaders.Remove("configUuid");
            host.Client.DefaultRequestHeaders.Add("configUuid", "unknown");
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/__compatibility/config")).StatusCode);
        }
        finally
        {
            // Only the freshly generated schema on the dedicated loopback instance is removed.
            await db.Database.EnsureDeletedAsync();
        }
    }
}
