using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Items;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), Route("api/items"), ApiController]
public class ItemsController : ControllerBase
{
    public const bool STATUS_INACTIVE = false;
    public const bool STATUS_ACTIVE = true;
    private readonly ItemService items;
    [ActivatorUtilitiesConstructor]
    public ItemsController(ItemService items) => this.items = items;
    public ItemsController(negosuiteContext context) => items = new ItemService(context);
    private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;
    private static ItemListCriteria Parse(string criteria) => string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<ItemListCriteria>(criteria);

    [HttpGet]
    public async Task<ActionResult> GetItems(string criteria, [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default, [FromQuery] string search = null,
        [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
        [FromQuery] bool? toSell = null, [FromQuery] bool? toPurchase = null, [FromQuery] bool? trackInventory = null)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        if (!ItemQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported item sortBy or sortDirection. Use an item column and asc or desc.");
        ItemListCriteria filter;
        try { filter = Parse(criteria); } catch (JsonException) { return BadRequest("Invalid item criteria JSON."); }
        if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
        if (filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await items.ListAsync(CompanyId.Value, filter, pageNumber, pageSize, search, sortBy, sortDirection, toSell, toPurchase, cancellationToken, trackInventory));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ItemDetailDto>> GetItem(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await items.GetAsync(CompanyId.Value, id, cancellationToken);
        return result == null ? NotFound() : result;
    }

    [HttpGet("average-cost/{id}")]
    public async Task<ActionResult> GetItemAverageCost(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        var result = await items.AverageCostAsync(CompanyId.Value, id, cancellationToken);
        return result.HasValue ? Ok(result.Value) : NotFound();
    }

    [HttpGet("units")]
    public async Task<ActionResult> GetItemUnits(string criteria, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        ItemListCriteria filter;
        try { filter = Parse(criteria); } catch (JsonException) { return BadRequest("Invalid item criteria JSON."); }
        if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
        if (filter.UserConfigId != CompanyId) return Forbid();
        return Ok(await items.UnitsAsync(CompanyId.Value, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ItemDetailDto>> PostItem(ItemCreateRequest item, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (item == null || item.Id != 0) return BadRequest("New item ID must be zero or omitted.");
        if (item.UserConfigId != CompanyId) return Forbid();
        var result = await items.SaveAsync(CompanyId.Value, null, item, cancellationToken);
        if (result.Error != null) return BadRequest(result.Error);
        return CreatedAtAction(nameof(GetItem), new { id = result.Item.Id }, result.Item);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutItem(int id, ItemUpdateRequest item, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        if (item == null || id != item.Id) return BadRequest();
        if (item.UserConfigId != CompanyId) return Forbid();
        var result = await items.SaveAsync(CompanyId.Value, id, item, cancellationToken);
        if (result.Missing) return NotFound();
        return result.Error != null ? BadRequest(result.Error) : NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken cancellationToken = default)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        try { if (!await items.DeleteAsync(CompanyId.Value, id, cancellationToken)) return NotFound(); }
        catch (DbUpdateException) { return BadRequest("Unable to delete item. It is probably used in another transaction."); }
        return NoContent();
    }
    public static async Task<InventoryItemSummary> GetInventoryItem(negosuiteContext dbContext, int itemId)
    {
        var result = await dbContext.InventoryTransactions
            .Where(it => it.ItemId == itemId && it.Status == 1)
            .GroupBy(it => new
            {
                it.ItemId,
                it.ItemName,
                it.ItemCost,
                it.AverageCost,
                it.ItemReorderPoint
            })
            .Select(g => new InventoryItemSummary
            {
                ItemId = g.Key.ItemId,
                ItemName = g.Key.ItemName,
                LastInCost = g.Key.ItemCost,
                AverageCost = g.Key.AverageCost,
                ItemReorderPoint = g.Key.ItemReorderPoint,
                Quantity = g.Sum(it => it.QuantityIn - it.QuantityOut)
            })
            .FirstOrDefaultAsync();

        return result;
    }

    public class InventoryItemSummary
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal? LastInCost { get; set; }
        public decimal? AverageCost { get; set; }
        public decimal? ItemReorderPoint { get; set; }
        public decimal Quantity { get; set; }
    }




}
