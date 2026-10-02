using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Models;
using Newtonsoft.Json.Linq;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class UserRoleService
{
    private readonly negosuiteContext db;
    public UserRoleService(negosuiteContext db) => this.db = db;
    public static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>
        { ["name"] = "Name", ["notes"] = "Notes", ["isAdmin"] = "IsAdmin" };
    private IQueryable<UserRole> Roles(int company) => db.UserRoles.AsNoTracking().Where(r => r.UserConfigId == company || r.UserConfigId == null);

    public async Task<object> ListAsync(int company, AdministrationListOptions options, bool full, CancellationToken ct)
    {
        var query = Roles(company);
        if (!string.IsNullOrWhiteSpace(options.Search))
        { var term = options.Search.Trim(); query = query.Where(r => r.Name.Contains(term) || (r.Notes != null && r.Notes.Contains(term))); }
        return full ? await PageAsync(query, options, SortFields, "name", AdministrationMapping.ToDto, ct)
            : await PageAsync(query, options, SortFields, "name", r => new UserRoleListDto { Id = r.Id, Name = r.Name, Notes = r.Notes, Permission = r.Permission, IsAdmin = r.IsAdmin }, ct);
    }
    public async Task<UserRoleDetailDto> GetAsync(int company, int id, CancellationToken ct) => AdministrationMapping.ToDto(await Roles(company).SingleOrDefaultAsync(r => r.Id == id, ct));

    public async Task<UserRoleDetailDto> SaveAsync(User actor, int? id, UserRoleWriteRequest input, CancellationToken ct)
    {
        Admin(actor); var company = Company(actor);
        Require(input.UserConfigId == company, "Role company does not match authenticated membership.", 403);
        await using var tx = await LockCompanyAsync(db, company, ct);
        var role = id.HasValue ? await db.UserRoles.SingleOrDefaultAsync(r => r.Id == id && (r.UserConfigId == company || r.UserConfigId == null), ct)
            : new UserRole { UserConfigId = company, CreatedDate = DateTime.UtcNow, CreatedByUserId = actor.Id };
        Require(role != null, "Role not found.", 404);
        Require(role.UserConfigId != null, "Shared system roles are read-only.", 403);
        Require(!string.IsNullOrWhiteSpace(input.Name), "Role name is required."); Lengths<UserRole>(db, input);
        foreach (var json in new[] { input.Permission, input.AdvancePermission, input.ColumnRestriction, input.MobileAppPermission })
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try { Require(JToken.Parse(json).Type is JTokenType.Array or JTokenType.Object, "Permission data must be a JSON array or object."); }
            catch (Newtonsoft.Json.JsonException) { throw new AdministrationException("Invalid permission JSON."); }
        }
        if (id.HasValue && role.IsAdmin && !input.IsAdmin) await PreserveAdminAsync(db, company, null, role.Id, ct);
        AdministrationMapping.Apply(input, role);
        if (id.HasValue) { role.LastUpdatedDate = DateTime.UtcNow; role.LastUpdatedByUserId = actor.Id; }
        else db.UserRoles.Add(role);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await GetAsync(company, role.Id, ct);
    }

    public async Task DeleteAsync(User actor, int id, CancellationToken ct)
    {
        Admin(actor); var company = Company(actor);
        await using var tx = await LockCompanyAsync(db, company, ct);
        var role = await db.UserRoles.SingleOrDefaultAsync(r => r.Id == id && (r.UserConfigId == company || r.UserConfigId == null), ct);
        Require(role != null, "Role not found.", 404); Require(role.UserConfigId != null, "Shared system roles are read-only.", 403);
        Require(!await db.Users.AnyAsync(u => u.UserRoleId == id, ct), "Unable to delete a role currently assigned to a user.");
        db.UserRoles.Remove(role); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
