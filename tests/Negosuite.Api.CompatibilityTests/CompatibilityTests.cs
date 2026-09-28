using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using negosuite_api;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public sealed class ApiHost : IDisposable
{
    public const string Key = "phase3-test-only-signing-key-at-least-32-bytes";
    public TestServer Server { get; }
    public HttpClient Client { get; }
    private readonly IHost host;
    public ApiHost(string connection = "server=127.0.0.1;port=1;database=unavailable;user=test;password=test")
    {
        host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Development")
            .ConfigureAppConfiguration((_, builder) => builder.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionString:negosuite"] = connection,
                ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "phase3", ["Jwt:Audience"] = "phase3"
            }))
            .UseStartup<Startup>()
            .ConfigureTestServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
                services.Configure<RequestLocalizationOptions>(options => options
                    .SetDefaultCulture("en-US").AddSupportedCultures("en-US", "fr-FR")
                    .AddSupportedUICultures("en-US", "fr-FR"));
            })).Start();
        Server = host.GetTestServer();
        Client = Server.CreateClient();
        Client.BaseAddress = new Uri("https://localhost");
    }
    public static string Token(string issuer = "phase3", string audience = "phase3", string key = Key, bool expired = false)
    {
        var token = new JwtSecurityToken(issuer, audience, new[] { new Claim("sub", "compatibility-test") },
            notBefore: DateTime.UtcNow.AddHours(-2), expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public void Dispose() { Client.Dispose(); host.Dispose(); }
}

// Test-only routes are loaded solely into TestServer, never into the shipped API.
[ApiController, Route("__compatibility")]
public class ProbeController : ControllerBase
{
    [HttpGet("secured"), Authorize]
    public object Secured() => new { Authenticated = User.Identity.IsAuthenticated };
    [HttpGet("config"), Authorize, TypeFilter(typeof(ConfigUuidFilter))]
    public object Config() => new { Accepted = true };
    [HttpGet("serialization")]
    public object Serialization() => new { Culture = CultureInfo.CurrentCulture.Name, Amount = 1234.5678m,
        Date = new DateTime(2026, 9, 28), Optional = (string)null };
}

public class CompatibilityTests
{
    [Theory]
    [InlineData("valid", 200)]
    [InlineData("expired", 401)]
    [InlineData("issuer", 401)]
    [InlineData("audience", 401)]
    [InlineData("signature", 401)]
    [InlineData("missing", 401)]
    public async Task Jwt_validation_preserves_access_boundary(string mode, int expected)
    {
        using var host = new ApiHost();
        if (mode != "missing") host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            ApiHost.Token(issuer: mode == "issuer" ? "wrong" : "phase3", audience: mode == "audience" ? "wrong" : "phase3",
                key: mode == "signature" ? ApiHost.Key + "wrong" : ApiHost.Key, expired: mode == "expired"));
        var response = await host.Client.GetAsync("/__compatibility/secured");
        Assert.Equal(expected, (int)response.StatusCode);
        if (expected == 401) Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
    }

    [Fact]
    public async Task Cors_preflight_is_handled_before_authentication()
    {
        using var host = new ApiHost();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/customers");
        request.Headers.Add("Origin", "https://frontend.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,configUuid,X-UserLog");
        var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Localization_runs_before_controller_and_json_is_culture_invariant()
    {
        using var host = new ApiHost();
        var response = await host.Client.GetFromJsonAsync<JsonElement>("/__compatibility/serialization?culture=fr-FR");
        Assert.Equal("fr-FR", response.GetProperty("culture").GetString());
        Assert.Equal(1234.5678m, response.GetProperty("amount").GetDecimal());
        Assert.Equal("2026-09-28T00:00:00", response.GetProperty("date").GetString());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("optional").ValueKind);
    }

    [Fact]
    public async Task Valid_jwt_does_not_bypass_missing_config_header()
    {
        using var host = new ApiHost();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token());
        var response = await host.Client.GetAsync("/__compatibility/config");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_refresh_token_retains_problem_details_shape()
    {
        using var host = new ApiHost();
        var response = await host.Client.PostAsJsonAsync("/api/auth/refresh-access-token", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", body.GetProperty("title").GetString());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public void MySql_model_and_parameterized_queries_compile_without_database_access()
    {
        using var host = new ApiHost();
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        Assert.NotEmpty(db.Model.GetEntityTypes());
        var uuid = "test-config";
        Assert.Contains("WHERE", db.Configs.Where(c => c.Uuid == uuid).ToQueryString());
        Assert.Contains("ORDER BY", db.UserLogs.Where(l => l.UserId == 1 && l.Platform == "web")
            .OrderByDescending(l => l.SignInDate).ToQueryString());
        Assert.Contains("CALL GetAccountRecap", db.AccountRecaps.FromSqlInterpolated($"CALL GetAccountRecap({1}, {"2026-09-28"})").ToQueryString());
    }
}
