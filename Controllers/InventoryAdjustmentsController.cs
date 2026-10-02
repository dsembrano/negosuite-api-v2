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
    [Route("api/inventory-adjustments")]
    [ApiController]
    public class InventoryAdjustmentsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly InventoryAdjustmentService service;

        [ActivatorUtilitiesConstructor]
        public InventoryAdjustmentsController(negosuiteContext context, InventoryAdjustmentService service)
        {
            _context = context;
            this.service = service;
        }

        public InventoryAdjustmentsController(negosuiteContext context) : this(context, new InventoryAdjustmentService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetInventoryAdjustments(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!InventoryAdjustmentQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
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
        public async Task<ActionResult<InventoryAdjustmentDetailDto>> GetInventoryAdjustment(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutInventoryAdjustment(int id, InventoryAdjustmentUpdateRequest request)
        {
            var inventoryAdjustment = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (inventoryAdjustment.UserConfigId != CompanyId) return Forbid();
            if (id != inventoryAdjustment.Id) return BadRequest();
            if (!await _context.InventoryAdjustments.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, inventoryAdjustment, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            foreach (var line in inventoryAdjustment.InventoryAdjustmentDetails) line.InventoryAdjustmentId = id;
            foreach (var entry in inventoryAdjustment.JournalEntries) entry.InventoryAdjustmentId = id;

            var j = await _context.InventoryAdjustments.FirstOrDefaultAsync(e => e.ReferenceNo == inventoryAdjustment.ReferenceNo && e.UserConfigId == inventoryAdjustment.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Inventory Adjustment Reference# {inventoryAdjustment.ReferenceNo} already exist.");
            }

            inventoryAdjustment.LastUpdatedDate = DateTime.Now;

            // Details
            foreach (var e in inventoryAdjustment.InventoryAdjustmentDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.InventoryAdjustmentDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.InventoryAdjustmentDetails.FindAsync(e.Id);
                        _context.InventoryAdjustmentDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }

            JournalEntriesController.SanitizeEntries(inventoryAdjustment.JournalEntries);

            // Journal Entries
            foreach (var e in inventoryAdjustment.JournalEntries.ToList())
            {
                e.JournalDate = inventoryAdjustment.ReferenceDate;
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


            _context.Entry(inventoryAdjustment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryAdjustmentExists(id))
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
        public async Task<ActionResult<InventoryAdjustmentDetailDto>> PostInventoryAdjustment(InventoryAdjustmentCreateRequest request)
        {
            var inventoryAdjustment = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (inventoryAdjustment.UserConfigId != CompanyId) return Forbid();
            if (inventoryAdjustment.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, inventoryAdjustment, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var j = await _context.InventoryAdjustments.FirstOrDefaultAsync(e => e.ReferenceNo == inventoryAdjustment.ReferenceNo && e.UserConfigId == inventoryAdjustment.UserConfigId);
            if (j != null)
            {
                return Conflict($"Inventory Adjustment Reference# {inventoryAdjustment.ReferenceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(inventoryAdjustment.JournalEntries);
            inventoryAdjustment.ReferenceDate = DateTime.Now;

            // Details
            foreach (var e in inventoryAdjustment.InventoryAdjustmentDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in inventoryAdjustment.JournalEntries)
            {
                e.JournalDate = inventoryAdjustment.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.InventoryAdjustments.Add(inventoryAdjustment);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetInventoryAdjustment", new { id = inventoryAdjustment.Id }, new TransactionResponseMapping().Map(inventoryAdjustment));
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteInventoryAdjustment(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var inventoryAdjustment = await _context.InventoryAdjustments.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries)
                .Include(e => e.InventoryAdjustmentDetails)
                .SingleOrDefaultAsync();

            if (inventoryAdjustment == null)
            {
                return NotFound();
            }

            // Details
            foreach (var e in inventoryAdjustment.InventoryAdjustmentDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            foreach (var e in inventoryAdjustment.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(inventoryAdjustment).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!InventoryAdjustmentExists(id))
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

        private bool InventoryAdjustmentExists(int id)
        {
            return _context.InventoryAdjustments.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }


    }
}
