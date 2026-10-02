using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Controllers;
using negosuite_api.Models;
using Newtonsoft.Json;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class UserService
{
    private readonly negosuiteContext db;
    public UserService(negosuiteContext db) => this.db = db;
    public static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>
    { ["name"] = "Name", ["email"] = "Email", ["mobileNo"] = "MobileNo", ["userTypeName"] = "UserTypeName", ["userRoleName"] = "UserRoleName", ["status"] = "Status" };

    public async Task<object> ListAsync(User actor, bool team, AdministrationListOptions options, CancellationToken ct)
    {
        Admin(actor); var company = Company(actor);
        var source = db.Users.AsNoTracking().Where(u => u.ConfigId == company && u.Status == (options.Status ?? true));
        if (team) source = source.Where(u => u.UserTypeId != UsersController.CONFIG_ADMIN);
        var query = source.Select(u => new UserListDto
        {
            Id = u.Id, Name = u.Name, Email = u.Email, MobileNo = u.MobileNo, UserTypeId = u.UserTypeId,
            UserTypeName = u.UserType.Name, Avatar = u.Avatar, Status = u.Status, UserRoleId = u.UserRoleId,
            UserRoleName = u.UserRole.UserConfigId == null || u.UserRole.UserConfigId == company ? u.UserRole.Name : null,
            UserRole = u.UserRole.UserConfigId == null || u.UserRole.UserConfigId == company ? AdministrationMapping.ToDto(u.UserRole) : null,
            UserUIConfig = u.UserUIConfig
        });
        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var term = options.Search.Trim();
            query = query.Where(u => u.Name.Contains(term) || u.Email.Contains(term) ||
                (u.MobileNo != null && u.MobileNo.Contains(term)) || (u.UserRoleName != null && u.UserRoleName.Contains(term)) ||
                (u.UserTypeName != null && u.UserTypeName.Contains(term)));
        }
        return await PageAsync(query, options, SortFields, "name", u => u, ct);
    }

    public async Task<UserDetailDto> GetAsync(User actor, int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id &&
            (u.Id == actor.Id || (actor.ConfigId != null && u.ConfigId == actor.ConfigId)), ct);
        if (user == null) return null;
        Require(id == actor.Id || CompanyAccessService.IsAdmin(actor), "You cannot view another user's profile.", 403);
        return AdministrationMapping.ToDto(user);
    }

    public async Task<User> TargetAsync(User actor, int id, CancellationToken ct)
    {
        var target = await db.Users.SingleOrDefaultAsync(u => u.Id == id && (u.Id == actor.Id ||
            (actor.ConfigId != null && u.ConfigId == actor.ConfigId)), ct);
        Require(target != null, "User not found.", 404); return target;
    }

    private async Task<UserRole> RoleAsync(int company, int? id, CancellationToken ct)
    {
        if (id == null) return null;
        var role = await db.UserRoles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && (r.UserConfigId == company || r.UserConfigId == null), ct);
        Require(role != null, "Role must belong to this company or be a shared system role."); return role;
    }

    public async Task UpdateAsync(User actor, int id, UserUpdateRequest input, CancellationToken ct)
    {
        Require(actor.Id == id || CompanyAccessService.IsAdmin(actor), "Administrator permission is required.", 403);
        // Company serialization protects role assignment and the last-administrator rule.
        await using var tx = actor.ConfigId.HasValue ? await LockCompanyAsync(db, actor.ConfigId.Value, ct) : await db.Database.BeginTransactionAsync(ct);
        var user = await TargetAsync(actor, id, ct);
        Require(!string.IsNullOrWhiteSpace(input.Name), "Name is required."); Lengths<User>(db, input);
        if (user.UserRoleId != input.UserRoleId)
        {
            Admin(actor); var role = await RoleAsync(Company(actor), input.UserRoleId, ct);
            if (role?.IsAdmin != true) await PreserveAdminAsync(db, Company(actor), user.Id, null, ct);
            user.UserRoleId = input.UserRoleId;
        }
        user.Name = input.Name; user.Email = input.Email;
        user.LastUpdatedDate = DateTime.UtcNow; user.LastUpdatedByUserId = actor.Id;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task SetRoleAsync(User actor, int id, int roleId, CancellationToken ct)
    {
        Admin(actor); var company = Company(actor);
        await using var tx = await LockCompanyAsync(db, company, ct);
        var user = await TargetAsync(actor, id, ct); var role = await RoleAsync(company, roleId, ct);
        if (!role.IsAdmin) await PreserveAdminAsync(db, company, user.Id, null, ct);
        user.UserRoleId = roleId; user.LastUpdatedDate = DateTime.UtcNow; user.LastUpdatedByUserId = actor.Id;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task SetUIAsync(User actor, int id, string value, CancellationToken ct)
    {
        Require(actor.Id == id, "UI preferences can only be changed by their owner.", 403);
        var user = await TargetAsync(actor, id, ct); user.UserUIConfig = value; user.LastUpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(User actor, int id, CancellationToken ct)
    {
        Admin(actor); var company = Company(actor);
        await using var tx = await LockCompanyAsync(db, company, ct);
        var user = await TargetAsync(actor, id, ct);
        Require(actor.Id != id, "You cannot deactivate your own account.");
        await PreserveAdminAsync(db, company, user.Id, null, ct);
        if (user.Status)
        {
            user.Email += "_" + DateTime.UtcNow.ToString("u"); user.Status = false;
            user.LastUpdatedDate = DateTime.UtcNow; user.LastUpdatedByUserId = actor.Id;
            await db.UserLogs.Where(l => l.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.IsRevoked, true), ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task<bool> CanAddAsync(User actor, int company, CancellationToken ct)
    {
        Admin(actor); Require(actor.ConfigId == company, "Company does not match authenticated membership.", 403);
        return !actor.Config.MaxUserCount.HasValue || await db.Users.CountAsync(u => u.ConfigId == company && u.Status && u.UserTypeId != UsersController.CONFIG_ADMIN, ct) < actor.Config.MaxUserCount;
    }

    public async Task<UserDetailDto> RegisterAsync(UserCreateRequest input, bool newAccount, CancellationToken ct)
    {
        Require(input.Id == 0, "New user ID must be zero or omitted.");
        var initial = await db.EmailLogs.AsNoTracking().SingleOrDefaultAsync(e => e.Uuid == input.Identifier, ct);
        Require(initial != null, "Invalid invitation or activation link.");
        await using var tx = !newAccount && initial.ConfigId.HasValue ? await LockCompanyAsync(db, initial.ConfigId.Value, ct) : await db.Database.BeginTransactionAsync(ct);
        var logs = await db.EmailLogs.FromSqlInterpolated($"SELECT * FROM emaillog WHERE Uuid = {input.Identifier} FOR UPDATE").ToListAsync(ct);
        var log = logs.SingleOrDefault();
        Require(log != null && log.Status == 1 && log.ExpiryDate > DateTime.Now && log.Action == (newAccount ? "new-account" : "member-invite"), "Invalid or expired invitation or activation link.");
        Require(string.Equals(input.Email, log.Email, StringComparison.OrdinalIgnoreCase), "Email does not match the invitation.");
        EmailPayload payload;
        try { payload = JsonConvert.DeserializeObject<EmailPayload>(log.Data ?? "null"); }
        catch (JsonException) { throw new AdministrationException("Invalid invitation data."); }
        Require(payload != null, "Invalid invitation data.");
        int? company = null, roleId = null;
        string password;
        if (newAccount)
        {
            Require(input.ConfigId == null && input.UserRoleId == null, "New accounts cannot select a company or role.");
            password = payload.Password;
            Require(password?.Length == 64 && password.All(Uri.IsHexDigit), "Invalid activation data.");
        }
        else
        {
            company = log.ConfigId; roleId = payload.UserRoleId;
            Require(company != null && payload.ConfigId == company && input.ConfigId == company && input.UserRoleId == roleId, "Invitation company or role does not match.");
            await RoleAsync(company.Value, roleId, ct);
            var config = await db.Configs.AsNoTracking().SingleOrDefaultAsync(c => c.Id == company, ct);
            Require(config != null, "Company no longer exists.");
            var count = await db.Users.CountAsync(u => u.ConfigId == company && u.Status && u.UserTypeId != UsersController.CONFIG_ADMIN, ct);
            Require(!config.MaxUserCount.HasValue || count < config.MaxUserCount, "Your team has reached the maximum number of users allowed for the subscription plan.");
            Require(!string.IsNullOrWhiteSpace(input.Password), "Password is required.");
            password = AuthController.CalculateSha256Hash(input.Password);
        }
        Require(!await db.Users.AnyAsync(u => u.Email == log.Email, ct), "Email address already exists.");
        var user = new User { Name = input.Name, Email = log.Email, Password = password, ConfigId = company,
            UserRoleId = roleId, Status = true, CreatedDate = DateTime.UtcNow };
        db.Users.Add(user); log.Status = 0; log.LastUpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return AdministrationMapping.ToDto(await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, ct));
    }
}
