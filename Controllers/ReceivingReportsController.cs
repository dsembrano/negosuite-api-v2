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
    [Route("api/receiving-reports")]
    [ApiController]
    public class ReceivingReportsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly ReceivingReportService service;

        [ActivatorUtilitiesConstructor]
        public ReceivingReportsController(negosuiteContext context, ReceivingReportService service)
        {
            _context = context;
            this.service = service;
        }

        public ReceivingReportsController(negosuiteContext context) : this(context, new ReceivingReportService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetReceivingReports(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!ReceivingReportQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
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
        public async Task<ActionResult<ReceivingReportDetailDto>> GetReceivingReport(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReceivingReport(int id, ReceivingReportUpdateRequest request)
        {
            var receivingReport = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (receivingReport.UserConfigId != CompanyId) return Forbid();
            if (id != receivingReport.Id) return BadRequest();
            if (!await _context.ReceivingReports.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, receivingReport, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            foreach (var line in receivingReport.ReceivingReportDetails) line.ReceivingReportId = id;
            foreach (var entry in receivingReport.JournalEntries) _context.Entry(entry).Property("ReceivingReportId").CurrentValue = id;

            var b = await _context.ReceivingReports.FirstOrDefaultAsync(e => e.ReferenceNo == receivingReport.ReferenceNo && e.UserConfigId == receivingReport.UserConfigId && e.Id != id);
            if (b != null)
            {
                return Conflict($"ReceivingReport# {receivingReport.ReferenceNo} already exist.");
            }

            receivingReport.LastUpdatedDate = DateTime.Now;


            // Invoice Details
            foreach (var e in receivingReport.ReceivingReportDetails.ToList())
            {
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.ReceivingReportDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.ReceivingReportDetails.FindAsync(e.Id);
                        _context.ReceivingReportDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }


            // Journal Entries
            foreach (var e in receivingReport.JournalEntries.ToList())
            {
                e.JournalDate = receivingReport.ReferenceDate;
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

            _context.Entry(receivingReport).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReceivingReportExists(id))
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
        public async Task<ActionResult<ReceivingReportDetailDto>> PostReceivingReport(ReceivingReportCreateRequest request)
        {
            var receivingReport = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (receivingReport.UserConfigId != CompanyId) return Forbid();
            if (receivingReport.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, receivingReport, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);

            var b = await _context.ReceivingReports.FirstOrDefaultAsync(e => e.ReferenceNo == receivingReport.ReferenceNo && e.UserConfigId == receivingReport.UserConfigId);
            if (b != null)
            {
                return Conflict($"ReceivingReport# {receivingReport.ReferenceNo} already exist.");
            }

            receivingReport.CreatedDate = DateTime.Now;

            // Details
            foreach (var e in receivingReport.ReceivingReportDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in receivingReport.JournalEntries)
            {
                e.JournalDate = receivingReport.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.ReceivingReports.Add(receivingReport);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetReceivingReport", new { id = receivingReport.Id }, new TransactionResponseMapping().Map(receivingReport));
        }

        // DELETE: api/ReceivingReports/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceivingReport(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var receivingReport = await _context.ReceivingReports.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries)
                .Include(e => e.ReceivingReportDetails)
                .SingleOrDefaultAsync();

            if (receivingReport == null)
            {
                return NotFound();
            }

            // Journal Entries
            foreach (var e in receivingReport.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            // ReceivingReport Details
            foreach (var e in receivingReport.ReceivingReportDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(receivingReport).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReceivingReportExists(id))
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

        private bool ReceivingReportExists(int id)
        {
            return _context.ReceivingReports.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }

    }
}
