using System;
using negosuite_api.Contracts.Transactions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Bills;
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
    [TypeFilter(typeof(TransactionIntegrityFilter), Order = 100)]
    [Route("api/bills")]
    [ApiController]
    public class BillsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly BillService service;

        [ActivatorUtilitiesConstructor]
        public BillsController(negosuiteContext context, BillService service)
        {
            _context = context;
            this.service = service;
        }

        public BillsController(negosuiteContext context) : this(context, new BillService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetBills(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!BillQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            BillListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<BillListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await service.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BillDetailDto>> GetBill(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBill(int id, BillUpdateRequest request)
        {
            var bill = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (bill.UserConfigId != CompanyId) return Forbid();
            if (id != bill.Id)
            {
                return BadRequest();
            }

            if (!await _context.Bills.AnyAsync(b => b.Id == id && b.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, bill, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var inventory = await BillInventorySnapshot.LoadAsync(_context, CompanyId.Value, bill.BillDetails, id, HttpContext.RequestAborted);
            var b = await _context.Bills.FirstOrDefaultAsync(e => e.BillNo == bill.BillNo && e.UserConfigId == bill.UserConfigId && e.Id != id);
            if (b != null)
            {
                return Conflict($"Bill# {bill.BillNo} already exist.");
            }

            bill.LastUpdatedDate = DateTime.Now;

            if (bill.Balance < 0)
            {
                return BadRequest("Invalid amount. Payment already applied to this Bill is more than the new amount.");
            }

            await inventory.ApplyAsync(_context, bill, false, HttpContext.RequestAborted);
            foreach (var e in bill.BillDetails.ToList())
            {
                e.BillId = id;
                if (e.Id == 0) { e.CreatedDate = DateTime.Now; _context.BillDetails.Add(e); }
                else if (e.Deleted == true) _context.Entry(e).State = EntityState.Deleted;
                else if (e.Touched == true) { e.LastUpdatedDate = DateTime.Now; _context.Entry(e).State = EntityState.Modified; }
            }

            // Journal Entries
            foreach (var e in bill.JournalEntries.ToList())
            {
                e.BillId = id;
                e.JournalDate = bill.BillDate;
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

            _context.Entry(bill).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BillExists(id))
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

        // POST: api/Bills
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<BillDetailDto>> PostBill(BillCreateRequest request)
        {
            var bill = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (bill.UserConfigId != CompanyId) return Forbid();

            if (bill.Id != 0) return BadRequest("New bill ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, bill, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var inventory = await BillInventorySnapshot.LoadAsync(_context, CompanyId.Value, bill.BillDetails, null, HttpContext.RequestAborted);
            var b = await _context.Bills.FirstOrDefaultAsync(e => e.BillNo == bill.BillNo && e.UserConfigId == bill.UserConfigId);
            if (b != null)
            {
                return Conflict($"Bill# {bill.BillNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(bill.JournalEntries);
            bill.CreatedDate = DateTime.Now;



            await inventory.ApplyAsync(_context, bill, false, HttpContext.RequestAborted);
            foreach (var e in bill.BillDetails) e.CreatedDate = DateTime.Now;

            foreach (var e in bill.JournalEntries)
            {
                e.JournalDate = bill.BillDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBill", new { id = bill.Id }, new TransactionResponseMapping().Map(bill));
        }

        // DELETE: api/Bills/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBill(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var bill = await _context.Bills.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries)
                .Include(e => e.BillDetails)
                .SingleOrDefaultAsync();

            if (bill == null)
            {
                return NotFound();
            }

            if (bill.Balance < bill.Amount)
            {
                return BadRequest("Can not delete this Bill because payment was already applied.");
            }

            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, bill, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var inventory = await BillInventorySnapshot.LoadAsync(_context, CompanyId.Value, bill.BillDetails, id, HttpContext.RequestAborted);
            // Journal Entries
            foreach (var e in bill.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            await inventory.ApplyAsync(_context, bill, true, HttpContext.RequestAborted);
            foreach (var e in bill.BillDetails.ToList()) _context.Entry(e).State = EntityState.Deleted;

            _context.Entry(bill).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BillExists(id))
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

        private bool BillExists(int id)
        {
            return _context.Bills.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }

    }
}
