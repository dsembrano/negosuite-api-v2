using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.SalesReceipts;
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
    [Route("api/sales-receipts")]
    [ApiController]
    public class SalesReceiptsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly SalesReceiptService service;

        [ActivatorUtilitiesConstructor]
        public SalesReceiptsController(negosuiteContext context, SalesReceiptService service)
        {
            _context = context;
            this.service = service;
        }
        public SalesReceiptsController(negosuiteContext context) : this(context, new SalesReceiptService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetSalesReceipts(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!SalesReceiptQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            SalesReceiptListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<SalesReceiptListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await service.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesReceipt>> GetSalesReceipt(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesReceipt(int id, SalesReceipt salesReceipt)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (salesReceipt.UserConfigId != CompanyId) return Forbid();
            if (id != salesReceipt.Id)
            {
                return BadRequest();
            }

            if (!await _context.SalesReceipts.AnyAsync(e => e.Id == id && e.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, salesReceipt, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var s = await _context.SalesReceipts.FirstOrDefaultAsync(s => s.ReceiptNo == salesReceipt.ReceiptNo && s.UserConfigId == salesReceipt.UserConfigId && s.Id != id);
            if (s != null)
            {
                return Conflict($"Sales Receipt# {salesReceipt.ReceiptNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(salesReceipt.JournalEntries);
            salesReceipt.LastUpdatedDate = DateTime.Now;

            // Sales Receipt Details
            foreach (var e in salesReceipt.SalesReceiptDetails.ToList())
            {
                e.SalesReceiptId = id;
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.SalesReceiptDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.SalesReceiptDetails.FindAsync(e.Id);
                        _context.SalesReceiptDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }


            // Journal Entries
            foreach (var e in salesReceipt.JournalEntries.ToList())
            {
                e.SalesReceiptId = id;
                e.JournalDate = salesReceipt.ReceiptDate;
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

            _context.Entry(salesReceipt).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesReceiptExists(id))
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
        public async Task<ActionResult<SalesReceipt>> PostSalesReceipt(SalesReceipt salesReceipt)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (salesReceipt.UserConfigId != CompanyId) return Forbid();
            if (salesReceipt.Id != 0) return BadRequest("New transaction ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, salesReceipt, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            // check for auto reference number ///////////////////////////////////////////////////////////
            // if returned is null then not auto generated
            var referenceNo = GetNextTransactionNo(salesReceipt.UserConfigId, salesReceipt.IsPOS ?? false);
            if (referenceNo != null) salesReceipt.ReceiptNo = referenceNo;

            var s = await _context.SalesReceipts.FirstOrDefaultAsync(s => s.ReceiptNo == salesReceipt.ReceiptNo && s.UserConfigId == salesReceipt.UserConfigId);
            if (s != null)
            {
                return Conflict($"Sales Receipt# {salesReceipt.ReceiptNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(salesReceipt.JournalEntries);
            salesReceipt.CreatedDate = DateTime.Now;

            // Details
            foreach (var e in salesReceipt.SalesReceiptDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in salesReceipt.JournalEntries)
            {
                e.ReferenceNo = salesReceipt.ReceiptNo;
                e.JournalDate = salesReceipt.ReceiptDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.SalesReceipts.Add(salesReceipt);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSalesReceipt", new { id = salesReceipt.Id }, salesReceipt);
        }

        // DELETE: api/SalesReceipts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesReceipt(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var salesReceipt = await _context.SalesReceipts.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries).Include(e => e.SalesReceiptDetails)
                .SingleOrDefaultAsync();

            if (salesReceipt == null)
            {
                return NotFound();
            }

            // Journal Entries
            foreach (var e in salesReceipt.JournalEntries.ToList())
            {
                //e.Status = GeneralJournalsController.STATUS_DELETED;
                //e.LastUpdatedDate = DateTime.Now;
                //_context.Entry(e).State = EntityState.Modified;
                _context.Entry(e).State = EntityState.Deleted;
            }

            // Sales Invoice Details
            foreach (var e in salesReceipt.SalesReceiptDetails.ToList())
            {
                //e.Status = GeneralJournalsController.STATUS_DELETED;
                //e.LastUpdatedDate = DateTime.Now;
                //_context.Entry(e).State = EntityState.Modified;
                _context.Entry(e).State = EntityState.Deleted;
            }

            //salesReceipt.Status = GeneralJournalsController.STATUS_DELETED;
            //salesReceipt.LastUpdatedDate = DateTime.Now;
            //_context.Entry(salesReceipt).State = EntityState.Modified;
            _context.Entry(salesReceipt).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesReceiptExists(id))
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

        private bool SalesReceiptExists(int id)
        {
            return _context.SalesReceipts.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }


        private string GetNextTransactionNo(int userConfigId, bool? isPOS)
        {
            var config = _context.Configs.Where(e => e.Id == userConfigId).FirstOrDefault();
            AutoReferenceNoConfig autoReferenceNoConfig = new AutoReferenceNoConfig();

            if (String.IsNullOrEmpty(config.AutoReferenceNoConfig))
            {
                string configStr = @$"{{
                    ""autoSIReferenceNo"": false,
                    ""autoSRReferenceNo"": false,
                    ""autoPOSReferenceNo"": true,
                    ""autoSIReferenceNoFormat"": """",
                    ""autoSIReferenceNoPrefix"": """",
                    ""autoSRReferenceNoFormat"": """",
                    ""autoSRReferenceNoPrefix"": """",
                    ""autoPOSReferenceNoFormat"": ""########"",
                    ""autoPOSReferenceNoPrefix"": ""POS""
                }}";
                config.AutoReferenceNoConfig = System.Text.Json.JsonSerializer.Serialize(autoReferenceNoConfig);
                config.AutoReferenceNoConfig = configStr;
                _context.Entry(config).State = EntityState.Modified;
            }

            autoReferenceNoConfig = JsonConvert.DeserializeObject<AutoReferenceNoConfig>(config.AutoReferenceNoConfig);

            // If module is Sales Receipt (Cash Invoice) and auto reference is false, then return null
            if (autoReferenceNoConfig.AutoSRReferenceNo == false && isPOS == false)
            {
                return null;
            }

            using var transaction = _context.Database.BeginTransaction();

            var sequence = _context.TransactionSequences.FirstOrDefault(e => e.UserConfigId == userConfigId && e.Source == (isPOS == true ? "POS" : "SR"));

            if (sequence == null)
            {
                sequence = new TransactionSequence
                {
                    UserConfigId = userConfigId,
                    Source = isPOS == true ? "POS" : "SR",
                    LastSequence = 0
                };
                _context.TransactionSequences.Add(sequence);
            }

            sequence.LastSequence++;
            _context.SaveChanges();
            transaction.Commit();

            var formattedSequence = AutoReferenceNoConfig.getFormattedSequenceNo(sequence.LastSequence, (isPOS == true ? autoReferenceNoConfig.AutoPOSReferenceNoFormat : autoReferenceNoConfig.AutoSRReferenceNoFormat));

            return $"{(isPOS == true ? autoReferenceNoConfig.AutoPOSReferenceNoPrefix : autoReferenceNoConfig.AutoSRReferenceNoPrefix)}{formattedSequence}";
        }

    }
}
