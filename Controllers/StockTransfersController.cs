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
    [Route("api/stock-transfers")]
    [ApiController]
    public class StockTransfersController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly StockTransferService service;

        [ActivatorUtilitiesConstructor]
        public StockTransfersController(negosuiteContext context, StockTransferService service)
        {
            _context = context;
            this.service = service;
        }

        public StockTransfersController(negosuiteContext context) : this(context, new StockTransferService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetStockTransfers(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!StockTransferQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
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
        public async Task<ActionResult<StockTransferDetailDto>> GetStockTransfer(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutStockTransfer(int id, StockTransferUpdateRequest request)
        {
            var stockTransfer = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (stockTransfer.UserConfigId != CompanyId) return Forbid();
            if (id != stockTransfer.Id) return BadRequest();
            if (!await _context.StockTransfers.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, stockTransfer, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            foreach (var line in stockTransfer.StockTransferDetails) line.StockTransferId = id;

            var j = await _context.StockTransfers.FirstOrDefaultAsync(e => e.ReferenceNo == stockTransfer.ReferenceNo && e.UserConfigId == stockTransfer.UserConfigId && e.Id != id);
            if (j != null)
            {
                return Conflict($"Stock Transfer Reference# {stockTransfer.ReferenceNo} already exist.");
            }

            stockTransfer.LastUpdatedDate = DateTime.Now;

            // Details
            foreach (var e in stockTransfer.StockTransferDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.StockTransferDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.StockTransferDetails.FindAsync(e.Id);
                        _context.StockTransferDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }

            _context.Entry(stockTransfer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockTransferExists(id))
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
        public async Task<ActionResult<StockTransferDetailDto>> PostStockTransfer(StockTransferCreateRequest request)
        {
            var stockTransfer = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (stockTransfer.UserConfigId != CompanyId) return Forbid();
            if (stockTransfer.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, stockTransfer, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var j = await _context.StockTransfers.FirstOrDefaultAsync(e => e.ReferenceNo == stockTransfer.ReferenceNo && e.UserConfigId == stockTransfer.UserConfigId);
            if (j != null)
            {
                return Conflict($"Stock Transfer Reference# {stockTransfer.ReferenceNo} already exist.");
            }

            stockTransfer.CreatedDate = DateTime.Now;

            // Details
            foreach (var e in stockTransfer.StockTransferDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            _context.StockTransfers.Add(stockTransfer);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetStockTransfer", new { id = stockTransfer.Id }, new TransactionResponseMapping().Map(stockTransfer));
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStockTransfer(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var stockTransfer = await _context.StockTransfers.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.StockTransferDetails)
                .SingleOrDefaultAsync();

            if (stockTransfer == null)
            {
                return NotFound();
            }

            // Details
            foreach (var e in stockTransfer.StockTransferDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(stockTransfer).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StockTransferExists(id))
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

        private bool StockTransferExists(int id)
        {
            return _context.StockTransfers.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }


    }
}
