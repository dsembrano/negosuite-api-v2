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
    [Route("api/expense-payments")]
    [ApiController]
    public class ExpensePaymentsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public ExpensePaymentsController(negosuiteContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult> GetExpensePayments(string criteria)
        {

            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.ExpensePayments
                .Where(e => (selectCriteria != null && selectCriteria.SupplierId.HasValue) ? e.SupplierId == selectCriteria.SupplierId : true)
                .Where(e => (selectCriteria != null && selectCriteria.CustomerId.HasValue) ? e.CustomerId == selectCriteria.CustomerId : true)
                .Where(e => e.ReferenceDate >= (
                    selectCriteria != null && selectCriteria.PeriodStart != null ? selectCriteria.PeriodStart : e.ReferenceDate))
                .Where(e => e.ReferenceDate <= (
                    selectCriteria != null && selectCriteria.PeriodEnd != null ? selectCriteria.PeriodEnd : e.ReferenceDate))
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

        }

        // GET: api/BillPayments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ExpensePayment>> GetExpensePayment(int id)
        {
            var expensePayment = await _context.ExpensePayments
               .Where(e => e.Id == id)
               .Include(e => e.Supplier)
               .Include(e => e.Customer)
               .Include(e => e.PaymentMode)
               .Include(e => e.PaidThroughAccount)               
               .Include(j => j.JournalEntries).ThenInclude(j => j.Account).ThenInclude(a => a.Category)
               .Include(j => j.JournalEntries).ThenInclude(j => j.Customer)
               .Include(j => j.JournalEntries).ThenInclude(j => j.Supplier)
               .Include(j => j.JournalEntries).ThenInclude(j => j.TaxRate)
               .SingleOrDefaultAsync();

            // Remove deleted entries unles main status is deleted
            if ( expensePayment.Status != GeneralJournalsController.STATUS_DELETED)
            {
                expensePayment.JournalEntries = expensePayment.JournalEntries.Where(j => j.Status != GeneralJournalsController.STATUS_DELETED).ToList();
            }
            
            if (expensePayment == null)
            {
                return NotFound();
            }

            return expensePayment;
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> PutExpensePayment(int id, ExpensePayment expensePayment)
        {
            if (id != expensePayment.Id)
            {
                return BadRequest();
            }

            JournalEntriesController.SanitizeEntries(expensePayment.JournalEntries);
            expensePayment.LastUpdatedDate = DateTime.Now;

            // Journal Entries
            foreach (var e in expensePayment.JournalEntries.ToList())
            {
                e.JournalDate = expensePayment.ReferenceDate;

                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.JournalEntries.Add(e);
                }
                else
                {
                    if (e.Deleted == true) e.Status = GeneralJournalsController.STATUS_DELETED;
                    e.LastUpdatedDate = DateTime.Now;
                    _context.Entry(e).State = EntityState.Modified;
                }
            }

            _context.Entry(expensePayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ExpensePaymentExists(id))
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
        public async Task<ActionResult<ExpensePayment>> PostExpensePayment(ExpensePayment expensePayment)
        {
            expensePayment.CreatedDate = DateTime.Now;

            JournalEntriesController.SanitizeEntries(expensePayment.JournalEntries);
            _context.ExpensePayments.Add(expensePayment);

            foreach (var e in expensePayment.JournalEntries)
            {
                e.JournalDate = expensePayment.ReferenceDate;
                e.CreatedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetExpensePayment", new { id = expensePayment.Id }, expensePayment);
        }


        // DELETE: api/BillPayments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpensePayment(int id)
        {
            var expensePayment = await _context.ExpensePayments
                .Where(e => e.Id == id)
                .Include(e => e.JournalEntries).SingleOrDefaultAsync();

            if (expensePayment == null)
            {
                return NotFound();
            }

            // Journal Entries
            foreach (var e in expensePayment.JournalEntries.ToList())
            {
                e.Status = GeneralJournalsController.STATUS_DELETED;
                e.LastUpdatedDate = DateTime.Now;
                _context.Entry(e).State = EntityState.Modified;
            }

            expensePayment.Status = GeneralJournalsController.STATUS_DELETED;
            expensePayment.LastUpdatedDate = DateTime.Now;
            _context.Entry(expensePayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ExpensePaymentExists(id))
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


        private bool ExpensePaymentExists(int id)
        {
            return _context.ExpensePayments.Any(e => e.Id == id);
        }

        private static string GetStatusName(ExpensePayment payment)
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
