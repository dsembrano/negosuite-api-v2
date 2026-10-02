using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Accounts;
using negosuite_api.Models;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), Route("api/accounts/tree"), ApiController]
public sealed class AccountTreeController(negosuiteContext db) : ControllerBase
{
    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;
    private async Task<AccountTree> Load(string search, CancellationToken ct)
    {
        var company = CompanyId.Value;
        var categories = await db.AccountCategories.AsNoTracking().Where(c => c.UserConfigId == company)
            .Select(c => new AccountCategoryDetailDto { Id = c.Id, Name = c.Name, OrderNo = c.OrderNo }).ToListAsync(ct);
        var rows = await db.Accounts.AsNoTracking().Where(a => a.UserConfigId == company).Select(a => new AccountListDto {
            Id = a.Id, Code = a.Code, Name = a.Name, CategoryId = a.CategoryId, ParentAccountId = a.ParentAccountId,
            CategoryName = a.Category.UserConfigId == company ? a.Category.Name : null,
            Type = a.Category.UserConfigId == company ? a.Category.Type : null,
            ParentAccountName = a.ParentAccount.UserConfigId == company ? a.ParentAccount.Name : null,
            ParentAccountCode = a.ParentAccount.UserConfigId == company ? a.ParentAccount.Code : null,
            RequireCustomer = a.RequireCustomer, RequireSupplier = a.RequireSupplier
        }).ToListAsync(ct);
        var useCodes = await db.Configs.Where(c => c.Id == company).Select(c => c.RequireAccountCode > 0).SingleAsync(ct);
        return new AccountTree(rows, categories, useCodes, search);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(string search = null, int? categoryId = null, CancellationToken ct = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (categoryId <= 0 || search?.Length > 200) return BadRequest("Invalid tree query.");
        var tree = await Load(search, ct);
        return Ok(tree.Categories.Where(c => !categoryId.HasValue || c.Id == categoryId));
    }
    [HttpGet("branch")]
    public async Task<IActionResult> Branch(int categoryId, int? parentAccountId = null, int offset = 0, int limit = 50, string search = null, CancellationToken ct = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (categoryId <= 0 || parentAccountId <= 0 || offset < 0 || limit < 1 || limit > 200 || search?.Length > 200) return BadRequest("Invalid branch query.");
        var tree = await Load(search, ct);
        if (!tree.HasCategory(categoryId) || !tree.HasParent(categoryId, parentAccountId)) return NotFound();
        return Ok(tree.Branch(categoryId, parentAccountId, offset, limit));
    }
    [HttpGet("export")]
    public async Task<IActionResult> Export(string search = null, int? categoryId = null, CancellationToken ct = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (categoryId <= 0 || search?.Length > 200) return BadRequest("Invalid tree query.");
        return Ok((await Load(search, ct)).Export(categoryId));
    }
}
