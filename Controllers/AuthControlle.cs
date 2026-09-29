using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Security.Claims;

namespace negosuite_api.Controllers
{
    [ApiController]
    public class AuthController : ControllerBase
    {
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;

        private readonly negosuiteContext _context;
        private IConfiguration _config { get; }

        public AuthController(negosuiteContext context, IConfiguration configuration)
        {
            _context = context;
            _config = configuration;
        }

        [AllowAnonymous]
        [Route("api/auth/sign-in")]
        [HttpPost]
        public async Task<ActionResult> PostSignIn(Credentials credentials)
        {
            var baseCurrency = await _context.Currencies.Where(c => c.IsBase).FirstOrDefaultAsync();
            var hash = CalculateSha256Hash(credentials.password);

            var user = await _context.Users
                .Where(u => (u.Status == STATUS_ACTIVE) && (!String.IsNullOrEmpty(credentials.userName) ? u.Username == credentials.userName : u.Email == credentials.email.ToUpper()) && u.Password == CalculateSha256Hash(credentials.password))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Name,
                    u.Email,
                    u.MobileNo,
                    u.UserTypeId,
                    UserTypeName = u.UserType.Name,
                    u.Avatar,
                    u.Status,
                    u.ConfigId,
                    u.Config,
                    BaseCurrency = baseCurrency,
                    u.UserRoleId,
                    u.UserRole,
                    u.UserUIConfig
                }).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound("Invalid email or password.");
            }

            var accessToken = GenerateJSONWebToken(user.Id);
            var refreshToken = GenerateRefreshToken();

            UserLog userLog = new UserLog();
            userLog.UserId = user.Id;
            userLog.Email = user.Email;
            userLog.Name = user.Name;
            userLog.SignInDate = DateTime.UtcNow;
            userLog.AccessToken = accessToken;
            userLog.Platform = credentials.platform;
            userLog.RefreshToken = refreshToken;
            userLog.ExpiryDate = DateTime.UtcNow.AddDays(2);
            userLog.PlatformVersion = credentials.platformVersion;

            _context.UserLogs.Add(userLog);
            await _context.SaveChangesAsync();

            var appVersion = await _context.AppVersions.FindAsync(1);

            var ret = new
            {
                User = user,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "bearer",
                UserLog = userLog,
                AppVersion = new
                {
                    LatestVersion = appVersion.VersionCode,
                    AndroidUpdateUrl = appVersion.AndroidUpdateUrl,
                    UpdateMode = 1
                }
            };

            return Ok(ret);
        }


        // [Authorize]
        [AllowAnonymous]
        [Route("api/auth/refresh-access-token")]
        [HttpPost]
        public async Task<ActionResult> PostRefreshToken(Credentials credentials)
        {
            if (credentials?.refreshToken == null) return BadRequest();
            // var refreshToken = credentials.refreshToken;

            var userLog = await _context.UserLogs.Where(e => e.RefreshToken == credentials.refreshToken).FirstOrDefaultAsync();

            if (userLog == null || userLog.IsRevoked == true || userLog.ExpiryDate < DateTime.UtcNow)
                return Unauthorized("Invalid or expired refresh token");

            var baseCurrency = await _context.Currencies.Where(c => c.IsBase).FirstOrDefaultAsync();
            var user = await _context.Users
                .Where(u => u.Id == userLog.UserId)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Name,
                    u.Email,
                    u.MobileNo,
                    u.UserTypeId,
                    UserTypeName = u.UserType.Name,
                    u.Avatar,
                    u.Status,
                    u.ConfigId,
                    u.Config,
                    BaseCurrency = baseCurrency,
                    u.UserRoleId,
                    u.UserRole,
                    u.UserUIConfig
                }).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound("Invalid email or password.");
            }

            var accessToken = GenerateJSONWebToken(user.Id);
            var refreshToken = credentials.refreshToken; // GenerateJSONWebToken(1440); // expiry = 2 days
            var appVersion = await _context.AppVersions.FindAsync(1);

            var ret = new
            {
                User = user,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "bearer",
                AppVersion = new
                {
                    LatestVersion = appVersion.VersionCode,
                    AndroidUpdateUrl = appVersion.AndroidUpdateUrl,
                    UpdateMode = 1
                }
            };

            return Ok(ret);
        }


        [Authorize]
        [Route("api/auth/change-password")]
        [HttpPost]
        public async Task<ActionResult> PostChangePassword(Credentials credentials)
        {
            var user = await _context.Users
                .Where(u => u.Email == credentials.email.ToUpper() && u.Password == CalculateSha256Hash(credentials.password)).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound("Incorrect password!");
            }

            _context.Entry(user).State = EntityState.Modified;
            try
            {
                user.LastUpdatedDate = DateTime.Now;
                user.Password = CalculateSha256Hash(credentials.newPassword);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }

            return Ok();
        }


        [AllowAnonymous]
        [Route("api/auth/reset-password")]
        [HttpPost]
        public async Task<ActionResult> PostResetPassword(Credentials credentials)
        {
            var user = await _context.Users
                .Where(u => u.Email == credentials.email).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound("Email not found!");
            }

            _context.Entry(user).State = EntityState.Modified;
            try
            {
                user.LastUpdatedDate = DateTime.Now;
                user.Password = CalculateSha256Hash(credentials.password);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }

            return Ok();
        }


        [AllowAnonymous]
        [Route("api/auth/app-version")]
        [HttpGet]
        public async Task<ActionResult<AppVersion>> GetAppVersion()
        {
            var res = await _context.AppVersions.FindAsync(1);
            return Ok(res);
        }


        [Authorize]
        [Route("api/auth/metabase-token")]
        [HttpGet]
        public async Task<ActionResult> GetMetabaseToken(int userConfigId)
        {

            if (userConfigId == 0) { return BadRequest(); }

            var config = await _context.Configs.FindAsync(userConfigId);

            if (config == null) { return BadRequest("Invalid user config id."); }

            var metabaseToken = GenerateEmbedToken(_config["Metabase:DashboardKey"], (int)userConfigId);
            var ret = new
            {
                //Config = config,
                MetabaseToken = metabaseToken,
            };

            return Ok(ret);

        }


        private string GenerateJSONWebToken(int userId, int expiry = 30)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Audience"],
                new[] { new Claim("negosuite_user_id", userId.ToString(System.Globalization.CultureInfo.InvariantCulture)) },
                expires: DateTime.UtcNow.AddMinutes(expiry),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }


        private string GenerateRefreshToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }


        // Function to generate hash (encrypt password) using SHA256 (Secure Hash Algorithm) //
        internal static string GetStringSha256Hash(string text)
        {
            if (String.IsNullOrEmpty(text))
                return String.Empty;
            //using (var sha = new System.Security.Cryptography.SHA256Managed())
            using (SHA256 sha = SHA256.Create())
            {
                byte[] textData = System.Text.Encoding.UTF8.GetBytes(text);
                byte[] hash = sha.ComputeHash(textData);
                return BitConverter.ToString(hash).Replace("-", String.Empty);
            }
        }

        public static string CalculateSha256Hash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(inputBytes);

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    builder.Append(hashBytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }


        private string GenerateEmbedToken(string metabaseSecretKey, int configId)
        {

            // Set expiration to 10 minutes from now
            var expiration = DateTime.UtcNow.AddMinutes(10);

            // Create the payload (claims)
            var claims = new[]
            {
                new Claim("resource", "{ \"dashboard\": 4 }", JsonClaimValueTypes.Json),
                new Claim("params", $"{{config_id: [{configId}]}}", JsonClaimValueTypes.Json)
            };

            // Create the security key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(metabaseSecretKey));

            // Create signing credentials
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Create the JWT token
            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiration,
                signingCredentials: creds
            );

            // Serialize the token to a string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}

public class Credentials
{
    public int? userId { get; set; }
    public string userName { get; set; }
    public string email { get; set; }
    public string password { get; set; }
    public string newPassword { get; set; }
    public string platform { get; set; }
    public string refreshToken { get; set; }
    public string platformVersion { get; set; }
}
