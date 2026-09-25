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
    [Route("api/general-journals")]
    [ApiController]
    public class GeneralJournalsController : ControllerBase
    {

        public static short STATUS_DRAFT = 0;
        public static short STATUS_POSTED = 1;
        public static short STATUS_DELETED = -1;

        private readonly negosuiteContext _context;

        public GeneralJournalsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/GeneralJournals
        /*
        [HttpGet]
        public async Task<ActionResult> GetGeneralJournals(string criteria)
        {
            var serilizerSettings = new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Local
            };
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria, serilizerSettings);

            var result = await _context.GeneralJournals
                .Where(e => selectCriteria.ReferenceNo != null ? e.ReferenceNo == selectCriteria.ReferenceNo : true)
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd : true)
                .Where(c => selectCriteria != null && selectCriteria.ShowDeleted == true ? true : c.Status != STATUS_DELETED )
                .Select(c => new
                {
                    c.Id,
                    c.ReferenceNo,
                    ReferenceDate = c.ReferenceDate,
                    c.Status,
                    StatusName = c.Status == STATUS_POSTED ? "Posted" : (c.Status == STATUS_DELETED ? "Deleted" : "Draft"),
                    c.Notes
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);
        }*/


        [HttpGet]
        public async Task<ActionResult> GetGeneralJournals(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPGeneralJournals
                .FromSqlInterpolated($"CALL GetGeneralJournals({userConfigId}, {periodStart}, {periodEnd}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Where(c => selectCriteria != null && selectCriteria.ShowDeleted == true ? true : c.Status != STATUS_DELETED)
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.Notes,
                    e.ResponsibilityCenterEntry,
                    e.Status,
                    StatusName = e.Status == STATUS_POSTED ? "Posted" : (e.Status == STATUS_DELETED ? "Deleted" : "Draft"),
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);
        }

        // GET: api/GeneralJournals/5
        [HttpGet("{id}")]
        public async Task<ActionResult<GeneralJournal>> GetGeneralJournal(int id)
        {
            var generalJournal = await _context.GeneralJournals.Where(j => j.Id == id)
                .Include(j => j.JournalEntries).ThenInclude(j => j.Account).ThenInclude(a => a.Category)
                .Include(j => j.JournalEntries).ThenInclude(j => j.Customer)
                .Include(j => j.JournalEntries).ThenInclude(j => j.Supplier)
                .Include(j => j.JournalEntries).ThenInclude(j => j.TaxRate)
                .Include(j => j.JournalEntries).ThenInclude(j => j.PaymentToJournalEntry)
                .SingleOrDefaultAsync();

            // Remove deleted entries unles main status is deleted
            if (generalJournal.Status != GeneralJournalsController.STATUS_DELETED)
            {
                generalJournal.JournalEntries = generalJournal.JournalEntries.Where(j => j.Status != STATUS_DELETED).ToList();
            }

            if (generalJournal == null)
            {
                return NotFound();
            }

            return generalJournal;
        }

        // PUT: api/GeneralJournals/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGeneralJournal(int id, GeneralJournal generalJournal)
        {
            if (id != generalJournal.Id)
            {
                return BadRequest();
            }

            var j = await _context.GeneralJournals.FirstOrDefaultAsync(e => e.ReferenceNo == generalJournal.ReferenceNo && e.UserConfigId == generalJournal.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Journal Reference# {generalJournal.ReferenceNo} already exist.");
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == generalJournal.UserConfigId);
            if (config == null || config.ARTradeAccountId == null)
            {
                return BadRequest();
            }

            generalJournal.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in generalJournal.JournalEntries.ToList())
            {

                e.JournalDate = generalJournal.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Invoice journal entry not found.");
                }

                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Bill journal entry not found.");
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

                        if (invoiceJE.Source == "PU")
                        {
                            var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                            bill.Balance = invoiceJE.Balance;
                            bill.LastUpdatedDate = DateTime.Now;
                            _context.Entry(bill).State = EntityState.Modified;
                        }
                    }
                }
                else
                {
                    if (e.Deleted == true)
                    {

                        if (e.AccountId == config.ARTradeAccountId && e.Nature == "D" && e.Balance < e.Amount)
                        {
                            return BadRequest("Can not remove/replace AR entry because a payment (credit) was already applied.");
                        }

                        if (e.AccountId == config.APTradeAccountId && e.Nature == "C" && e.Balance < e.Amount)
                        {
                            return BadRequest("Can not remove/replace AP entry because a payment (debit) was already applied.");
                        }

                        var entry = await _context.JournalEntries.FindAsync(e.Id);
                        _context.JournalEntries.Remove(entry);
                        e.Status = STATUS_DELETED;

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

                            if (invoiceJE.Source == "PU")
                            {
                                var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                                bill.Balance = invoiceJE.Balance;
                                bill.LastUpdatedDate = DateTime.Now;
                                _context.Entry(bill).State = EntityState.Modified;
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
            
            _context.Entry(generalJournal).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GeneralJournalExists(id))
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

        // POST: api/GeneralJournals
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<GeneralJournal>> PostGeneralJournal(GeneralJournal generalJournal)
        {
            var j = await _context.GeneralJournals.FirstOrDefaultAsync(e => e.ReferenceNo == generalJournal.ReferenceNo && e.UserConfigId == generalJournal.UserConfigId);
            if (j != null)
            {
                return Conflict($"Journal Reference# {generalJournal.ReferenceNo} already exist.");
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == generalJournal.UserConfigId);
            if (config == null || config.ARTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Receivable Trade.");
            }

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Payable Trade.");
            }

            generalJournal.CreatedDate = DateTime.Now;

            foreach(var e in generalJournal.JournalEntries)
            {
                e.JournalDate = generalJournal.ReferenceDate;
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


                // Debit AP: Subtract payment amount to bill balance
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    var invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Data integrity error. Missing Bill record.");
                    invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                    invoiceJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(invoiceJE).State = EntityState.Modified;

                    if (invoiceJE.Source == "PU")
                    {
                        var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;
                    }

                }

            }

            _context.GeneralJournals.Add(generalJournal);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetGeneralJournal", new { id = generalJournal.Id }, generalJournal);
        }

        // DELETE: api/GeneralJournals/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGeneralJournal(int id)
        {
            var generalJournal = await _context.GeneralJournals
                .Where(e => e.Id == id)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (generalJournal == null)
            {
                return NotFound();
            }

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == generalJournal.UserConfigId);
            if (config == null || config.ARTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Receivable Trade.");
            }
            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Payable Trade.");
            }

            // Journal Entries
            foreach (var e in generalJournal.JournalEntries.Where(e => e.Status != GeneralJournalsController.STATUS_DELETED).ToList())
            {

                if (e.AccountId == config.ARTradeAccountId && e.Nature == "D" && e.Balance < e.Amount)
                {
                    return BadRequest("Can not delete AR entry because a payment (credit) was already applied.");
                }

                if (e.AccountId == config.APTradeAccountId && e.Nature == "C" && e.Balance < e.Amount)
                {
                    return BadRequest("Can not delete AP entry because a payment (debit) was already applied.");
                }


                e.Status = STATUS_DELETED;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Invoice journal entry not found.");
                }

                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Bill journal entry not found.");
                }

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

                    if (invoiceJE.Source == "PU")
                    {
                        var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;
                    }

                }

                //e.LastUpdatedDate = DateTime.Now;
                //_context.Entry(e).State = EntityState.Modified;
                _context.Entry(e).State = EntityState.Deleted;
            }

            //generalJournal.Status = STATUS_DELETED;
            //generalJournal.LastUpdatedDate = DateTime.Now;
            //_context.Entry(generalJournal).State = EntityState.Modified;

            // Change to permanent deletion - Feb 2025
            _context.Entry(generalJournal).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!GeneralJournalExists(id))
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

        private bool GeneralJournalExists(int id)
        {
            return _context.GeneralJournals.Any(e => e.Id == id);
        }
    }
}
