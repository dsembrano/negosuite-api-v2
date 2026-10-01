using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.SalesInvoicePayments;
using negosuite_api.Services;
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
        private readonly SalesInvoicePaymentService service;

        [ActivatorUtilitiesConstructor]
        public SalesInvoicePaymentsController(negosuiteContext context, SalesInvoicePaymentService service)
        {
            _context = context;
            this.service = service;
        }
        public SalesInvoicePaymentsController(negosuiteContext context) : this(context, new SalesInvoicePaymentService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetSalesInvoicePayments(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!SalesInvoicePaymentQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            SalesInvoicePaymentListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<SalesInvoicePaymentListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await service.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesInvoicePayment>> GetSalesInvoicePayment(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesInvoicePayment(int id, SalesInvoicePayment salesInvoicePayment)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (salesInvoicePayment.UserConfigId != CompanyId) return Forbid();
            if (id != salesInvoicePayment.Id)
            {
                return BadRequest();
            }

            if (!await _context.SalesInvoicePayments.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, salesInvoicePayment, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
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
                e.SalesInvoicePaymentId = id;
                e.JournalDate = salesInvoicePayment.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.ARTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
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
                            var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                            if (salesInvoice == null) return BadRequest("Invoice not found for this company.");
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
                                var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                                if (salesInvoice == null) return BadRequest("Invoice not found for this company.");
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
            if (!CompanyId.HasValue) return Unauthorized();
            if (salesInvoicePayment.UserConfigId != CompanyId) return Forbid();
            if (salesInvoicePayment.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, salesInvoicePayment, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
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
                    var invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Data integrity error. Missing Sales Invoice record.");
                    invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                    invoiceJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(invoiceJE).State = EntityState.Modified;

                    if (invoiceJE.Source == "SI")
                    {
                        var salesInvoice = await _context.SalesInvoices.SingleOrDefaultAsync(i => i.Id == invoiceJE.SalesInvoiceId && i.UserConfigId == CompanyId.Value);
                        if (salesInvoice == null) return BadRequest("Invoice not found for this company.");
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
            if (!CompanyId.HasValue) return Unauthorized();
            var salesInvoicePayment = await _context.SalesInvoicePayments.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
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
            return _context.SalesInvoicePayments.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }

    }
}
