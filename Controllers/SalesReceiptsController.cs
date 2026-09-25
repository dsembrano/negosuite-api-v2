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
    [Route("api/sales-receipts")]
    [ApiController]
    public class SalesReceiptsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public SalesReceiptsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/SalesReceipts
        /*
        [HttpGet]
        public async Task<ActionResult> GetSalesReceipts(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.SalesReceipts
                .Where(e => selectCriteria.ReferenceNo != null ? e.ReceiptNo == selectCriteria.ReferenceNo : true)
                .Where(e => selectCriteria.UserConfigId.HasValue ? e.UserConfigId == selectCriteria.UserConfigId : true)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.ReceiptDate >= selectCriteria.PeriodStart && e.ReceiptDate <= selectCriteria.PeriodEnd : true)
                .Where(e => selectCriteria.CustomerId.HasValue ? e.CustomerId == selectCriteria.CustomerId : true)
                .Where(e => selectCriteria.ShowDeleted == true ? true : e.Status != GeneralJournalsController.STATUS_DELETED)
               .Select(e => new
               {
                   e.Id,
                   e.ReceiptNo,
                   e.ReceiptDate,
                   e.CustomerId,
                   CustomerName = e.Customer.Name,
                   //e.Customer,
                   e.Amount,
                   e.Balance,
                   e.PaymentModeId,
                   PaymentModeName = e.PaymentMode.Name,
                   e.PaymentMode,
                   e.Status,
                   StatusName = GetStatusName(e)
               }).OrderBy(e => e.ReceiptDate).ThenBy(e => e.ReceiptNo).ToListAsync();

            return Ok(result);
        }*/


        [HttpGet]
        public async Task<ActionResult> GetSalesReceipts(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;
            var isPOS = (selectCriteria.IsPOS == true) ? "1" : "";

            var list = await _context.SPSalesReceipts
                .FromSqlInterpolated($"CALL GetSalesReceipts({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {referenceNo}, {arrayString}, {isPOS})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.ReceiptNo,
                    e.ReceiptDate,
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
                    e.PaymentModeId,
                    e.PaymentModeName,
                    e.Notes,
                    e.Status,
                    e.ResponsibilityCenterEntry,
                    e.CreatedDate,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReceiptDate).ThenBy(e => e.ReceiptNo).ToList();

            return Ok(result);
        }

        // GET: api/SalesReceipts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<SalesReceipt>> GetSalesReceipt(int id)
        {
            var salesReceipt = await _context.SalesReceipts.Where(e => e.Id == id)
                .Include(e => e.Customer).ThenInclude(e => e.CustomerAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
                .Include(e => e.Customer).ThenInclude(e => e.CustomerContacts)
                .Include(e => e.PaymentMode)
                .Include(e => e.DepositToAccount)
                .Include(e => e.SalesReceiptDetails).ThenInclude(e => e.Item).ThenInclude(e => e.SalesTaxRate)
                .Include(e => e.SalesReceiptDetails).ThenInclude(e => e.TaxRate)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.InventoryLocation)
                .SingleOrDefaultAsync();

            if (salesReceipt == null)
            {
                return NotFound();
            }

            return salesReceipt;
        }

        // PUT: api/SalesReceipts/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSalesReceipt(int id, SalesReceipt salesReceipt)
        {
            if (id != salesReceipt.Id)
            {
                return BadRequest();
            }

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
            var salesReceipt = await _context.SalesReceipts.Where(e => e.Id == id)
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
            return _context.SalesReceipts.Any(e => e.Id == id);
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

        private static string GetStatusName(SPSalesReceipt receipt)
        {
            string status = "";
            switch (receipt.Status)
            {
                case -1:
                    status = "Deleted";
                    break;
                case 0:
                    status = "Draft";
                    break;
                case 1:
                    status = "Posted";
                    break;
            }
            return status;
        }
    }
}
