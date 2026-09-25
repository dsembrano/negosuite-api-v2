using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/financial-reports")]
    [ApiController]
    public class FinancialReportsController : ControllerBase
    {       
        private readonly negosuiteContext _context;

        public FinancialReportsController(negosuiteContext context)
        {
            _context = context;
        }


        [Route("journal-transactions-rc")]
        [HttpGet]
        public async Task<ActionResult> GetJournalTransactionsByRC(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var journalSource = (selectCriteria.JournalSource != null) ? selectCriteria.JournalSource : "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var accountId = (selectCriteria.AccountId != null) ? selectCriteria.AccountId.ToString() : "";
            var arrayString = selectCriteria.ArrayString;

            var result = await _context.ResponsibilityCenterJournalEntries
                .FromSqlInterpolated($"CALL GetJournalEntryByResponsibilityCenter({userConfigId}, {periodStart}, {periodEnd}, {journalSource}, {customerId}, {supplierId}, {arrayString}, {accountId})")
                .ToListAsync();

            return Ok(result);
        }


        [Route("journal-transactions")]
        [HttpGet]
        public async Task<ActionResult> GetJournalTransactions(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await ( from j in _context.JournalEntries
                                 join payment in _context.Payments on j.PaymentId equals payment.Id into payments
                                 from payment in payments.DefaultIfEmpty()
                                 where (j.UserConfigId == selectCriteria.UserConfigId) &&
                                    (j.JournalDate >= selectCriteria.PeriodStart && j.JournalDate <= selectCriteria.PeriodEnd && j.Status == GeneralJournalsController.STATUS_POSTED) &&
                                    (selectCriteria.JournalSource != null ? j.Source == selectCriteria.JournalSource : true) &&
                                    (selectCriteria.JournalSources != null ? selectCriteria.JournalSources.Contains(j.Source) : true) &&
                                    (selectCriteria.AccountId != null ? selectCriteria.AccountId == j.AccountId : true) &&
                                    (selectCriteria.CustomerId != null ? selectCriteria.CustomerId == j.CustomerId : true) &&
                                    (selectCriteria.SupplierId != null ? selectCriteria.SupplierId == j.SupplierId : true)
                                 select new 
                        {
                            j.Id,
                            j.JournalDate,
                            j.ReferenceNo,
                            j.AccountId,
                            AccountName = j.Account.Name,
                            j.CustomerId,
                            CustomerName = j.Customer != null ? j.Customer.Name : "",
                            j.SupplierId,
                            SupplierName = j.Supplier != null ? j.Supplier.Name : "",
                            j.Nature,
                            Debit = j.Nature == "D" ? j.Amount : 0,
                            Credit = j.Nature == "C" ? j.Amount : 0,
                            j.Amount,
                            j.Balance,
                            j.Source,
                            j.ResponsibilityCenterEntry,
                            j.Notes,
                            j.Particular,
                            j.Payee,
                            Payor = (j.SalesInvoicePaymentId != null || j.SalesReceiptId != null) ? j.Customer.Name : "",
                            CheckNo = payment.CheckNo,
                            SourceName = FinancialReportsController.GetJournalSourceName(j.Source),
                        }).OrderBy(e => e.JournalDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);

        }


        /*
        [Route("trial-balance")]
        [HttpGet]
        public async Task<ActionResult> GetAccountRecap(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await (
                from e in _context.JournalEntries
                where (e.UserConfigId == selectCriteria.UserConfigId) &&
                    (selectCriteria.PeriodStart != null ? e.JournalDate >= selectCriteria.PeriodStart : true) && 
                    e.JournalDate <= selectCriteria.PeriodEnd && e.Status == GeneralJournalsController.STATUS_POSTED
                group e by e.AccountId into g
                select new
                {
                    g.First().AccountId,
                    AccountCode = g.First().Account.Code,
                    AccountName = g.First().Account.Name,
                    AccountType = g.First().Account.Category.Type,
                    CategoryId = g.First().Account.Category.Id,
                    CategoryName = g.First().Account.Category.Name,
                    CategoryOrderNo = g.First().Account.Category.OrderNo,
                    CategoryAccountPrefix = g.First().Account.Category.AccountCodePrefix,
                    Debit = g.Where(e => e.Nature == "D").Sum(e => e.Amount),
                    Credit = g.Where(e => e.Nature == "C").Sum(e => e.Amount)
                }
                ).OrderBy(e => e.CategoryOrderNo).ThenBy(e => e.AccountCode).ToListAsync();

            return Ok(result);
        }*/


        [Route("trial-balance")]
        [HttpGet]
        public async Task<ActionResult> GetAccountRecap(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");

            var result = await _context.AccountRecaps
                .FromSqlInterpolated($"CALL GetAccountRecap({userConfigId}, {periodEnd})")
                .ToListAsync();

            return Ok(result);

        }


        [Route("gl-summary")]
        [HttpGet]
        public async Task<ActionResult> GetGeneralLedgerSummary(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");

            var result = await _context.GeneralLedgerRecaps
                .FromSqlInterpolated($"CALL GetGeneralLedgerRecap({userConfigId}, {periodStart}, {periodEnd})")
                .ToListAsync();

            return Ok(result);

        }


        [Route("gl-details")]
        [HttpGet]
        public async Task<ActionResult> GetGeneralLedgerDetails(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var accountId = selectCriteria.AccountId.ToString();

            var result = await _context.GeneralLedgerDetails
                .FromSqlInterpolated($"CALL GetGeneralLedgerDetails({userConfigId}, {accountId}, {periodStart}, {periodEnd})")
                .ToListAsync();

            result.ForEach(r =>
            {
                r.SourceName = FinancialReportsController.GetJournalSourceName(r.Source);
            });

            return Ok(result);

        }


        /*
        [Route("balance-sheet")]
        [HttpGet]
        public async Task<ActionResult> GetAccountRecapBS(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await (
                from e in _context.JournalEntries
                where e.JournalDate <= selectCriteria.PeriodEnd && e.Status == GeneralJournalsController.STATUS_POSTED 
                    && (e.Account.Category.Type == "Asset" || e.Account.Category.Type == "Liability" || e.Account.Category.Type == "Equity")
                group e by e.AccountId into g
                select new
                {
                    g.First().AccountId,
                    AccountCode = g.First().Account.Code,
                    AccountName = g.First().Account.Name,
                    AccountType = g.First().Account.Category.Type,
                    CategoryId = g.First().Account.Category.Id,
                    CategoryName = g.First().Account.Category.Name,
                    CategoryOrderNo = g.First().Account.Category.OrderNo,
                    CategoryAccountPrefix = g.First().Account.Category.AccountCodePrefix,
                    Debit = g.Where(e => e.Nature == "D").Sum(e => e.Amount),
                    Credit = g.Where(e => e.Nature == "C").Sum(e => e.Amount)
                }
                ).OrderBy(e => e.CategoryOrderNo).ThenBy(e => e.AccountCode).ToListAsync();

            return Ok(result);
        }*/


        [Route("income-statement")]
        [HttpGet]
        public async Task<ActionResult> GetAccountRecapPL(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            /*
            var result = await (
                from e in _context.JournalEntries
                where (e.JournalDate >= selectCriteria.PeriodStart && e.JournalDate <= selectCriteria.PeriodEnd) 
                    && e.Status == GeneralJournalsController.STATUS_POSTED
                    && (e.Account.Category.Type == "Income" || e.Account.Category.Type == "Expense")
                group e by e.AccountId into g
                select new
                {
                    g.First().AccountId,
                    AccountCode = g.First().Account.Code,
                    AccountName = g.First().Account.Name,
                    AccountType = g.First().Account.Category.Type,
                    CategoryId = g.First().Account.Category.Id,
                    CategoryName = g.First().Account.Category.Name,
                    CategoryOrderNo = g.First().Account.Category.OrderNo,
                    CategoryAccountPrefix = g.First().Account.Category.AccountCodePrefix,
                    Debit = g.Where(e => e.Nature == "D").Sum(e => e.Amount),
                    Credit = g.Where(e => e.Nature == "C").Sum(e => e.Amount)
                }
                ).OrderBy(e => e.CategoryOrderNo).ThenBy(e => e.AccountCode).ToListAsync();
            */

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var arrayString = selectCriteria.ArrayString;

            var result = await _context.JournalEntrySummaries
                .FromSqlInterpolated($"CALL GetJournalEntryRecapIS({userConfigId}, {periodStart}, {periodEnd}, {arrayString})")
                .ToListAsync();

            return Ok(result);

        }

        public static string GetJournalSourceName(string source)
        {
            string name = "";
            switch (source)
            {
                case "GJ":
                    name = "General Journal";
                    break;
                case "SI":
                    name = "Sales Invoice";
                    break;
                case "SR":
                    name = "Cash Invoice";
                    break;
                case "PR":
                    name = "Payment Received";
                    break;
                case "PU":
                    name = "Purchase";
                    break;
                case "BP":
                    name = "Bill Payment";
                    break;
                case "OP":
                    name = "Others Payment";
                    break;
                case "PV":
                    name = "Payment Voucher";
                    break;
                case "IA":
                    name = "Inventory Adjustment";
                    break;
                case "II":
                    name = "Stock Issuance";
                    break;
                case "RR":
                    name = "Receiving Report";
                    break;
            }
            return name;
        }

    }

}
