using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Accounts;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), Route("api/account-categories"), ApiController]
public class AccountCategoriesController : ControllerBase
{
    private readonly AccountCategoryService service;
    [ActivatorUtilitiesConstructor]
    public AccountCategoriesController(AccountCategoryService service) => this.service = service;
    public AccountCategoriesController(negosuiteContext context) => service = new AccountCategoryService(context);
    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

    [HttpGet]
    public async Task<ActionResult> GetAccountCategories(string criteria, [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
        [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        if (!AccountCategoryQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection.");
        AccountListCriteria filter;
        try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<AccountListCriteria>(criteria); }
        catch (JsonException) { return BadRequest("Invalid account category criteria JSON."); }
        if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
        if (filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await service.ListAsync(CompanyId.Value, pageNumber, pageSize, search, sortBy, sortDirection, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AccountCategoryDetailDto>> GetAccountCategory(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
        return result == null ? NotFound() : result;
    }

    [HttpPost]
    public async Task<ActionResult<AccountCategoryDetailDto>> PostAccountCategory(AccountCategoryCreateRequest input, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (input == null || input.Id != 0) return BadRequest("New account category ID must be zero or omitted.");
        if (input.UserConfigId != CompanyId) return Forbid();
        var result = await service.SaveAsync(CompanyId.Value, null, input, cancellationToken);
        if (result.Error != null) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetAccountCategory), new { id = result.Item.Id }, result.Item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutAccountCategory(int id, AccountCategoryUpdateRequest input, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (input == null || id != input.Id) return BadRequest();
        if (input.UserConfigId != CompanyId) return Forbid();
        var result = await service.SaveAsync(CompanyId.Value, id, input, cancellationToken);
        if (result.Missing) return NotFound();
        return result.Error != null ? BadRequest(result.Error) : NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAccountCategory(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        try { if (!await service.DeleteAsync(CompanyId.Value, id, cancellationToken)) return NotFound(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1451 })
        {
            return BadRequest("Unable to delete account category. It is probably used by another record.");
        }
        return NoContent();
    }
}
