using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ConfigUuidFilter : IAsyncActionFilter
{
    public const string CompanyIdKey = "Negosuite.ValidatedCompanyId";
    private readonly negosuiteContext db;
    private readonly IConfiguration configuration;
    public ConfigUuidFilter(negosuiteContext db, IConfiguration configuration)
    { this.db = db; this.configuration = configuration; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue("configUuid", out var uuid) || uuid.Count != 1 || string.IsNullOrWhiteSpace(uuid))
        { context.Result = new UnauthorizedResult(); return; }
        var actor = await new CompanyAccessService(db).CurrentAsync(http.User, http.RequestAborted);
        if (actor == null) { context.Result = new UnauthorizedResult(); return; }
        var companyId = await db.Configs.AsNoTracking().Where(c => c.Uuid == uuid.ToString()).Select(c => (int?)c.Id).SingleOrDefaultAsync(http.RequestAborted);
        if (!companyId.HasValue) { context.Result = new UnauthorizedResult(); return; }
        if (actor.ConfigId != companyId) { context.Result = new ForbidResult(); return; }
        // Legacy report/list controllers also pass the company in a JSON criteria argument.
        if (context.ActionArguments.TryGetValue("criteria", out var criteria) && criteria is string json && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var value = JsonConvert.DeserializeObject<JObject>(json);
                var supplied = value?.GetValue("userConfigId", System.StringComparison.OrdinalIgnoreCase);
                if (supplied != null && supplied.Type != JTokenType.Null && supplied.Value<int>() != companyId)
                { context.Result = new ForbidResult(); return; }
            }
            catch (System.Exception ex) when (ex is JsonException or System.FormatException or System.InvalidCastException or System.OverflowException)
            { context.Result = new BadRequestObjectResult("Invalid criteria JSON or company ID."); return; }
        }

        // Keep the temporary development opt-out for side-by-side web clients.
        if (configuration.GetValue("Authentication:EnforceSingleWebSession", true) && http.Request.Headers.TryGetValue("X-UserLog", out var header))
        {
            UserLogInfo info;
            try { info = JsonConvert.DeserializeObject<UserLogInfo>(header.ToString()); }
            catch (JsonException) { context.Result = new UnauthorizedResult(); return; }
            if (info == null || info.UserId != actor.Id || info.Id <= 0) { context.Result = new UnauthorizedResult(); return; }
            var latest = await db.UserLogs.AsNoTracking().Where(l => l.UserId == actor.Id && l.Platform == "web")
                .OrderByDescending(l => l.SignInDate).ThenByDescending(l => l.Id).FirstOrDefaultAsync(http.RequestAborted);
            if (latest == null || latest.Id != info.Id || latest.IsRevoked == true)
            { context.Result = new UnauthorizedResult(); return; }
        }
        http.Items[CompanyIdKey] = companyId.Value;
        http.Items[CompanyAccessService.ActorKey] = actor;
        await next();
    }
    private sealed class UserLogInfo { public int Id { get; set; } public int UserId { get; set; } }
}
