using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/inventory-adjustments")]
    [ApiController]
    public class InventoryAdjustmentsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public InventoryAdjustmentsController(negosuiteContext context)
        {
            _context = context;
        }


        /*
        [HttpGet]
        public async Task<ActionResult> GetInventoryAdjustments(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.InventoryAdjustments
                .Where(e => selectCriteria.ReferenceNo != null ? e.ReferenceNo == selectCriteria.ReferenceNo : true)
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd : true)
                .Where(e => selectCriteria != null && selectCriteria.ShowDeleted == true ? true : e.Status != GeneralJournalsController.STATUS_DELETED)
               .Select(e => new
               {
                   e.Id,
                   e.ReferenceNo,
                   e.ReferenceDate,
                   e.Notes,
                   e.Status,
                   InventoryLocationName = e.InventoryLocation.Name,
                   StatusName = GetStatusName(e)
               }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);
        }*/


        [HttpGet]
        public async Task<ActionResult> GetInventoryAdjustments(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPInventoryAdjustments
                .FromSqlInterpolated($"CALL GetInventoryAdjustments({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {supplierId}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    e.CustomerName,
                    e.Notes,
                    e.InventoryLocationName,
                    e.ResponsibilityCenterEntry,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryAdjustment>> GetInventoryAdjustment(int id)
        {
            var result = await _context.InventoryAdjustments.Where(e => e.Id == id)
                .Include(e => e.InventoryAdjustmentDetails).ThenInclude(e => e.Item)
                .Include(e => e.InventoryLocation)
                .Include(e => e.AdjustmentAccount)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.Customer).Include(e => e.Supplier)
                .SingleOrDefaultAsync();

            if (result == null)
            {
                return NotFound();
            }
            return result;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutInventoryAdjustment(int id, InventoryAdjustment inventoryAdjustment)
        {
            if (id != inventoryAdjustment.Id)
            {
                return BadRequest();
            }

            var j = await _context.InventoryAdjustments.FirstOrDefaultAsync(e => e.ReferenceNo == inventoryAdjustment.ReferenceNo && e.UserConfigId == inventoryAdjustment.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Inventory Adjustment Reference# {inventoryAdjustment.ReferenceNo} already exist.");
            }

            inventoryAdjustment.LastUpdatedDate = DateTime.Now;

            // Details
            foreach (var e in inventoryAdjustment.InventoryAdjustmentDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.InventoryAdjustmentDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.InventoryAdjustmentDetails.FindAsync(e.Id);
                        _context.InventoryAdjustmentDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }

            JournalEntriesController.SanitizeEntries(inventoryAdjustment.JournalEntries);

            // Journal Entries
            foreach (var e in inventoryAdjustment.JournalEntries.ToList())
            {
                e.JournalDate = inventoryAdjustment.ReferenceDate;
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.JournalEntries.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.JournalEntries.FindAsync(e.Id);
                        _context.JournalEntries.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }


            _context.Entry(inventoryAdjustment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryAdjustmentExists(id))
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


        [HttpPost]
        public async Task<ActionResult<InventoryAdjustment>> PostInventoryAdjustment(InventoryAdjustment inventoryAdjustment)
        {
            var j = await _context.InventoryAdjustments.FirstOrDefaultAsync(e => e.ReferenceNo == inventoryAdjustment.ReferenceNo && e.UserConfigId == inventoryAdjustment.UserConfigId);
            if (j != null)
            {
                return Conflict($"Inventory Adjustment Reference# {inventoryAdjustment.ReferenceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(inventoryAdjustment.JournalEntries);
            inventoryAdjustment.ReferenceDate = DateTime.Now;

            // Details
            foreach (var e in inventoryAdjustment.InventoryAdjustmentDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in inventoryAdjustment.JournalEntries)
            {
                e.JournalDate = inventoryAdjustment.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.InventoryAdjustments.Add(inventoryAdjustment);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetInventoryAdjustment", new { id = inventoryAdjustment.Id }, inventoryAdjustment);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryAdjustment(int id)
        {
            var inventoryAdjustment = await _context.InventoryAdjustments.Where(e => e.Id == id)
                .Include(e => e.JournalEntries)
                .Include(e => e.InventoryAdjustmentDetails)
                .SingleOrDefaultAsync();

            if (inventoryAdjustment == null)
            {
                return NotFound();
            }

            // Details
            foreach (var e in inventoryAdjustment.InventoryAdjustmentDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            foreach (var e in inventoryAdjustment.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(inventoryAdjustment).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryAdjustmentExists(id))
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

        private bool InventoryAdjustmentExists(int id)
        {
            return _context.InventoryAdjustments.Any(e => e.Id == id);
        }


        private static string GetStatusName(SPInventoryAdjustment transfer)
        {
            string status = "";
            switch (transfer.Status)
            {
                case -1:
                    status = "Deleted";
                    break;
                case 0:
                    status = "Draft";
                    break;
                case 1:
                    status = "Posted";
                    break;
            }
            return status;
        }

    }
}
