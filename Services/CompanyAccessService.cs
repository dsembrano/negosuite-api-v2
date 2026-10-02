using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class CompanyAccessService
{
    public const string ActorKey = "Negosuite.AuthenticatedUser";
    private readonly negosuiteContext db;
    public CompanyAccessService(negosuiteContext db) => this.db = db;

    public async Task<User> CurrentAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirst("negosuite_user_id")?.Value, out var id) || id <= 0) return null;
        return await db.Users.AsNoTracking().Include(u => u.Config).Include(u => u.UserRole)
            .SingleOrDefaultAsync(u => u.Id == id && u.Status, ct);
    }

    public static bool IsAdmin(User user) => user?.ConfigId != null && user.UserRole?.IsAdmin == true &&
        (user.UserRole.UserConfigId == null || user.UserRole.UserConfigId == user.ConfigId);
}
