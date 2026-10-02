using System;
using negosuite_api.Contracts.Transactions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Payments;
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
    [Route("api/payments")]
    [ApiController]
    public partial class PaymentsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly PaymentService service;

        [ActivatorUtilitiesConstructor]
        public PaymentsController(negosuiteContext context, PaymentService service)
        {
            _context = context;
            this.service = service;
        }

        public PaymentsController(negosuiteContext context) : this(context, new PaymentService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetPayments(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null, [FromQuery] bool? isBillPayment = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!PaymentQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            PaymentListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<PaymentListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await service.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, isBillPayment, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentDetailDto>> GetPayment(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPayment(int id, PaymentUpdateRequest request)
        {
            var Payment = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (Payment.UserConfigId != CompanyId) return Forbid();
            if (id != Payment.Id) return BadRequest();
            if (!await _context.Payments.AnyAsync(p => p.Id == id && p.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, Payment, false, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);

            var p = await _context.Payments.FirstOrDefaultAsync(p => p.ReferenceNo == Payment.ReferenceNo && p.UserConfigId == Payment.UserConfigId && p.Id != id);
            if (p != null)
            {
                return Conflict($"Payment Reference# {Payment.ReferenceNo} already exist.");
            }

            Payment.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in Payment.JournalEntries.ToList())
            {
                e.PaymentId = id;
                e.JournalDate = Payment.ReferenceDate;

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

            _context.Entry(Payment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentExists(id))
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
        public async Task<ActionResult<PaymentDetailDto>> PostPayment(PaymentCreateRequest request)
        {
            var Payment = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (Payment.UserConfigId != CompanyId) return Forbid();
            if (Payment.Id != 0) return BadRequest("New payments must have an ID of zero.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, Payment, false, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);

            var p = await _context.Payments.FirstOrDefaultAsync(p => p.ReferenceNo == Payment.ReferenceNo && p.UserConfigId == Payment.UserConfigId);
            if (p != null)
            {
                return Conflict($"Payment Reference# {Payment.ReferenceNo} already exist.");
            }

            Payment.CreatedDate = DateTime.Now;

            _context.Payments.Add(Payment);

            foreach (var e in Payment.JournalEntries)
            {
                e.JournalDate = Payment.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetPayment", new { id = Payment.Id }, new TransactionResponseMapping().Map(Payment));
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePayment(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var Payment = await _context.Payments
                .Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (Payment == null)
            {
                return NotFound();
            }

            // Journal Entries
            foreach (var e in Payment.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(Payment).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentExists(id))
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


        [Route("bill/{id}")]
        [HttpPut]
        public async Task<IActionResult> PutBillPayment(int id, PaymentUpdateRequest request)
        {
            var billPayment = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (billPayment.UserConfigId != CompanyId) return Forbid();
            if (id != billPayment.Id) return BadRequest();
            if (!await _context.Payments.AnyAsync(p => p.Id == id && p.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, billPayment, true, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);

            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest();
            }

            billPayment.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in billPayment.JournalEntries.ToList())
            {
                e.PaymentId = id;
                e.JournalDate = billPayment.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE == null) return BadRequest("Bill payment journal entry not found.");
                }

                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.JournalEntries.Add(e);

                    // Credit AP: Subtract payment amount to bill journal entry balance
                    if (invoiceJE != null)
                    {
                        invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                        invoiceJE.LastUpdatedDate = DateTime.Now;
                        _context.Entry(invoiceJE).State = EntityState.Modified;

                        var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                        if (bill == null) return BadRequest("Bill target not found for this company.");
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;

                    }
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        //e.Status = GeneralJournalsController.STATUS_DELETED;
                        var entry = await _context.JournalEntries.FindAsync(e.Id);
                        _context.JournalEntries.Remove(entry);

                        // Credit AP: Subtract payment amount to bill journal entry balance
                        if (invoiceJE != null)
                        {
                            invoiceJE.Balance = invoiceJE.Balance + e.Amount;
                            invoiceJE.LastUpdatedDate = DateTime.Now;
                            _context.Entry(invoiceJE).State = EntityState.Modified;

                            var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                            if (bill == null) return BadRequest("Bill target not found for this company.");
                            bill.Balance = invoiceJE.Balance;
                            bill.LastUpdatedDate = DateTime.Now;
                            _context.Entry(bill).State = EntityState.Modified;
                        }
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;

                    }

                }

            }

            _context.Entry(billPayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentExists(id))
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


        // Post Bill Payment
        [Route("bill")]
        [HttpPost]
        public async Task<ActionResult<PaymentDetailDto>> PostBillPayment(PaymentCreateRequest request)
        {
            var billPayment = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (billPayment.UserConfigId != CompanyId) return Forbid();
            if (billPayment.Id != 0) return BadRequest("New payments must have an ID of zero.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, billPayment, true, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Payable Trade.");
            }

            billPayment.CreatedDate = DateTime.Now;

            _context.Payments.Add(billPayment);

            foreach (var e in billPayment.JournalEntries)
            {
                e.JournalDate = billPayment.ReferenceDate;
                e.CreatedDate = DateTime.Now;

                // Debit: Subtract payment amount to bill balance
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    var billJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (billJE == null) return BadRequest("Data integrity error. Missing bill record.");
                    billJE.Balance = billJE.Balance - e.Amount;
                    billJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(billJE).State = EntityState.Modified;

                    var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == billJE.BillId && b.UserConfigId == CompanyId.Value);
                    if (bill == null) return BadRequest("Bill target not found for this company.");
                    bill.Balance = billJE.Balance;
                    bill.LastUpdatedDate = DateTime.Now;
                    _context.Entry(bill).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetPayment", new { id = billPayment.Id }, new TransactionResponseMapping().Map(billPayment));
        }


        [Route("bill/{id}")]
        [HttpDelete]
        public async Task<IActionResult> DeleteBillPayment(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var billPayment = await _context.Payments.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (billPayment == null)
            {
                return NotFound();
            }

            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest();
            }

            var validationError = await service.ValidateTargetsAsync(CompanyId.Value, billPayment.JournalEntries.Where(j => j.Status != -1), true, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);

            // Journal Entries
            foreach (var e in billPayment.JournalEntries.Where(e => e.Status != GeneralJournalsController.STATUS_DELETED).ToList())
            {
                e.JournalDate = billPayment.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.SingleOrDefaultAsync(j => j.Id == e.PaymentToJournalEntryId && j.UserConfigId == CompanyId.Value);
                    if (invoiceJE != null)
                    {
                        // Credit AP: Subtract payment amount to bill journal entry balance
                        invoiceJE.Balance = invoiceJE.Balance + e.Amount;
                        invoiceJE.LastUpdatedDate = DateTime.Now;
                        _context.Entry(invoiceJE).State = EntityState.Modified;

                        var bill = await _context.Bills.SingleOrDefaultAsync(b => b.Id == invoiceJE.BillId && b.UserConfigId == CompanyId.Value);
                        if (bill == null) return BadRequest("Bill target not found for this company.");
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;
                    }
                    else
                    {
                        return BadRequest("Bill payment journal entry not found.");
                    }
                }

                /*
                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;*/

                _context.Entry(e).State = EntityState.Deleted;

                //_context.JournalEntries.Remove(e);


            }

            /*
            billPayment.LastUpdatedDate = DateTime.Now;
            billPayment.Status = GeneralJournalsController.STATUS_DELETED;
            _context.Entry(billPayment).State = EntityState.Modified;*/

            _context.Entry(billPayment).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentExists(id))
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


        private bool PaymentExists(int id)
        {
            return _context.Payments.Any(e => e.Id == id && e.UserConfigId == CompanyId);
        }

    }
}
