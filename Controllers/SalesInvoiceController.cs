using System;
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
    [Route("api/sales-invoices")]
    [ApiController]
    public class SalesInvoicesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public SalesInvoicesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/sales-invoices
        /*
        [HttpGet]
        public async Task<ActionResult> GetSalesInvoices(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.SalesInvoices
                .Where(e => selectCriteria.ReferenceNo != null ? e.InvoiceNo == selectCriteria.ReferenceNo : true)
                .Where(e => selectCriteria.UserConfigId.HasValue ? e.UserConfigId == selectCriteria.UserConfigId : true)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.InvoiceDate >= selectCriteria.PeriodStart && e.InvoiceDate <= selectCriteria.PeriodEnd : true)
                .Where(e => selectCriteria.CustomerId.HasValue ? e.CustomerId == selectCriteria.CustomerId : true)
                .Where(e => selectCriteria.ShowDeleted == true ? true : e.Status != GeneralJournalsController.STATUS_DELETED)
                .Select(e => new
                {
                    e.Id,
                    e.InvoiceNo,
                    e.InvoiceDate,
                    e.DueDate,
                    e.CustomerId,
                    CustomerName = e.Customer.Name,
                    e.Customer,
                    e.Amount,
                    e.Balance,
                    e.PaymentTermId,
                    PaymentTermName = e.PaymentTerm.Name,
                    e.PaymentTerm,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.InvoiceDate).ThenBy(e => e.InvoiceNo).ToListAsync();

            return Ok(result);
        }*/


        [HttpGet]
        public async Task<ActionResult> GetSalesInvoices(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPSalesInvoices
                .FromSqlInterpolated($"CALL GetSalesInvoices({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {supplierId}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.InvoiceNo,
                    e.InvoiceDate,
                    e.DueDate,
                    e.PurchaseOrderNo,
                    e.CustomerId,
                    e.CustomerName,
                    e.CustomerTIN,
                    e.BillingAddress,
                    e.BillingContactName,
                    e.BillingContactEmail,
                    e.ShippingAddress,
                    e.ShippingContactName,
                    e.ShippingContactEmail,
                    e.Amount,
                    e.Balance,
                    e.PaymentTermId,
                    e.PaymentTermName,
                    e.Notes,
                    e.Status,
                    StatusName = GetStatusName(e),
                    e.ResponsibilityCenterEntry
                }).OrderBy(e => e.InvoiceDate).ThenBy(e => e.InvoiceNo).ToList();

            return Ok(result);
        }



        // GET: api/sales-invoices/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SalesInvoice>> GetSalesInvoice(int id)
        {
            var salesInvoice = await _context.SalesInvoices.Where(e => e.Id == id)
                .Include(e => e.Customer).ThenInclude(e => e.CustomerAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
                .Include(e => e.Customer).ThenInclude(e => e.CustomerContacts)
                .Include(e => e.Supplier)
                .Include(e => e.PaymentTerm)
                .Include(e => e.SalesInvoiceDetails).ThenInclude(e => e.Item).ThenInclude(e => e.SalesTaxRate)
                .Include(e => e.SalesInvoiceDetails).ThenInclude(e => e.TaxRate)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.InventoryLocation)
                .SingleOrDefaultAsync();

            if (salesInvoice == null)
            {
                return NotFound();
            }

            return salesInvoice;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesInvoice(int id, SalesInvoice salesInvoice)
        {
            if (id != salesInvoice.Id)
            {
                return BadRequest();
            }

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
        public async Task<ActionResult<SalesInvoice>> PostSalesInvoice(SalesInvoice salesInvoice)
        {
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

            return CreatedAtAction("GetSalesInvoice", new { id = salesInvoice.Id }, salesInvoice);
        }

        // DELETE: api/sales-invoices/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesInvoice(int id)
        {
            var salesInvoice = await _context.SalesInvoices.Where(e => e.Id == id)
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
                /*
                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;*/
                _context.Entry(e).State = EntityState.Deleted;
            }

            // Sales Invoice Details
            foreach (var e in salesInvoice.SalesInvoiceDetails.ToList())
            {
                /*
                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;*/
                _context.Entry(e).State = EntityState.Deleted;
            }

            /*
            salesInvoice.Status = GeneralJournalsController.STATUS_DELETED;
            salesInvoice.LastUpdatedDate = DateTime.Now;
            _context.Entry(salesInvoice).State = EntityState.Modified;*/
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

        private static string GetStatusName(SPSalesInvoice invoice)
        {
            string status = "";
            switch (invoice.Status)
            {
                case -1:
                    status = "Deleted";
                    break;
                case 0:
                    status = "Draft";
                    break;
                case 1:
                    if (invoice.Balance == 0) status = "Paid";
                    if (invoice.Balance < invoice.Amount && invoice.Balance > 0) status = "Partially paid";
                    if (invoice.Balance == invoice.Amount && invoice.DueDate == DateTime.Now.Date) status = "Due today";
                    if (invoice.Balance == invoice.Amount && invoice.DueDate > DateTime.Now.Date) status = $"Due in {(invoice.DueDate - DateTime.Now.Date).Days} days";
                    if (invoice.Balance == invoice.Amount && invoice.DueDate < DateTime.Now.Date) status = $"{(DateTime.Now.Date - invoice.DueDate).Days} days overdue";
                    break;
            }
            return status;
        }

        private bool SalesInvoiceExists(int id)
        {
            return _context.SalesInvoices.Any(e => e.Id == id);
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

            using var transaction = _context.Database.BeginTransaction();

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
            transaction.Commit();

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
