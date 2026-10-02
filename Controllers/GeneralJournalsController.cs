using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Services;
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
        private readonly GeneralJournalService service;

        [ActivatorUtilitiesConstructor]
        public GeneralJournalsController(negosuiteContext context, GeneralJournalService service)
        {
            _context = context;
            this.service = service;
        }

        public GeneralJournalsController(negosuiteContext context) : this(context, new GeneralJournalService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetGeneralJournals(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!GeneralJournalQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            TransactionListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<TransactionListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await service.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GeneralJournalDetailDto>> GetGeneralJournal(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutGeneralJournal(int id, GeneralJournalUpdateRequest request)
        {
            var generalJournal = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (generalJournal.UserConfigId != CompanyId) return Forbid();
            if (id != generalJournal.Id) return BadRequest();
            if (!await _context.GeneralJournals.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, generalJournal, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            foreach (var entry in generalJournal.JournalEntries) entry.GeneralJournalId = id;

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
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Invoice journal entry not found.");
                }

                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
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
                            var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                            if (salesInvoice == null) return BadRequest("Linked invoice not found.");
                            salesInvoice.Balance = invoiceJE.Balance;
                            salesInvoice.LastUpdatedDate = DateTime.Now;
                            _context.Entry(salesInvoice).State = EntityState.Modified;
                        }

                        if (invoiceJE.Source == "PU")
                        {
                            var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                            if (bill == null) return BadRequest("Linked bill not found.");
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
                                var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                                if (salesInvoice == null) return BadRequest("Linked invoice not found.");
                                salesInvoice.Balance = invoiceJE.Balance;
                                salesInvoice.LastUpdatedDate = DateTime.Now;
                                _context.Entry(salesInvoice).State = EntityState.Modified;
                            }

                            if (invoiceJE.Source == "PU")
                            {
                                var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                                if (bill == null) return BadRequest("Linked bill not found.");
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
        public async Task<ActionResult<GeneralJournalDetailDto>> PostGeneralJournal(GeneralJournalCreateRequest request)
        {
            var generalJournal = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (generalJournal.UserConfigId != CompanyId) return Forbid();
            if (generalJournal.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, generalJournal, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
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

            foreach (var e in generalJournal.JournalEntries)
            {
                e.JournalDate = generalJournal.ReferenceDate;
                e.CreatedDate = DateTime.Now;

                // Credit AR: Subtract payment amount to invoice balance
                if (e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    var invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Data integrity error. Missing Sales Invoice record.");
                    invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                    invoiceJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(invoiceJE).State = EntityState.Modified;

                    if (invoiceJE.Source == "SI")
                    {
                        var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                        if (salesInvoice == null) return BadRequest("Linked invoice not found.");
                        salesInvoice.Balance = invoiceJE.Balance;
                        salesInvoice.LastUpdatedDate = DateTime.Now;
                        _context.Entry(salesInvoice).State = EntityState.Modified;
                    }
                }


                // Debit AP: Subtract payment amount to bill balance
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    var invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Data integrity error. Missing Bill record.");
                    invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                    invoiceJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(invoiceJE).State = EntityState.Modified;

                    if (invoiceJE.Source == "PU")
                    {
                        var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                        if (bill == null) return BadRequest("Linked bill not found.");
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;
                    }

                }

            }

            _context.GeneralJournals.Add(generalJournal);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetGeneralJournal", new { id = generalJournal.Id }, new TransactionResponseMapping().Map(generalJournal));
        }

        // DELETE: api/GeneralJournals/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGeneralJournal(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var generalJournal = await _context.GeneralJournals
                .Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (generalJournal == null)
            {
                return NotFound();
            }

            var validationError = await new TransactionWriteValidator(_context).TargetsAsync(CompanyId.Value, generalJournal.JournalEntries.Where(j => j.Status != -1), HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
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
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Invoice journal entry not found.");
                }

                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Bill journal entry not found.");
                }

                if (invoiceJE != null)
                {
                    invoiceJE.Balance = invoiceJE.Balance + e.Amount;
                    invoiceJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(invoiceJE).State = EntityState.Modified;

                    if (invoiceJE.Source == "SI")
                    {
                        var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                        if (salesInvoice == null) return BadRequest("Linked invoice not found.");
                        salesInvoice.Balance = invoiceJE.Balance;
                        salesInvoice.LastUpdatedDate = DateTime.Now;
                        _context.Entry(salesInvoice).State = EntityState.Modified;
                    }

                    if (invoiceJE.Source == "PU")
                    {
                        var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                        if (bill == null) return BadRequest("Linked bill not found.");
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
            return _context.GeneralJournals.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }
    }
}
