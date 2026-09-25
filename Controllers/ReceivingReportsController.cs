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
    [Route("api/receiving-reports")]
    [ApiController]
    public class ReceivingReportsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public ReceivingReportsController(negosuiteContext context)
        {
            _context = context;
        }


        [HttpGet]
        public async Task<ActionResult> GetReceivingReports(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPReceivingReports
                .FromSqlInterpolated($"CALL GetReceivingReports({userConfigId}, {periodStart}, {periodEnd}, {supplierId}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.SupplierId,
                    e.SupplierName,
                    e.Amount,
                    e.Balance,
                    e.DeliveryReceiptNo,
                    e.PurchaseOrderNo,
                    e.InventoryLocationName,
                    e.Notes,
                    e.ResponsibilityCenterEntry,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ReceivingReport>> GetReceivingReport(int id)
        {
            var receivingReport = await _context.ReceivingReports.Where(e => e.Id == id)
                .Include(e => e.Supplier).ThenInclude(e => e.SupplierAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
                .Include(e => e.Supplier).ThenInclude(e => e.SupplierContacts)
                .Include(e => e.ReceivingReportDetails).ThenInclude(e => e.Item)
                .Include(e => e.ReceivingReportDetails).ThenInclude(e => e.TaxRate)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.InventoryLocation)
                .Include(e => e.CreditAccount)
                .SingleOrDefaultAsync();

            if (receivingReport == null)
            {
                return NotFound();
            }

            return receivingReport;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutReceivingReport(int id, ReceivingReport receivingReport)
        {
            if (id != receivingReport.Id)
            {
                return BadRequest();
            }

            var b = await _context.ReceivingReports.FirstOrDefaultAsync(e => e.ReferenceNo == receivingReport.ReferenceNo && e.UserConfigId == receivingReport.UserConfigId && e.Id != id);
            if (b != null)
            {
                return Conflict($"ReceivingReport# {receivingReport.ReferenceNo} already exist.");
            }

            receivingReport.LastUpdatedDate = DateTime.Now;


            // Invoice Details
            foreach (var e in receivingReport.ReceivingReportDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.ReceivingReportDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.ReceivingReportDetails.FindAsync(e.Id);
                        _context.ReceivingReportDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }


            // Journal Entries
            foreach (var e in receivingReport.JournalEntries.ToList())
            {
                e.JournalDate = receivingReport.ReferenceDate;
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

            _context.Entry(receivingReport).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReceivingReportExists(id))
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
        public async Task<ActionResult<ReceivingReport>> PostReceivingReport(ReceivingReport receivingReport)
        {

            var b = await _context.ReceivingReports.FirstOrDefaultAsync(e => e.ReferenceNo == receivingReport.ReferenceNo && e.UserConfigId == receivingReport.UserConfigId);
            if (b != null)
            {
                return Conflict($"ReceivingReport# {receivingReport.ReferenceNo} already exist.");
            }

            receivingReport.CreatedDate = DateTime.Now;

            // Details
            foreach (var e in receivingReport.ReceivingReportDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in receivingReport.JournalEntries)
            {
                e.JournalDate = receivingReport.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.ReceivingReports.Add(receivingReport);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetReceivingReport", new { id = receivingReport.Id }, receivingReport);
        }

        // DELETE: api/ReceivingReports/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceivingReport(int id)
        {
            var receivingReport = await _context.ReceivingReports.Where(e => e.Id == id)
                .Include(e => e.JournalEntries)
                .Include(e => e.ReceivingReportDetails)
                .SingleOrDefaultAsync();

            if (receivingReport == null)
            {
                return NotFound();
            }

            // Journal Entries
            foreach (var e in receivingReport.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            // ReceivingReport Details
            foreach (var e in receivingReport.ReceivingReportDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(receivingReport).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReceivingReportExists(id))
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

        private bool ReceivingReportExists(int id)
        {
            return _context.ReceivingReports.Any(e => e.Id == id);
        }

        private static string GetStatusName(SPReceivingReport receivingReport)
        {
            string status = "";
            switch (receivingReport.Status)
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
