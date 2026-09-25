using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/items")]
    [ApiController]
    public class ItemsController : ControllerBase
    {
        public static bool STATUS_INACTIVE = false;
        public static bool STATUS_ACTIVE = true;
        private readonly negosuiteContext _context;
        public ItemsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/Items
        [HttpGet]
        public async Task<ActionResult> GetItems(string criteria)
        {

            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.Items
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => selectCriteria != null && selectCriteria.ItemType != null ? e.Type == selectCriteria.ItemType : true)
                .Where(e => selectCriteria != null && selectCriteria.ItemCategoryId != null ? e.ItemCategoryId == selectCriteria.ItemCategoryId : true)
                .Where(e => selectCriteria != null && selectCriteria.ShowInactive == true ? true : e.Status == STATUS_ACTIVE)
                .Select(e => new
                {
                    e.Id,
                    e.Code,
                    e.Name,
                    e.ItemCategoryId,
                    ItemCategoryName = e.ItemCategory.Name,
                    TypeName = getTypeName(e.Type),
                    e.Type,
                    e.Unit,
                    e.Rate,
                    e.Cost,
                    e.Status,
                    e.ToSell,
                    e.ToPurchase,
                    //e.SalesAccountId,
                    //e.PurchaseAccountId,
                    //e.SalesTaxRate,
                    //e.SalesTaxRateId,
                    //e.PurchaseTaxRate,
                    //e.PurchaseTaxRateId,
                    e.TrackInventory,
                    //e.InventoryAccountId,
                    //e.OpeningQuantity,
                    e.ReorderPoint
                }).OrderBy(e => e.Name).ToListAsync();

            return Ok(result);

        }

        // GET: api/Items/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Item>> GetItem(int id)
        {
            var item = await _context.Items.Where(e => e.Id == id)
                .Include(e => e.ItemCategory)
                .Include(e => e.PurchaseAccount).ThenInclude(a => a.Category)
                .Include(e => e.PurchaseTaxRate)
                .Include(e => e.SalesAccount).ThenInclude(a => a.Category)
                .Include(e => e.SalesTaxRate)
                .Include(e => e.InventoryAccount).ThenInclude(a => a.Category)
                .FirstOrDefaultAsync();


            if (item == null)
            {
                return NotFound();
            }

            return item;
        }

        // GET: api/Items/5
        [Route("average-cost/{id}")]
        [HttpGet]
        public async Task<ActionResult<Item>> GetItemAverageCost(int id)
        {
            decimal? result = await _context.BillDetails
                .Where(bd => bd.ItemId == id)
                .GroupBy(bd => true)
                .Select(g => g.Sum(bd => bd.Rate * bd.Quantity) / g.Sum(bd => bd.Quantity))
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }

        // GET: api/Items/units
        [Route("units")]
        [HttpGet]
        public async Task<ActionResult> GetItemUnits(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.Items
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => !string.IsNullOrEmpty(e.Unit))
                .Select(e => new {name = e.Unit}).Distinct().ToListAsync();
            return Ok(result);
        }

        // PUT: api/Items/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(int id, Item item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            if (item.Code != null)
            {
                var itemCodeExisit = await _context.Items.AnyAsync(e => e.Code == item.Code && e.UserConfigId == item.UserConfigId && e.Id != id);
                if (itemCodeExisit)
                {
                    return BadRequest("SKU already exist. Duplicate is not allowed");
                }
            }

            item.LastUpdatedDate = DateTime.UtcNow;
            _context.Entry(item).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
           
            return NoContent();
        }

        // POST: api/Items
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Item>> PostItem(Item item)
        {
            if (item.Code != null)
            {
                var itemCodeExisit = await _context.Items.AnyAsync(e => e.Code == item.Code && e.UserConfigId == item.UserConfigId);
                if (itemCodeExisit)
                {
                    return BadRequest("SKU already exist. Duplicate is not allowed");
                }
            }

            item.CreatedDate = DateTime.UtcNow;
            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetItem", new { id = item.Id }, item);
        }

        // DELETE: api/Items/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }
           
            try
            {
                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return BadRequest("Unable to delete item. It is probably used in another transaction.");
            }

            return NoContent();
        }

        private bool ItemExists(int id)
        {
            return _context.Items.Any(e => e.Id == id);
        }

        private static string getTypeName(string type)
        {
            if (type == "G") return "Goods";
            if (type == "S") return "Service";
            return "";
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
}
