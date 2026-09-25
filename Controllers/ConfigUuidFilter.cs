using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

public class ConfigUuidFilter : IAsyncActionFilter
{
    private readonly negosuiteContext _dbContext;

    public ConfigUuidFilter(negosuiteContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("configUuid", out var configUuidHeader))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var configUuid = configUuidHeader.FirstOrDefault();
        if (string.IsNullOrEmpty(configUuid))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var config = await _dbContext.Configs.FirstOrDefaultAsync(c => c.Uuid == configUuid);
        if (config == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Implement single device sign-in for the web app. If user sign-in in another device, the others will be sign off  
        if (context.HttpContext.Request.Headers.TryGetValue("X-UserLog", out var xUserLog))
        {
            UserLogInfo userLogInfo = JsonConvert.DeserializeObject<UserLogInfo>(xUserLog);
            var userLog = _dbContext.UserLogs.OrderByDescending(e => e.SignInDate).FirstOrDefault(e => e.UserId == userLogInfo.UserId && e.Platform == "web");

            if (userLog != null)
            {
                if (userLog.Id != userLogInfo.Id)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }
            }
        }

        await next();
    }

    class UserLogInfo
    {
        public int Id { get; set; }
        public int UserId { get; set;}
    }

}
