using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

// Company-less onboarding is permitted, but identity always comes from the signed token.
public sealed class AuthenticatedUserFilter : IAsyncActionFilter
{
    private readonly CompanyAccessService access;
    public AuthenticatedUserFilter(CompanyAccessService access) => this.access = access;
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null) { await next(); return; }
        var actor = await access.CurrentAsync(context.HttpContext.User, context.HttpContext.RequestAborted);
        if (actor == null) { context.Result = new UnauthorizedResult(); return; }
        if (context.HttpContext.Request.Headers.TryGetValue("configUuid", out var uuid) &&
            !string.IsNullOrWhiteSpace(uuid) && actor.Config?.Uuid != uuid.ToString())
        { context.Result = new ForbidResult(); return; }
        context.HttpContext.Items[CompanyAccessService.ActorKey] = actor;
        await next();
    }
}
