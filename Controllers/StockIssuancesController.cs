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
    [Route("api/stock-issuances")]
    [ApiController]
    public class StockIssuancesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public StockIssuancesController(negosuiteContext context)
        {
            _context = context;
        }


        /*
        [HttpGet]
        public async Task<ActionResult> GetStockIssuances(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.StockIssuances
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
        public async Task<ActionResult> GetStockIssuances(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPStockIssuances
                .FromSqlInterpolated($"CALL GetStockIssuances({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {supplierId}, {referenceNo}, {arrayString})")
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
        public async Task<ActionResult<StockIssuance>> GetStockIssuance(int id)
        {
            var result = await _context.StockIssuances.Where(e => e.Id == id)
                .Include(e => e.StockIssuanceDetails).ThenInclude(e => e.Item)
                .Include(e => e.InventoryLocation)
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
        public async Task<IActionResult> PutStockIssuance(int id, StockIssuance stockIssuance)
        {
            if (id != stockIssuance.Id)
            {
                return BadRequest();
            }

            var j = await _context.StockIssuances.FirstOrDefaultAsync(e => e.ReferenceNo == stockIssuance.ReferenceNo && e.UserConfigId == stockIssuance.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Stock Issuance Reference# {stockIssuance.ReferenceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(stockIssuance.JournalEntries);
            stockIssuance.LastUpdatedDate = DateTime.Now;

            // Details
            foreach (var e in stockIssuance.StockIssuanceDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.StockIssuanceDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.StockIssuanceDetails.FindAsync(e.Id);
                        _context.StockIssuanceDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }


            // Journal Entries
            foreach (var e in stockIssuance.JournalEntries.ToList())
            {
                e.JournalDate = stockIssuance.ReferenceDate;
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


            _context.Entry(stockIssuance).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockIssuanceExists(id))
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
        public async Task<ActionResult<StockIssuance>> PostStockIssuance(StockIssuance stockIssuance)
        {
            var j = await _context.StockIssuances.FirstOrDefaultAsync(e => e.ReferenceNo == stockIssuance.ReferenceNo && e.UserConfigId == stockIssuance.UserConfigId);
            if (j != null)
            {
                return Conflict($"Stock Issuance Reference# {stockIssuance.ReferenceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(stockIssuance.JournalEntries);
            stockIssuance.ReferenceDate = DateTime.Now;

            // Details
            foreach (var e in stockIssuance.StockIssuanceDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in stockIssuance.JournalEntries)
            {
                e.JournalDate = stockIssuance.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.StockIssuances.Add(stockIssuance);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetStockIssuance", new { id = stockIssuance.Id }, stockIssuance);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStockIssuance(int id)
        {
            var StockIssuance = await _context.StockIssuances.Where(e => e.Id == id)
                .Include(e => e.JournalEntries)
                .Include(e => e.StockIssuanceDetails)
                .SingleOrDefaultAsync();

            if (StockIssuance == null)
            {
                return NotFound();
            }

            // Details
            foreach (var e in StockIssuance.StockIssuanceDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            foreach (var e in StockIssuance.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(StockIssuance).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockIssuanceExists(id))
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

        private bool StockIssuanceExists(int id)
        {
            return _context.StockIssuances.Any(e => e.Id == id);
        }


        private static string GetStatusName(SPStockIssuance stockIssuance)
        {
            string status = "";
            switch (stockIssuance.Status)
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
