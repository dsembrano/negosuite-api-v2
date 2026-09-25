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
    [Route("api/payments")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public PaymentsController(negosuiteContext context)
        {
            _context = context;
        }

        /*
        [HttpGet]
        public async Task<ActionResult> GetPayments(string criteria)
        {

            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.Payments
                .Where(e => selectCriteria.ReferenceNo != null ? e.ReferenceNo == selectCriteria.ReferenceNo : true)
                .Where(a => a.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => (selectCriteria != null && selectCriteria.SupplierId.HasValue) ? e.SupplierId == selectCriteria.SupplierId : true)
                .Where(e => (selectCriteria != null && selectCriteria.CustomerId.HasValue) ? e.CustomerId == selectCriteria.CustomerId : true)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd : true)
                .Where(c => selectCriteria != null && selectCriteria.ShowDeleted == true ? true : c.Status != GeneralJournalsController.STATUS_DELETED)
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.SupplierId,
                    SupplierName = e.Supplier.Name,
                    e.CustomerId,
                    CustomerrName = e.Customer.Name,
                    e.Payee,
                    e.PaymentModeId,
                    PaymentModeName = e.PaymentMode.Name,
                    e.PaidThroughAccountId,
                    PaidThroughAccountName = e.PaidThroughAccount.Name,
                    e.CheckNo,
                    e.Amount,
                    e.Balance,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);

        }*/



        [HttpGet]
        public async Task<ActionResult> GetPayments(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPPayments
                .FromSqlInterpolated($"CALL GetPayments({userConfigId}, {periodStart}, {periodEnd}, {supplierId}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.SupplierId,
                    SupplierName = e.SupplierName,
                    e.CustomerId,
                    CustomerrName = e.CustomerName,
                    e.Payee,
                    e.PaymentModeId,
                    PaymentModeName = e.PaymentModeName,
                    e.PaidThroughAccountId,
                    PaidThroughAccountName = e.PaidThroughAccountName,
                    e.CheckNo,
                    e.Amount,
                    e.Balance,
                    e.ResponsibilityCenterEntry,
                    e.Notes,
                    e.Status,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);

        }


        [HttpGet("{id}")]
        public async Task<ActionResult<Payment>> GetPayment(int id)
        {
            var payment = await _context.Payments.Where(e => e.Id == id)
               .Include(e => e.Supplier)
               .Include(e => e.Customer)
               .Include(e => e.PaymentMode)
               .Include(e => e.PaidThroughAccount)
               .Include(j => j.JournalEntries).ThenInclude(j => j.Account).ThenInclude(a => a.Category)
               .Include(j => j.JournalEntries).ThenInclude(j => j.Customer)
               .Include(j => j.JournalEntries).ThenInclude(j => j.Supplier)
               .Include(j => j.JournalEntries).ThenInclude(j => j.TaxRate)
               .Include(j => j.JournalEntries).ThenInclude(j => j.PaymentToJournalEntry)
               .SingleOrDefaultAsync();

            // Remove deleted entries unles main status is deleted
            if (payment.Status != GeneralJournalsController.STATUS_DELETED)
            {
                payment.JournalEntries = payment.JournalEntries.Where(j => j.Status != GeneralJournalsController.STATUS_DELETED).ToList();
            }

            if (payment == null)
            {
                return NotFound();
            }

            return payment;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutPayment(int id, Payment Payment)
        {
            if (id != Payment.Id)
            {
                return BadRequest();
            }

            var p = await _context.Payments.FirstOrDefaultAsync(p => p.ReferenceNo == Payment.ReferenceNo && p.UserConfigId == Payment.UserConfigId && p.Id != id);
            if (p != null)
            {
                return Conflict($"Payment Reference# {Payment.ReferenceNo} already exist.");
            }

            Payment.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in Payment.JournalEntries.ToList())
            {
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
        public async Task<ActionResult<Payment>> PostPayment(Payment Payment)
        {

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
            return CreatedAtAction("GetPayment", new { id = Payment.Id }, Payment);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePayment(int id)
        {
            var Payment = await _context.Payments
                .Where(e => e.Id == id)
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


        [Route("bill/{id}")]
        [HttpPut]
        public async Task<IActionResult> PutBillPayment(int id, Payment billPayment)
        {
            if (id != billPayment.Id)
            {
                return BadRequest();
            }

            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest();
            }

            billPayment.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in billPayment.JournalEntries.ToList())
            {
                e.JournalDate = billPayment.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE == null) return BadRequest("Bill payment journal entry not found.");
                }

                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.JournalEntries.Add(e);

                    // Credit AP: Subtract payment amount to bill journal entry balance
                    if (invoiceJE != null)
                    {
                        invoiceJE.Balance = invoiceJE.Balance - e.Amount;
                        invoiceJE.LastUpdatedDate = DateTime.Now;
                        _context.Entry(invoiceJE).State = EntityState.Modified;

                        var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;

                    }
                }
                else
                {
                    if (e.Deleted == true)
                    {
                        //e.Status = GeneralJournalsController.STATUS_DELETED;
                        var entry = await _context.JournalEntries.FindAsync(e.Id);
                        _context.JournalEntries.Remove(entry);

                        // Credit AP: Subtract payment amount to bill journal entry balance
                        if (invoiceJE != null)
                        {
                            invoiceJE.Balance = invoiceJE.Balance + e.Amount;
                            invoiceJE.LastUpdatedDate = DateTime.Now;
                            _context.Entry(invoiceJE).State = EntityState.Modified;

                            var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                            bill.Balance = invoiceJE.Balance;
                            bill.LastUpdatedDate = DateTime.Now;
                            _context.Entry(bill).State = EntityState.Modified;
                        }
                    } 
                    else
                    {
                        e.LastUpdatedDate = DateTime.Now;
                        _context.Entry(e).State = EntityState.Modified;

                    }

                }

            }

            _context.Entry(billPayment).State = EntityState.Modified;

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


        // Post Bill Payment
        [Route("bill")]
        [HttpPost]
        public async Task<ActionResult<Payment>> PostBillPayment(Payment billPayment)
        {
            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest("Missing configuration for Accounts Payable Trade.");
            }

            billPayment.CreatedDate = DateTime.Now;

            _context.Payments.Add(billPayment);

            foreach (var e in billPayment.JournalEntries)
            {
                e.JournalDate = billPayment.ReferenceDate;
                e.CreatedDate = DateTime.Now;

                // Debit: Subtract payment amount to bill balance
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    var billJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (billJE == null) return BadRequest("Data integrity error. Missing bill record.");
                    billJE.Balance = billJE.Balance - e.Amount;
                    billJE.LastUpdatedDate = DateTime.Now;
                    _context.Entry(billJE).State = EntityState.Modified;

                    var bill = await _context.Bills.FindAsync(billJE.BillId);
                    bill.Balance = billJE.Balance;
                    bill.LastUpdatedDate = DateTime.Now;
                    _context.Entry(bill).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetPayment", new { id = billPayment.Id }, billPayment);
        }


        [Route("bill/{id}")]
        [HttpDelete]
        public async Task<IActionResult> DeleteBillPayment(int id)
        {
            var billPayment = await _context.Payments.Where(e => e.Id == id)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (billPayment == null)
            {
                return NotFound();
            }

            var config = await _context.Configs.FirstOrDefaultAsync(e => e.Id == billPayment.UserConfigId);

            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest();
            }

            // Journal Entries
            foreach (var e in billPayment.JournalEntries.Where(e => e.Status != GeneralJournalsController.STATUS_DELETED).ToList())
            {
                e.JournalDate = billPayment.ReferenceDate;

                JournalEntry invoiceJE = null;
                if (e.AccountId == config.APTradeAccountId && e.PaymentToJournalEntryId != null)
                {
                    invoiceJE = await _context.JournalEntries.FindAsync(e.PaymentToJournalEntryId);
                    if (invoiceJE != null)
                    {
                        // Credit AP: Subtract payment amount to bill journal entry balance
                        invoiceJE.Balance = invoiceJE.Balance + e.Amount;
                        invoiceJE.LastUpdatedDate = DateTime.Now;
                        _context.Entry(invoiceJE).State = EntityState.Modified;

                        var bill = await _context.Bills.FindAsync(invoiceJE.BillId);
                        bill.Balance = invoiceJE.Balance;
                        bill.LastUpdatedDate = DateTime.Now;
                        _context.Entry(bill).State = EntityState.Modified;
                    }
                    else
                    {
                        return BadRequest("Bill payment journal entry not found.");
                    }
                }

                /*
                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;*/

                _context.Entry(e).State = EntityState.Deleted;

                //_context.JournalEntries.Remove(e);


            }

            /*
            billPayment.LastUpdatedDate = DateTime.Now;
            billPayment.Status = GeneralJournalsController.STATUS_DELETED;
            _context.Entry(billPayment).State = EntityState.Modified;*/

            _context.Entry(billPayment).State = EntityState.Deleted;

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


        private bool PaymentExists(int id)
        {
            return _context.Payments.Any(e => e.Id == id);
        }

        private static string GetStatusName(SPPayment payment)
        {
            string status = "";
            switch (payment.Status)
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
