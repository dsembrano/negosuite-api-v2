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
    [Route("api/receivable-reports")]
    [ApiController]
    public class ReceivableReportsController : ControllerBase
    {
        private static short AGING_BY_INVOICE_DATE = 1;
        private static short AGING_BY_INVOICE_DUE_DATE = 2;

        private readonly negosuiteContext _context;

        public ReceivableReportsController(negosuiteContext context)
        {
            _context = context;
        }

        [Route("customer-balances")]
        [HttpGet]
        public async Task<ActionResult> GetCustomerBalances(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var arTradeAccountId = selectCriteria.AccountId;

            var customerBalance = await (
                from e in _context.JournalEntries
                where e.JournalDate <= selectCriteria.PeriodEnd && e.Balance > 0 && e.AccountId == arTradeAccountId &&
                      e.Status == GeneralJournalsController.STATUS_POSTED
                group e by e.CustomerId into g
                select new
                {
                    g.First().CustomerId,
                    CustomerName = g.First().Customer.Name,
                    InvoiceBalance = g.Where(b => b.Nature == "D").Sum(s => s.Balance),
                    CreditBalance = g.Where(b => b.Nature == "C").Sum(s => s.Balance)
                }
                ).ToListAsync();

            var result = customerBalance
               .Select(r => new
               {
                   r.CustomerId,
                   r.CustomerName,
                   r.InvoiceBalance,
                   r.CreditBalance
               }).OrderBy(e => e.CustomerName).ToList();

            return Ok(result);

        }


        [Route("customer-balances-rc")]
        [HttpGet]
        public async Task<ActionResult> GetCustomerBalancesRC(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = new DateTime(1971, 12, 29).ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var journalSource = (selectCriteria.JournalSource != null) ? selectCriteria.JournalSource : "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = "";
            var accountId = selectCriteria.AccountId.ToString();
            var arrayString = selectCriteria.ArrayString;

            var list = await _context.ResponsibilityCenterJournalEntries
                .FromSqlInterpolated($"CALL GetJournalEntryBalancesByResponsibilityCenter({userConfigId}, {periodStart}, {periodEnd}, {journalSource}, {customerId}, {supplierId}, {arrayString}, {accountId})")
                .ToListAsync();

            var result = (from e in list
                group e by e.CustomerId into g
                select new
                {
                    g.First().CustomerId,
                    CustomerName = g.First().CustomerName,
                    InvoiceBalance = g.Where(b => b.Nature == "D").Sum(s => s.Balance),
                    CreditBalance = g.Where(b => b.Nature == "C").Sum(s => s.Balance)
                }).ToList()
                .Where(e => e.InvoiceBalance > 0)
                .Select(r => new
                {
                    r.CustomerId,
                    r.CustomerName,
                    r.InvoiceBalance,
                    r.CreditBalance
                }).OrderBy(e => e.CustomerName).ToList();

            return Ok(result);

        }


        [Route("aging-summary")]
        [HttpGet]
        public async Task<ActionResult> GetAgingSummary(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FindAsync(selectCriteria.UserConfigId);

            var arTradeAccountId = config.ARTradeAccountId;

            var agingPeriod = new
            {
                ShowCurrent = config.ARAgingShowCurrent,
                Period1 = config.ARAgingPeriod1,
                Period2 = config.ARAgingPeriod2,
                Period3 = config.ARAgingPeriod3,
                Period4 = config.ARAgingPeriod4,
            };

            var list = await (
                from e in _context.JournalEntries
                where ( (selectCriteria.AgingBaseDate == AGING_BY_INVOICE_DATE || e.Nature == "C") ? e.JournalDate : e.DueDate ) <= selectCriteria.PeriodEnd &&
                    e.Balance > 0 && e.AccountId == arTradeAccountId && e.Status == GeneralJournalsController.STATUS_POSTED
                select new
                {
                    e.CustomerId,
                    CustomerName = e.Customer.Name,
                    e.Nature,
                    e.Balance,
                    BaseDate = (selectCriteria.AgingBaseDate == AGING_BY_INVOICE_DATE) ? e.JournalDate : e.DueDate
                }).ToListAsync();

            var result = (
                from e in list
                group e by e.CustomerId into g
                select new
                {
                    g.First().CustomerId,
                    CustomerName = g.First().CustomerName,

                    CurrentAmount = (agingPeriod.ShowCurrent) ? g.Where(e => e.Nature == "D").Where(g => g.BaseDate == DateTime.Today).Sum(s => s.Balance) : 0,

                    Period1Amount = (agingPeriod.ShowCurrent)
                        ? g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period1) && e.BaseDate < DateTime.Today).Sum(s => s.Balance)
                        : g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period1) && e.BaseDate <= DateTime.Today).Sum(s => s.Balance),

                    Period2Amount = g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period2) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period1)).Sum(s => s.Balance),

                    Period3Amount = g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period3) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period2)).Sum(s => s.Balance),

                    Period4Amount = (agingPeriod.Period4 == 0)
                        ? g.Where(e => e.Nature == "D").Where(e => e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period3)).Sum(s => s.Balance)
                        : g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period4) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period3)).Sum(s => s.Balance),

                    Period5Amount = (agingPeriod.Period4 == 0) ? 0 : g.Where(e => e.Nature == "D").Where(e => e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period4)).Sum(s => s.Balance),

                    CreditBalance = -g.Where(e => e.Nature == "C").Sum(s => s.Balance),

                    Balance = g.Where(e => e.Nature == "D").Sum(s => s.Balance) - g.Where(e => e.Nature == "C").Sum(s => s.Balance)

                }).OrderBy(e => e.CustomerName).ToList();

            return Ok(result);

        }


        [Route("aging-summary-rc")]
        [HttpGet]
        public async Task<ActionResult> GetAgingSummaryRC(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FindAsync(selectCriteria.UserConfigId);

            var arTradeAccountId = config.ARTradeAccountId;

            var agingPeriod = new
            {
                ShowCurrent = config.ARAgingShowCurrent,
                Period1 = config.ARAgingPeriod1,
                Period2 = config.ARAgingPeriod2,
                Period3 = config.ARAgingPeriod3,
                Period4 = config.ARAgingPeriod4,
            };

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = new DateTime(1971, 12, 29).ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var journalSource = "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = "";
            var accountId = arTradeAccountId;
            var arrayString = selectCriteria.ArrayString;


            var list0 = await _context.ResponsibilityCenterJournalEntries
                .FromSqlInterpolated($"CALL GetJournalEntryBalancesByResponsibilityCenter({userConfigId}, {periodStart}, {periodEnd}, {journalSource}, {customerId}, {supplierId}, {arrayString}, {accountId})")
                .ToListAsync();


            var list = (
                from e in list0
                where ((selectCriteria.AgingBaseDate == AGING_BY_INVOICE_DATE || e.Nature == "C") ? e.JournalDate : e.DueDate) <= selectCriteria.PeriodEnd &&
                    e.Balance > 0 && e.AccountId == arTradeAccountId && e.Status == GeneralJournalsController.STATUS_POSTED
                select new
                {
                    e.CustomerId,
                    e.CustomerName,
                    e.Nature,
                    e.Balance,
                    BaseDate = (selectCriteria.AgingBaseDate == AGING_BY_INVOICE_DATE) ? e.JournalDate : e.DueDate
                }).ToList();

            var result = (
                from e in list
                group e by e.CustomerId into g
                select new
                {
                    g.First().CustomerId,
                    CustomerName = g.First().CustomerName,

                    CurrentAmount = (agingPeriod.ShowCurrent) ? g.Where(e => e.Nature == "D").Where(g => g.BaseDate == DateTime.Today).Sum(s => s.Balance) : 0,

                    Period1Amount = (agingPeriod.ShowCurrent)
                        ? g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period1) && e.BaseDate < DateTime.Today).Sum(s => s.Balance)
                        : g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period1) && e.BaseDate <= DateTime.Today).Sum(s => s.Balance),

                    Period2Amount = g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period2) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period1)).Sum(s => s.Balance),

                    Period3Amount = g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period3) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period2)).Sum(s => s.Balance),

                    Period4Amount = (agingPeriod.Period4 == 0)
                        ? g.Where(e => e.Nature == "D").Where(e => e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period3)).Sum(s => s.Balance)
                        : g.Where(e => e.Nature == "D").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period4) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period3)).Sum(s => s.Balance),

                    Period5Amount = (agingPeriod.Period4 == 0) ? 0 : g.Where(e => e.Nature == "D").Where(e => e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period4)).Sum(s => s.Balance),

                    CreditBalance = -g.Where(e => e.Nature == "C").Sum(s => s.Balance),

                    Balance = g.Where(e => e.Nature == "D").Sum(s => s.Balance) - g.Where(e => e.Nature == "C").Sum(s => s.Balance)

                }).OrderBy(e => e.CustomerName).ToList();

            return Ok(result);

        }


        [Route("aging-details")]
        [HttpGet]
        public async Task<ActionResult> GetAgingDetails(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FindAsync(selectCriteria.UserConfigId);

            var arTradeAccountId = config.ARTradeAccountId;

            var agingPeriod = new
            {
                ShowCurrent = config.ARAgingShowCurrent,
                Period1 = config.ARAgingPeriod1,
                Period2 = config.ARAgingPeriod2,
                Period3 = config.ARAgingPeriod3,
                Period4 = config.ARAgingPeriod4,
            };

            var result = await (
                from e in _context.JournalEntries
                where ((selectCriteria.AgingBaseDate == AGING_BY_INVOICE_DATE) ? e.JournalDate : e.DueDate) <= selectCriteria.PeriodEnd &&
                    ((selectCriteria != null && selectCriteria.CustomerId.HasValue) ? e.CustomerId == selectCriteria.CustomerId : true) &&
                    e.Balance > 0 && e.AccountId == arTradeAccountId && e.Status == GeneralJournalsController.STATUS_POSTED
                select new
                {
                    e.JournalDate,
                    e.DueDate,
                    e.ReferenceNo,
                    e.CustomerId,
                    CustomerName = e.Customer.Name,
                    Amount = e.Nature == "D" ? e.Amount : (decimal?)null,
                    Balance = e.Nature == "D" ? e.Balance : -e.Balance,
                    CreditBalance = e.Nature == "C" ? e.Balance : 0
                }).OrderBy(e => e.JournalDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);

        }


        [Route("aging-details-rc")]
        [HttpGet]
        public async Task<ActionResult> GetAgingDetailsRC(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FindAsync(selectCriteria.UserConfigId);

            var arTradeAccountId = config.ARTradeAccountId;

            var agingPeriod = new
            {
                ShowCurrent = config.ARAgingShowCurrent,
                Period1 = config.ARAgingPeriod1,
                Period2 = config.ARAgingPeriod2,
                Period3 = config.ARAgingPeriod3,
                Period4 = config.ARAgingPeriod4,
            };

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = new DateTime(1971, 12, 29).ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var journalSource = "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = "";
            var accountId = arTradeAccountId;
            var arrayString = selectCriteria.ArrayString;


            var list0 = await _context.ResponsibilityCenterJournalEntries
                .FromSqlInterpolated($"CALL GetJournalEntryBalancesByResponsibilityCenter({userConfigId}, {periodStart}, {periodEnd}, {journalSource}, {customerId}, {supplierId}, {arrayString}, {accountId})")
                .ToListAsync();

            var result = (
                from e in list0
                where ((selectCriteria.AgingBaseDate == AGING_BY_INVOICE_DATE) ? e.JournalDate : e.DueDate) <= selectCriteria.PeriodEnd &&
                    ((selectCriteria != null && selectCriteria.CustomerId.HasValue) ? e.CustomerId == selectCriteria.CustomerId : true) &&
                    e.Balance > 0 && e.AccountId == arTradeAccountId && e.Status == GeneralJournalsController.STATUS_POSTED
                select new
                {
                    e.JournalDate,
                    e.DueDate,
                    e.ReferenceNo,
                    e.CustomerId,
                    e.CustomerName,
                    Amount = e.Nature == "D" ? e.Amount : (decimal?)null,
                    Balance = e.Nature == "D" ? e.Balance : -e.Balance,
                    CreditBalance = e.Nature == "C" ? e.Balance : 0
                }).OrderBy(e => e.JournalDate).ThenBy(e => e.ReferenceNo).ToList();

            return Ok(result);

        }


        /*
        private static string GetAgingPeriod(JournalEntry e, Config config)
        {
            string p = "";
            DateTime? baseDate = DateTime.Today;

            if (config.ARAgingBaseDate == AGING_BY_INVOICE_DATE)
            {
                baseDate = e.JournalDate;
            }
            if (config.ARAgingBaseDate == AGING_BY_INVOICE_DUE_DATE)
            {
                baseDate = e.DueDate;
            }

            // Period 1
            if (baseDate >= DateTime.Today.AddDays(-config.ARAgingPeriod1) && baseDate <= DateTime.Today)
            {
                p = $"0-{config.ARAgingPeriod1} Days";
            }

            // Period 2
            if (baseDate >= DateTime.Today.AddDays(-config.ARAgingPeriod2) && baseDate < DateTime.Today.AddDays(-config.ARAgingPeriod1))
            {
                p = $"{config.ARAgingPeriod1 + 1}-{config.ARAgingPeriod2} Days";
            }

            // Period 3
            if (baseDate >= DateTime.Today.AddDays(-config.ARAgingPeriod3) && baseDate < DateTime.Today.AddDays(-config.ARAgingPeriod2))
            {
                p = $"{config.ARAgingPeriod2 + 1}-{config.ARAgingPeriod3} Days";
            }

            // Period 4 / Over
            if (config.ARAgingPeriod4 == 0)
            {
                if (baseDate < DateTime.Today.AddDays(-config.ARAgingPeriod3))
                {
                    p = $"{config.ARAgingPeriod3}+ Days";
                }
            }
            else
            {
                if (baseDate >= DateTime.Today.AddDays(-config.ARAgingPeriod4) && baseDate < DateTime.Today.AddDays(-config.ARAgingPeriod3))
                {
                    p = $"{-config.ARAgingPeriod3 + 1}-{-config.ARAgingPeriod4} Days";
                }
            }

            // Period Over
            if (config.ARAgingPeriod4 != 0)
            {
                if (baseDate < DateTime.Today.AddDays(-config.ARAgingPeriod4))
                {
                    p = $"{config.ARAgingPeriod4}+ Days";
                }
            }

            return p;                   
        }
        */

    }

}
