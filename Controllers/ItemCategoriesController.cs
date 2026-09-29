using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.ItemCategories;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), Route("api/item-categories"), ApiController]
public class ItemCategoriesController : ControllerBase
{
    private readonly ItemCategoryService categories;
    [ActivatorUtilitiesConstructor]
    public ItemCategoriesController(ItemCategoryService categories) => this.categories = categories;
    public ItemCategoriesController(negosuiteContext context) => categories = new ItemCategoryService(context);
    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

    [HttpGet]
    public async Task<ActionResult> GetItemCategories(string criteria, [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
        [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
        [FromQuery] bool? status = null)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        if (!ItemCategoryQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported item category sortBy or sortDirection. Use name, status, createdDate or lastUpdatedDate and asc or desc.");
        ItemCategoryListCriteria filter;
        try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<ItemCategoryListCriteria>(criteria); }
        catch (JsonException) { return BadRequest("Invalid item category criteria JSON."); }
        if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
        if (filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await categories.ListAsync(CompanyId.Value, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ItemCategoryDetailDto>> GetItemCategory(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await categories.GetAsync(CompanyId.Value, id, cancellationToken);
        return result == null ? NotFound() : result;
    }

    [HttpPost]
    public async Task<ActionResult<ItemCategoryDetailDto>> PostItemCategory(ItemCategoryCreateRequest itemCategory, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (itemCategory == null || itemCategory.Id != 0) return BadRequest("New item category ID must be zero or omitted.");
        if (itemCategory.UserConfigId != CompanyId) return Forbid();
        var result = await categories.SaveAsync(CompanyId.Value, null, itemCategory, cancellationToken);
        if (result.Error != null) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetItemCategory), new { id = result.Item.Id }, result.Item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutItemCategory(int id, ItemCategoryUpdateRequest itemCategory, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (itemCategory == null || id != itemCategory.Id) return BadRequest();
        if (itemCategory.UserConfigId != CompanyId) return Forbid();
        var result = await categories.SaveAsync(CompanyId.Value, id, itemCategory, cancellationToken);
        if (result.Missing) return NotFound();
        return result.Error != null ? BadRequest(result.Error) : NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItemCategory(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        try { if (!await categories.DeleteAsync(CompanyId.Value, id, cancellationToken)) return NotFound(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1451 })
        {
            return BadRequest("Unable to delete item category. It is probably used by an item or another record.");
        }
        return NoContent();
    }
}
