using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace negosuite_api.Services;

public sealed class AuthTokenService
{
    private readonly IConfiguration config;
    public AuthTokenService(IConfiguration config) => this.config = config;
    public string Access(int userId) => Write(new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
        new[] { new Claim("negosuite_user_id", userId.ToString(CultureInfo.InvariantCulture)) },
        expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: Credentials(config["Jwt:Key"])));
    public string Refresh() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public string Metabase(int companyId) => Write(new JwtSecurityToken(claims: new[]
    {
        new Claim("resource", JsonSerializer.Serialize(new { dashboard = 4 }), JsonClaimValueTypes.Json),
        new Claim("params", JsonSerializer.Serialize(new { config_id = new[] { companyId } }), JsonClaimValueTypes.Json)
    }, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: Credentials(config["Metabase:DashboardKey"])));
    private static SigningCredentials Credentials(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new AdministrationException("Token service is not configured.", 503);
        return new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
    }
    private static string Write(JwtSecurityToken token) => new JwtSecurityTokenHandler().WriteToken(token);
}
