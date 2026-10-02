using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

public sealed class AdministrationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is AdministrationException error)
            context.Result = new ObjectResult(error.Message) { StatusCode = error.Status };
        else if (context.Exception is DbUpdateException { InnerException: MySqlException sql } && sql.Number is 1062 or 1451 or 1452)
            context.Result = new BadRequestObjectResult(sql.Number == 1062 ? "A record with these unique details already exists." : "The operation conflicts with a referenced record.");
        else return;
        context.ExceptionHandled = true;
    }
}
