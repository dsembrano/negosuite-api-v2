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
    [Route("api/sales-invoice-payments")]
    [ApiController]
    public class SalesInvoicePaymentsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public SalesInvoicePaymentsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/SalesInvoicePayments
        /*
        [HttpGet]
        public async Task<ActionResult> GetSalesInvoicePayments(string criteria)
        {

            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.SalesInvoicePayments
                .Where(e => selectCriteria.ReferenceNo != null ? e.ReferenceNo == selectCriteria.ReferenceNo : true)
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd : true)
                .Where(e => selectCriteria.CustomerId.HasValue ? e.CustomerId == selectCriteria.CustomerId : true)
                .Where(e => selectCriteria.ShowDeleted == true ? true : e.Status != GeneralJournalsController.STATUS_DELETED)
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    CustomerName = e.Customer.Name,
                    e.Customer,
                    e.PaymentModeId,
                    PaymentModeName = e.PaymentMode.Name,
                    e.PaymentMode,
                    e.Amount,
                    e.Balance,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);

        }*/


        [HttpGet]
        public async Task<ActionResult> GetSalesInvoicePayments(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPSalesInvoicePayments
                .FromSqlInterpolated($"CALL GetSalesInvoicePayments({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    CustomerName = e.CustomerName,
                    e.PaymentModeId,
                    PaymentModeName = e.PaymentModeName,
                    e.DepositToAccountId,
                    DepositToAccountName = e.DepositToAccountName,
                    e.Amount,
                    e.Balance,
                    e.Notes,
                    e.Status,
                    e.ResponsibilityCenterEntry,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);

        }


        // GET: api/SalesInvoicePayments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SalesInvoicePayment>> GetSalesInvoicePayment(int id)
        {
            var salesInvoicePayment = await _context.SalesInvoicePayments.Where(e => e.Id == id)
                .Include(e => e.Customer)
                .Include(e => e.PaymentMode)
                .Include(e => e.DepositToAccount)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.PaymentToJournalEntry)
                .SingleOrDefaultAsync();

            if (salesInvoicePayment == null)
            {
                return NotFound();
            }

            return salesInvoicePayment;
        }

        // PUT: api/SalesInvoicePayments/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesInvoicePayment(int id, SalesInvoicePayment salesInvoicePayment)
        {
            if (id != salesInvoicePayment.Id)
            {
                return BadRequest();
            }

            var s = await _context.SalesInvoicePayments.FirstOrDefaultAsync(s => s.ReferenceNo == salesInvoicePayment.ReferenceNo && s.UserConfigId == salesInvoicePayment.UserConfigId && s.Id != id);
            if (s != null)
            {
                return Conflict($"Payment Receipt No {salesInvoicePayment.ReferenceNo} already exist.");
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == salesInvoicePayment.UserConfigId);
            if (config == null || config.ARTradeAccountId == null)
            {
                return BadRequest();
            }

            JournalEntriesController.SanitizeEntries(salesInvoicePayment.JournalEntries);
            salesInvoicePayment.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in salesInvoicePayment.JournalEntries.ToList())
            {
                e.JournalDate = salesInvoicePayment.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Invoice journal entry not found.");
                }

                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.JournalEntries.Add(e);

                    // Credit AR: Subtract payment amount to invoice journal entry balance
                    if (invoiceJE != null)
                    {
                        invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                        invoiceJE.LastUpdatedDate = DateTime.Now;
                        _context.Entry(invoiceJE).State = EntityState.Modified;

                        if (invoiceJE.Source == "SI")
                        {
                            var salesInvoice = await _context.SalesInvoices.FindAsync(invoiceJE.SalesInvoiceId);
                            salesInvoice.Balance = invoiceJE.Balance;
                            salesInvoice.LastUpdatedDate = DateTime.Now;
                            _context.Entry(salesInvoice).State = EntityState.Modified;
                        }
                    }
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.JournalEntries.FindAsync(e.Id);
                        _context.JournalEntries.Remove(entry);

                        // Credit AR: Add payment amount to invoice journal entry balance
                        if (invoiceJE != null)
                        {
                            invoiceJE.Balance = invoiceJE.Balance + e.Amount;
                            invoiceJE.LastUpdatedDate = DateTime.Now;
                            _context.Entry(invoiceJE).State = EntityState.Modified;

                            if (invoiceJE.Source == "SI")
                            {
                                var salesInvoice = await _context.SalesInvoices.FindAsync(invoiceJE.SalesInvoiceId);
                                salesInvoice.Balance = invoiceJE.Balance;
                                salesInvoice.LastUpdatedDate = DateTime.Now;
                                _context.Entry(salesInvoice).State = EntityState.Modified;
                            }
                        }
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }

            }


            var invoiceJe = salesInvoicePayment.JournalEntries
                .Where(e => e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null);


            _context.Entry(salesInvoicePayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesInvoicePaymentExists(id))
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

        // POST: api/SalesInvoicePayments
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SalesInvoicePayment>> PostSalesInvoicePayment(SalesInvoicePayment salesInvoicePayment)
        {
            var s = await _context.SalesInvoicePayments.FirstOrDefaultAsync(s => s.ReferenceNo == salesInvoicePayment.ReferenceNo && s.UserConfigId == salesInvoicePayment.UserConfigId);
            if (s != null)
            {
                return Conflict($"Payment Receipt No {salesInvoicePayment.ReferenceNo} already exist.");
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == salesInvoicePayment.UserConfigId);
            if (config == null || config.ARTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Receivable Trade.");
            }

            JournalEntriesController.SanitizeEntries(salesInvoicePayment.JournalEntries);
            salesInvoicePayment.CreatedDate = DateTime.Now;

            _context.SalesInvoicePayments.Add(salesInvoicePayment);

            foreach (var e in salesInvoicePayment.JournalEntries)
            {
                e.JournalDate = salesInvoicePayment.ReferenceDate;
                e.CreatedDate = DateTime.Now;

                // Credit AR: Subtract payment amount to invoice balance
                if (e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    var invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Data integrity error. Missing Sales Invoice record.");
                    invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                    invoiceJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(invoiceJE).State = EntityState.Modified;

                    if (invoiceJE.Source == "SI")
                    {
                        var salesInvoice = await _context.SalesInvoices.FindAsync(invoiceJE.SalesInvoiceId);
                        salesInvoice.Balance = invoiceJE.Balance;
                        salesInvoice.LastUpdatedDate = DateTime.Now;
                        _context.Entry(salesInvoice).State = EntityState.Modified;
                    }
                }

            }

            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSalesInvoicePayment", new { id = salesInvoicePayment.Id }, salesInvoicePayment);
        }

        // DELETE: api/SalesInvoicePayments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesInvoicePayment(int id)
        {
            var salesInvoicePayment = await _context.SalesInvoicePayments.Where(e => e.Id == id)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (salesInvoicePayment == null)
            {
                return NotFound();
            }

            if (salesInvoicePayment.Balance < salesInvoicePayment.Amount)
            {
                return BadRequest("Can not delete this transaction because payment was already applied.");
            }

                        // Journal Entries
            foreach (var e in salesInvoicePayment.JournalEntries.ToList())
            {
                /*
                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;*/
                _context.Entry(e).State = EntityState.Deleted;
            }

            /*
            salesInvoicePayment.Status = GeneralJournalsController.STATUS_DELETED;
            salesInvoicePayment.LastUpdatedDate = DateTime.Now;
            _context.Entry(salesInvoicePayment).State = EntityState.Modified;*/
            _context.Entry(salesInvoicePayment).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesInvoicePaymentExists(id))
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

        private bool SalesInvoicePaymentExists(int id)
        {
            return _context.SalesInvoicePayments.Any(e => e.Id == id);
        }

        private static string GetStatusName(SPSalesInvoicePayment payment)
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
