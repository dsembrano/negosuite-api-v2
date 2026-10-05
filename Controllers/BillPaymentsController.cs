using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using negosuite_api.Services;
using Newtonsoft.Json;


/*
 * This is not use anymore. Instead, use PaymentsController for both Bills or Others Payments
 */

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [TypeFilter(typeof(TransactionIntegrityFilter), Order = 100)]
    [Route("api/bill-payments")]
    [ApiController]
    public class BillPaymentsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private int? CompanyId => HttpContext.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        public BillPaymentsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/BillPayments
        [HttpGet]
        public async Task<ActionResult> GetBillPayments(string criteria)
        {

            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.BillPayments
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => (selectCriteria != null && selectCriteria.SupplierId.HasValue) ? e.SupplierId == selectCriteria.SupplierId : true)
                .Where(e => e.ReferenceDate >= (
                    selectCriteria != null && selectCriteria.PeriodStart != null ? selectCriteria.PeriodStart : e.ReferenceDate))
                .Where(e => e.ReferenceDate <= (
                    selectCriteria != null && selectCriteria.PeriodEnd != null ? selectCriteria.PeriodEnd : e.ReferenceDate))
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.SupplierId,
                    SupplierName = e.Supplier.Name,
                    e.PaymentModeId,
                    PaymentModeName = e.PaymentMode.Name,
                    e.PaidThroughAccountId,
                    PaidThroughAccountName = e.PaidThroughAccount.Name,
                    e.CheckNo,
                    e.Amount,
                    e.Balance,
                    e.Status,
                    e.ResponsibilityCenterEntry,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);

        }

        // GET: api/BillPayments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BillPayment>> GetBillPayment(int id)
        {
            var billPayment = await _context.BillPayments
               .Where(e => e.Id == id && e.UserConfigId == CompanyId)
               .Include(e => e.Supplier)
               .Include(e => e.PaymentMode)
               .Include(e => e.PaidThroughAccount)
               .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
               .Include(e => e.JournalEntries).ThenInclude(e => e.PaymentToJournalEntry)
               .SingleOrDefaultAsync();

            if (billPayment == null) return NotFound();
            // Exclude deleted entries.
            billPayment.JournalEntries = billPayment.JournalEntries.Where(j => j.Status != GeneralJournalsController.STATUS_DELETED).ToList();

            if (billPayment == null)
            {
                return NotFound();
            }

            return billPayment;
        }

        // PUT: api/BillPayments/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBillPayment(int id, BillPayment billPayment)
        {
            if (id != billPayment.Id)
            {
                return BadRequest();
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == CompanyId);
            if (config == null)
            {
                return BadRequest();
            }

            billPayment.LastUpdatedDate = DateTime.Now;

            JournalEntriesController.SanitizeEntries(billPayment.JournalEntries);

            // Journal Entries
            foreach (var e in billPayment.JournalEntries.ToList())
            {
                e.JournalDate = billPayment.ReferenceDate;




                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.JournalEntries.Add(e);
                    _context.Entry(e).Property("BillPaymentId").CurrentValue = id;

                    // Credit AP: Subtract payment amount to bill journal entry balance

                }
                else
                {
                    if (e.Deleted == true)
                    {
                        //var entry = await _context.JournalEntries.FindAsync(e.Id);
                        //_context.JournalEntries.Remove(entry);
                        e.Status = GeneralJournalsController.STATUS_DELETED;

                        // Credit AP: Subtract payment amount to bill journal entry balance

                    }
                    e.LastUpdatedDate = DateTime.Now;
                    _context.Entry(e).State = EntityState.Modified;

                }

            }

            _context.Entry(billPayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BillPaymentExists(id))
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

        // POST: api/BillPayments
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<BillPayment>> PostBillPayment(BillPayment billPayment)
        {
            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null)
            {
                return BadRequest("Missing configuration for Accounts Payable Trade.");
            }

            JournalEntriesController.SanitizeEntries(billPayment.JournalEntries);

            billPayment.CreatedDate = DateTime.Now;

            _context.BillPayments.Add(billPayment);

            foreach (var e in billPayment.JournalEntries)
            {
                e.JournalDate = billPayment.ReferenceDate;
                e.CreatedDate = DateTime.Now;

                // Debit: Subtract payment amount to bill balance

            }

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetBillPayment", new { id = billPayment.Id }, billPayment);
        }

        // DELETE: api/BillPayments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBillPayment(int id)
        {
            var billPayment = await _context.BillPayments.Where(e => e.Id == id && e.UserConfigId == CompanyId)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (billPayment == null)
            {
                return NotFound();
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == CompanyId);
            if (config == null)
            {
                return BadRequest();
            }

            // Journal Entries
            foreach (var e in billPayment.JournalEntries.Where(e => e.Status != GeneralJournalsController.STATUS_DELETED).ToList())
            {
                e.JournalDate = billPayment.ReferenceDate;




                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;

            }

            billPayment.LastUpdatedDate = DateTime.Now;
            billPayment.Status = GeneralJournalsController.STATUS_DELETED;
            _context.Entry(billPayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BillPaymentExists(id))
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

        private bool BillPaymentExists(int id)
        {
            return _context.BillPayments.Any(e => e.Id == id);
        }

        private static string GetStatusName(BillPayment payment)
        {
            string status = "";
            switch (payment.Status)
            {
                case -1:
                    status = "Deleted";
                    break;
                case 0:
                    status = "Draft";
                    break;
                case 1:
                    if (payment.Balance == payment.Amount) status = "Unapplied";
                    else if (payment.Balance == 0) status = "Fully applied";
                    else if (payment.Balance > 0 && payment.Balance != payment.Amount) status = "Partially applied";
                    break;
            }
            return status;
        }

    }
}
