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
    [Route("api/journal-entries")]
    [ApiController]
    public class JournalEntriesController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public JournalEntriesController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/JournalEntries
        [Route("unpaid-invoices")]
        [HttpGet]
        public async Task<ActionResult> GetUnpaidInvoices(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var arTradeAccountId = selectCriteria.AccountId;

            /*
            var config = await _context.Configs.FirstOrDefaultAsync();
            if (config == null || config.ARTradeAccountId == null)
            {
                return BadRequest();
            }*/
           
            var result = await _context.JournalEntries
                .Where(e => e.Nature == "D" && e.AccountId == arTradeAccountId && e.Balance > 0)
                .Where(e => e.CustomerId == selectCriteria.CustomerId)
                .Where(e => (selectCriteria.PeriodStart != null ?  e.JournalDate >= selectCriteria.PeriodStart : true ) )
                .Where(e => (selectCriteria.PeriodEnd != null ? e.JournalDate <= selectCriteria.PeriodEnd : true) )
                .Select(e => new
                {
                    JournalEntryId = e.Id,
                    InvoiceNo = e.ReferenceNo,
                    InvoiceDate = e.JournalDate,
                    e.DueDate,
                    e.Amount,
                    e.Balance
                }).OrderBy(e => e.InvoiceDate).ThenBy(e => e.InvoiceNo).ToListAsync();

            return Ok(result);
        }


        // GET: api/JournalEntries
        [Route("unpaid-bills")]
        [HttpGet]
        public async Task<ActionResult> GetUnpaidBills(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var apTradeAccountId = selectCriteria.AccountId;

            /*
            var config = await _context.Configs.FirstOrDefaultAsync();
            if (config == null || config.APTradeAccountId == null)
            {
                return BadRequest();
            }*/

            var result = await _context.JournalEntries
                .Where(e => e.Nature == "C" && e.AccountId == apTradeAccountId && e.Balance > 0)
                .Where(e => e.SupplierId == selectCriteria.SupplierId)
                .Where(e => (selectCriteria.PeriodStart != null ? e.JournalDate >= selectCriteria.PeriodStart : true))
                .Where(e => (selectCriteria.PeriodEnd != null ? e.JournalDate <= selectCriteria.PeriodEnd : true))
                .Select(e => new
                {
                    JournalEntryId = e.Id,
                    BillNo = e.ReferenceNo,
                    BillDate = e.JournalDate,
                    e.DueDate,
                    e.Amount,
                    e.Balance
                }).OrderBy(e => e.BillDate).ThenBy(e => e.BillNo).ToListAsync();

            return Ok(result);
        }


        [AllowAnonymous]
        [Route("unapplied-ar-credits")]
        [HttpGet]
        public async Task<ActionResult> GetUnAppliedARCredits(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FirstOrDefaultAsync(c => c.Id == selectCriteria.UserConfigId);
            //var arTradeAccountId = selectCriteria.AccountId;
            var arTradeAccountId = config.ARTradeAccountId;

            var result = await _context.JournalEntries
                .Where(e => e.Nature == "C" && e.AccountId == arTradeAccountId && e.Balance > 0)
                .Where(e => (selectCriteria.CustomerId != null) ? e.CustomerId == selectCriteria.CustomerId : true)
                .Where(e => (selectCriteria.PeriodStart != null ? e.JournalDate >= selectCriteria.PeriodStart : true))
                .Where(e => (selectCriteria.PeriodEnd != null ? e.JournalDate <= selectCriteria.PeriodEnd : true))
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    ReferenceDate = e.JournalDate,
                    e.CustomerId,
                    CustomerName = e.Customer.Name,
                    e.DueDate,
                    e.Amount,
                    e.Balance,
                    e.Source,
                    SourceName = FinancialReportsController.GetJournalSourceName(e.Source)
                }).OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);
        }


        [AllowAnonymous]
        [Route("unapplied-ar-credits/{id}")]
        [HttpGet]
        public async Task<ActionResult> GetUnAppliedARCredit(int id)
        {

            var result = await _context.JournalEntries.Where(j => j.Id == id)
                .Include(j => j.Account)
                .Include(j => j.Customer)
                .Select(e => new
                {
                    e.Id,
                    e.ReferenceNo,
                    ReferenceDate = e.JournalDate,
                    e.Customer,
                    e.CustomerId,
                    CustomerName = e.Customer.Name,
                    e.DueDate,
                    e.Amount,
                    e.Balance,
                    e.Source,
                    SourceName = FinancialReportsController.GetJournalSourceName(e.Source)
                }).FirstOrDefaultAsync();

            return Ok(result);
        }


        private bool JournalEntryExists(int id)
        {
            return _context.JournalEntries.Any(e => e.Id == id);
        }

        public static void SanitizeEntries(ICollection<JournalEntry> journalEntries)
        {
            journalEntries.ToList().ForEach(e =>
            {
                e.Amount = Math.Round(e.Amount, 2);
                e.Balance = Math.Round(e.Balance, 2);
            });
        }

    }
}
