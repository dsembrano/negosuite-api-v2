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
    [Route("api/stock-transfers")]
    [ApiController]
    public class StockTransfersController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public StockTransfersController(negosuiteContext context)
        {
            _context = context;
        }

        /*
        [HttpGet]
        public async Task<ActionResult> GetStockTransfers(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.StockTransfers
                .Where(e => selectCriteria.ReferenceNo != null ? e.ReferenceNo == selectCriteria.ReferenceNo : true)
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd : true)
                .Where(e => selectCriteria != null && selectCriteria.ShowDeleted == true ? true : e.Status != GeneralJournalsController.STATUS_DELETED)
               .Select(e => new
               {
                   e.Id,
                   e.ReferenceNo,
                   e.ReferenceDate,
                   FromInventoryLocationName = e.FromInventoryLocation.Name,
                   ToInventoryLocationName = e.ToInventoryLocation.Name,
                   e.Notes,
                   e.Status,
                   StatusName = GetStatusName(e)
               }).ToListAsync();

            return Ok(result);
        }*/


        [HttpGet]
        public async Task<ActionResult> GetStockTransfers(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPStockTransfers
                .FromSqlInterpolated($"CALL GetStockTransfers({userConfigId}, {periodStart}, {periodEnd}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.Notes,
                    e.FromInventoryLocationName,
                    e.ToInventoryLocationName,
                    e.ResponsibilityCenterEntry,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<StockTransfer>> GetStockTransfer(int id)
        {
            var result = await _context.StockTransfers.Where(e => e.Id == id)
                .Include(e => e.StockTransferDetails).ThenInclude(e => e.Item).ThenInclude(e => e.InventoryAccount).ThenInclude(a => a.Category)
                .Include(e => e.FromInventoryLocation)
                .Include(e => e.ToInventoryLocation)
                .SingleOrDefaultAsync();

            if (result == null)
            {
                return NotFound();
            }
            return result;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutStockTransfer(int id, StockTransfer stockTransfer)
        {
            if (id != stockTransfer.Id)
            {
                return BadRequest();
            }

            var j = await _context.StockTransfers.FirstOrDefaultAsync(e => e.ReferenceNo == stockTransfer.ReferenceNo && e.UserConfigId == stockTransfer.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Stock Transfer Reference# {stockTransfer.ReferenceNo} already exist.");
            }

            stockTransfer.LastUpdatedDate = DateTime.Now;

            // Details
            foreach (var e in stockTransfer.StockTransferDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.StockTransferDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.StockTransferDetails.FindAsync(e.Id);
                        _context.StockTransferDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }

            _context.Entry(stockTransfer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockTransferExists(id))
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
        public async Task<ActionResult<StockTransfer>> PostStockTransfer(StockTransfer stockTransfer)
        {
            var j = await _context.StockTransfers.FirstOrDefaultAsync(e => e.ReferenceNo == stockTransfer.ReferenceNo && e.UserConfigId == stockTransfer.UserConfigId);
            if (j != null)
            {
                return Conflict($"Stock Transfer Reference# {stockTransfer.ReferenceNo} already exist.");
            }

            stockTransfer.ReferenceDate = DateTime.Now;

            // Details
            foreach (var e in stockTransfer.StockTransferDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            _context.StockTransfers.Add(stockTransfer);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetStockTransfer", new { id = stockTransfer.Id }, stockTransfer);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStockTransfer(int id)
        {
            var stockTransfer = await _context.StockTransfers.Where(e => e.Id == id)
                .Include(e => e.StockTransferDetails)
                .SingleOrDefaultAsync();

            if (stockTransfer == null)
            {
                return NotFound();
            }

            // Details
            foreach (var e in stockTransfer.StockTransferDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(stockTransfer).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockTransferExists(id))
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

        private bool StockTransferExists(int id)
        {
            return _context.StockTransfers.Any(e => e.Id == id);
        }


        private static string GetStatusName(SPStockTransfer transfer)
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
