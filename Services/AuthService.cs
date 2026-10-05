using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.Auth;
using negosuite_api.Contracts.ReferenceData;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class AuthService
{
    private readonly negosuiteContext db;
    private readonly AuthTokenService tokens;
    private readonly CompanyAccessService access;
    public AuthService(negosuiteContext db, AuthTokenService tokens, CompanyAccessService access)
    { this.db = db; this.tokens = tokens; this.access = access; }

    private async Task<AuthUserDto> UserAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.UserType).Include(u => u.Config).Include(u => u.UserRole)
            .SingleOrDefaultAsync(u => u.Id == id && u.Status, ct);
        if (user == null) return null;
        var currency = await db.Currencies.AsNoTracking().Where(c => c.IsBase).OrderBy(c => c.Id).Select(ReferenceDataMapping.CurrencyProjection).FirstOrDefaultAsync(ct);
        return new AuthUserDto { Id = user.Id, Username = user.Username, Name = user.Name, Email = user.Email, MobileNo = user.MobileNo,
            UserTypeId = user.UserTypeId, UserTypeName = user.UserType?.Name, Avatar = user.Avatar, Status = user.Status,
            ConfigId = user.ConfigId, Config = AdministrationMapping.ToDto(user.Config), BaseCurrency = currency,
            UserRoleId = user.UserRoleId, UserRole = user.UserRole != null && (user.UserRole.UserConfigId == null || user.UserRole.UserConfigId == user.ConfigId)
                ? AdministrationMapping.ToDto(user.UserRole) : null, UserUIConfig = user.UserUIConfig };
    }
    public async Task<SignInResponse> SignInAsync(SignInRequest input, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(input.UserName) || !string.IsNullOrWhiteSpace(input.Email), "Email or username is required.");
        Require(!string.IsNullOrEmpty(input.Password), "Password is required.");
        var email = input.Email?.Trim(); var username = input.UserName?.Trim(); var hash = PasswordSecurity.Hash(input.Password);
        var id = await db.Users.AsNoTracking().Where(u => u.Status && (!string.IsNullOrEmpty(username) ? u.Username == username : u.Email == email) && u.Password == hash)
            .Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
        Require(id.HasValue, "Invalid email or password.", 404);
        var user = await UserAsync(id.Value, ct); Require(user != null, "Invalid email or password.", 404);
        var token = tokens.Access(user.Id); var refresh = tokens.Refresh();
        var version = await GetAppVersionAsync(ct);
        var log = new UserLog { UserId = user.Id, Name = user.Name, Email = user.Email, Platform = input.Platform,
            PlatformVersion = input.PlatformVersion, SignInDate = DateTime.UtcNow, AccessToken = token, RefreshToken = refresh, ExpiryDate = DateTime.UtcNow.AddDays(2) };
        db.UserLogs.Add(log); await db.SaveChangesAsync(ct);
        return new SignInResponse { User = user, AccessToken = token, RefreshToken = refresh, AppVersion = Version(version), UserLog = new AuthUserLogDto
        { Id = log.Id, UserId = log.UserId, Name = log.Name, Email = log.Email, SignInDate = log.SignInDate, AccessToken = log.AccessToken,
            Platform = log.Platform, PlatformVersion = log.PlatformVersion, RefreshToken = log.RefreshToken, ExpiryDate = log.ExpiryDate, IsRevoked = log.IsRevoked } };
    }
    public async Task<AuthSessionResponse> RefreshAsync(RefreshAccessTokenRequest input, CancellationToken ct)
    {
        var log = await db.UserLogs.AsNoTracking().FirstOrDefaultAsync(l => l.RefreshToken == input.RefreshToken, ct);
        Require(log != null && log.IsRevoked != true && log.ExpiryDate > DateTime.UtcNow, "Invalid or expired refresh token", 401);
        var user = await UserAsync(log.UserId, ct); Require(user != null, "Invalid email or password.", 404);
        return new AuthSessionResponse { User = user, AccessToken = tokens.Access(user.Id), RefreshToken = input.RefreshToken,
            AppVersion = Version(await GetAppVersionAsync(ct)) };
    }
    public async Task ChangePasswordAsync(ClaimsPrincipal principal, ChangePasswordRequest input, CancellationToken ct)
    {
        var actor = await access.CurrentAsync(principal, ct); Require(actor != null, "Authentication is required.", 401);
        Require(string.Equals(actor.Email, input.Email?.Trim(), StringComparison.OrdinalIgnoreCase), "Email does not match the authenticated user.", 403);
        Require(PasswordSecurity.Matches(input.Password, actor.Password), "Incorrect password!", 404);
        PasswordSecurity.ValidateNew(input.NewPassword); Require(input.NewPassword != input.Password, "Choose a different password.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await db.Users.Where(u => u.Id == actor.Id && u.Status && u.Password == actor.Password)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Password, PasswordSecurity.Hash(input.NewPassword))
                .SetProperty(u => u.LastUpdatedDate, DateTime.UtcNow).SetProperty(u => u.LastUpdatedByUserId, actor.Id), ct);
        Require(changed == 1, "Password changed; sign in again before retrying.", 409);
        await RevokeAsync(actor.Id, ct); await tx.CommitAsync(ct);
    }
    public async Task ResetPasswordAsync(ResetPasswordRequest input, CancellationToken ct)
    {
        Require(Guid.TryParse(input.Identifier, out _), "Invalid or expired reset link."); PasswordSecurity.ValidateNew(input.Password);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var logs = await db.EmailLogs.FromSqlInterpolated($"SELECT * FROM emaillog WHERE Uuid = {input.Identifier} FOR UPDATE").ToListAsync(ct);
        var log = logs.SingleOrDefault();
        Require(log != null && log.Action == EmailWorkflowService.ResetAction && log.Status == 1 && log.ExpiryDate > DateTime.Now &&
            string.Equals(log.Email, input.Email?.Trim(), StringComparison.OrdinalIgnoreCase), "Invalid or expired reset link.");
        var users = await db.Users.FromSqlInterpolated($"SELECT * FROM user WHERE Email = {log.Email} AND Status = 1 FOR UPDATE").ToListAsync(ct);
        var user = users.SingleOrDefault();
        Require(user != null, "Invalid or expired reset link.");
        user.Password = PasswordSecurity.Hash(input.Password); user.LastUpdatedDate = DateTime.UtcNow; user.LastUpdatedByUserId = user.Id;
        log.Status = 0; log.LastUpdatedDate = DateTime.Now;
        await RevokeAsync(user.Id, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private Task<int> RevokeAsync(int userId, CancellationToken ct) => db.UserLogs.Where(l => l.UserId == userId)
        .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsRevoked, true), ct);
    public async Task<MetabaseTokenResponse> MetabaseAsync(ClaimsPrincipal principal, int companyId, CancellationToken ct)
    {
        Require(companyId > 0, "Invalid user config id.");
        var actor = await access.CurrentAsync(principal, ct); Require(actor != null, "Authentication is required.", 401);
        Require(actor.ConfigId == companyId && actor.Config != null, "Company does not match authenticated membership.", 403);
        return new MetabaseTokenResponse(tokens.Metabase(companyId));
    }
    public Task<AppVersionDetailDto> GetAppVersionAsync(CancellationToken ct) => db.AppVersions.AsNoTracking().Where(v => v.Id == 1)
        .Select(v => new AppVersionDetailDto { Id = v.Id, VersionCode = v.VersionCode, AndroidUpdateUrl = v.AndroidUpdateUrl, APKFilename = v.APKFilename }).SingleOrDefaultAsync(ct);
    private static AuthAppVersionDto Version(AppVersionDetailDto value) => new() { LatestVersion = value?.VersionCode, AndroidUpdateUrl = value?.AndroidUpdateUrl };
}
