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
    [TypeFilter(typeof(TransactionIntegrityFilter), Order = 100)]
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


        // Preserve all V1 bill-payment URLs. Shared integrity rules use persisted links.
        [HttpPut("bill/{id}")]
        public Task<IActionResult> PutBillPayment(int id, PaymentUpdateRequest request) => PutPayment(id, request);

        [HttpPost("bill")]
        public Task<ActionResult<PaymentDetailDto>> PostBillPayment(PaymentCreateRequest request) => PostPayment(request);

        [HttpDelete("bill/{id}")]
        public Task<IActionResult> DeleteBillPayment(int id) => DeletePayment(id);

        private bool PaymentExists(int id)
        {
            return _context.Payments.Any(e => e.Id == id && e.UserConfigId == CompanyId);
        }

    }
}
