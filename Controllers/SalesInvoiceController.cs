using System;
using negosuite_api.Contracts.Transactions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.SalesInvoices;
using negosuite_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [TypeFilter(typeof(TransactionIntegrityFilter), Order = 100)]
    [Route("api/sales-invoices")]
    [ApiController]
    public class SalesInvoicesController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly SalesInvoiceService invoices;

        [ActivatorUtilitiesConstructor]
        public SalesInvoicesController(negosuiteContext context, SalesInvoiceService invoices)
        {
            _context = context;
            this.invoices = invoices;
        }

        public SalesInvoicesController(negosuiteContext context) : this(context, new SalesInvoiceService(context)) { }

        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetSalesInvoices(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!SalesInvoiceQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sales invoice sortBy or sortDirection. Use a supported invoice column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            SalesInvoiceListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<SalesInvoiceListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid sales invoice criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await invoices.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesInvoiceDetailDto>> GetSalesInvoice(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var invoice = await invoices.GetAsync(CompanyId.Value, id, cancellationToken);
            return invoice == null ? NotFound() : new TransactionResponseMapping().Map(invoice);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesInvoice(int id, SalesInvoiceUpdateRequest request)
        {
            var salesInvoice = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (salesInvoice.UserConfigId != CompanyId) return Forbid();
            if (id != salesInvoice.Id)
            {
                return BadRequest();
            }

            if (!await _context.SalesInvoices.AnyAsync(i => i.Id == id && i.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await invoices.ValidateWriteAsync(CompanyId.Value, id, salesInvoice, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);

            var s = await _context.SalesInvoices.FirstOrDefaultAsync(s => s.InvoiceNo == salesInvoice.InvoiceNo && s.UserConfigId == salesInvoice.UserConfigId && s.Id != id);
            if (s != null)
            {
                return Conflict($"Sales Invoice# {salesInvoice.InvoiceNo} already exist.");
            }

            salesInvoice.LastUpdatedDate = DateTime.Now;

            if (salesInvoice.Balance < 0)
            {
                return BadRequest("Invalid amount. Payment already made to this Invoice is more than the new amount.");
            }

            // Invoice Details
            foreach (var e in salesInvoice.SalesInvoiceDetails.ToList())
            {
                e.SalesInvoiceId = id;
                // Convert date to local timezone
                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.SalesInvoiceDetails.Add(e);
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        var entry = await _context.SalesInvoiceDetails.FindAsync(e.Id);
                        _context.SalesInvoiceDetails.Remove(entry);
                    }
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;
                    }
                }
            }

            JournalEntriesController.SanitizeEntries(salesInvoice.JournalEntries);

            // Journal Entries
            foreach (var e in salesInvoice.JournalEntries.ToList())
            {
                e.SalesInvoiceId = id;
                e.JournalDate = salesInvoice.InvoiceDate;
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

            _context.Entry(salesInvoice).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesInvoiceExists(id))
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

        // POST: api/sales-Invoices
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<SalesInvoiceDetailDto>> PostSalesInvoice(SalesInvoiceCreateRequest request)
        {
            var salesInvoice = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (salesInvoice.UserConfigId != CompanyId) return Forbid();
            if (salesInvoice.Id != 0) return BadRequest("New invoice ID must be zero or omitted.");
            var validationError = await invoices.ValidateWriteAsync(CompanyId.Value, null, salesInvoice, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            // check if auto reference number ///////////////////////////////////////////////////////////
            var referenceNo = GetNextTransactionNo(salesInvoice.UserConfigId);
            if (referenceNo != null) salesInvoice.InvoiceNo = referenceNo;

            var s = await _context.SalesInvoices.FirstOrDefaultAsync(s => s.InvoiceNo == salesInvoice.InvoiceNo && s.UserConfigId == salesInvoice.UserConfigId);
            if (s != null)
            {
                return Conflict($"Sales Invoice# {salesInvoice.InvoiceNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(salesInvoice.JournalEntries);
            salesInvoice.CreatedDate = DateTime.Now;

            // Details
            foreach (var e in salesInvoice.SalesInvoiceDetails)
            {
                e.CreatedDate = DateTime.Now;
            }

            foreach (var e in salesInvoice.JournalEntries)
            {
                e.ReferenceNo = salesInvoice.InvoiceNo;
                e.JournalDate = salesInvoice.InvoiceDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.SalesInvoices.Add(salesInvoice);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSalesInvoice", new { id = salesInvoice.Id }, new TransactionResponseMapping().Map(salesInvoice));
        }

        // DELETE: api/sales-invoices/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesInvoice(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var salesInvoice = await _context.SalesInvoices.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries).Include(e => e.SalesInvoiceDetails)
                .SingleOrDefaultAsync();

            if (salesInvoice == null)
            {
                return NotFound();
            }

            if (salesInvoice.Balance < salesInvoice.Amount)
            {
                return BadRequest("Can not delete this Invoice because payment was already applied.");
            }

            // Journal Entries
            foreach (var e in salesInvoice.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            // Sales Invoice Details
            foreach (var e in salesInvoice.SalesInvoiceDetails.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(salesInvoice).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesInvoiceExists(id))
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

        private bool SalesInvoiceExists(int id)
        {
            return _context.SalesInvoices.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }


        private string GetNextTransactionNo(int userConfigId)
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
            if (autoReferenceNoConfig.AutoSIReferenceNo == false)
            {
                return null;
            }

            // Number allocation participates in the enclosing company transaction.

            var sequence = _context.TransactionSequences.FirstOrDefault(e => e.UserConfigId == userConfigId && e.Source == "SI");

            if (sequence == null)
            {
                sequence = new TransactionSequence
                {
                    UserConfigId = userConfigId,
                    Source = "SI",
                    LastSequence = 0
                };
                _context.TransactionSequences.Add(sequence);
            }

            sequence.LastSequence++;
            _context.SaveChanges();


            var formattedSequence = AutoReferenceNoConfig.getFormattedSequenceNo(sequence.LastSequence, autoReferenceNoConfig.AutoSIReferenceNoFormat);

            return $"{autoReferenceNoConfig.AutoSIReferenceNoPrefix}{formattedSequence}";
        }

    }


    public class Tax
    {
        public decimal Amount { get; set; }
        public TaxRate TaxRate { get; set; }
    }

    public class Taxes
    {
        //public 
    }


}
