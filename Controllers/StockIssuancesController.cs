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
    [TypeFilter(typeof(TransactionIntegrityFilter), Order = 100)]
    [Route("api/stock-issuances")]
    [ApiController]
    public class StockIssuancesController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly StockIssuanceService service;

        [ActivatorUtilitiesConstructor]
        public StockIssuancesController(negosuiteContext context, StockIssuanceService service)
        {
            _context = context;
            this.service = service;
        }

        public StockIssuancesController(negosuiteContext context) : this(context, new StockIssuanceService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetStockIssuances(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!StockIssuanceQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
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
        public async Task<ActionResult<StockIssuanceDetailDto>> GetStockIssuance(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutStockIssuance(int id, StockIssuanceUpdateRequest request)
        {
            var stockIssuance = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (stockIssuance.UserConfigId != CompanyId) return Forbid();
            if (id != stockIssuance.Id) return BadRequest();
            if (!await _context.StockIssuances.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, stockIssuance, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            foreach (var line in stockIssuance.StockIssuanceDetails) line.StockIssuanceId = id;
            foreach (var entry in stockIssuance.JournalEntries) _context.Entry(entry).Property("StockIssuanceId").CurrentValue = id;

            var j = await _context.StockIssuances.FirstOrDefaultAsync(e => e.ReferenceNo == stockIssuance.ReferenceNo && e.UserConfigId == stockIssuance.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Stock Issuance Reference# {stockIssuance.ReferenceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(stockIssuance.JournalEntries);
            stockIssuance.LastUpdatedDate = DateTime.Now;

            // Details
            foreach (var e in stockIssuance.StockIssuanceDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.StockIssuanceDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.StockIssuanceDetails.FindAsync(e.Id);
                        _context.StockIssuanceDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }


            // Journal Entries
            foreach (var e in stockIssuance.JournalEntries.ToList())
            {
                e.JournalDate = stockIssuance.ReferenceDate;
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


            _context.Entry(stockIssuance).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockIssuanceExists(id))
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
        public async Task<ActionResult<StockIssuanceDetailDto>> PostStockIssuance(StockIssuanceCreateRequest request)
        {
            var stockIssuance = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (stockIssuance.UserConfigId != CompanyId) return Forbid();
            if (stockIssuance.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, stockIssuance, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var j = await _context.StockIssuances.FirstOrDefaultAsync(e => e.ReferenceNo == stockIssuance.ReferenceNo && e.UserConfigId == stockIssuance.UserConfigId);
            if (j != null)
            {
                return Conflict($"Stock Issuance Reference# {stockIssuance.ReferenceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(stockIssuance.JournalEntries);
            stockIssuance.ReferenceDate = DateTime.Now;

            // Details
            foreach (var e in stockIssuance.StockIssuanceDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in stockIssuance.JournalEntries)
            {
                e.JournalDate = stockIssuance.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.StockIssuances.Add(stockIssuance);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetStockIssuance", new { id = stockIssuance.Id }, new TransactionResponseMapping().Map(stockIssuance));
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStockIssuance(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var StockIssuance = await _context.StockIssuances.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries)
                .Include(e => e.StockIssuanceDetails)
                .SingleOrDefaultAsync();

            if (StockIssuance == null)
            {
                return NotFound();
            }

            // Details
            foreach (var e in StockIssuance.StockIssuanceDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            foreach (var e in StockIssuance.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(StockIssuance).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockIssuanceExists(id))
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

        private bool StockIssuanceExists(int id)
        {
            return _context.StockIssuances.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }


    }
}
