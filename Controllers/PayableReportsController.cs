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
    [Route("api/payable-reports")]
    [ApiController]
    public class PayableReportsController : ControllerBase
    {
        private static short AGING_BY_BILL_DATE = 1;
        private static short AGING_BY_BILL_DUE_DATE = 2;

        private readonly negosuiteContext _context;

        public PayableReportsController(negosuiteContext context)
        {
            _context = context;
        }


        [Route("supplier-balances")]
        [HttpGet]
        public async Task<ActionResult> GetCustomerBalances(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var apTradeAccountId = selectCriteria.AccountId;

            var supplierBalance = await (
               from e in _context.JournalEntries
               where e.JournalDate <= selectCriteria.PeriodEnd && e.Balance > 0 && e.AccountId == apTradeAccountId && 
                     e.Status == GeneralJournalsController.STATUS_POSTED
               group e by e.SupplierId into g
               select new
               {
                   g.First().SupplierId,
                   SupplierName = g.First().Supplier.Name,
                   BillBalance = g.Where(j => j.Nature == "C").Sum(s => s.Balance),
                   DebitBalance = g.Where(j => j.Nature == "D").Sum(s => s.Balance)
               }
               ).ToListAsync();

            var result = supplierBalance
                .Select(r => new
                {
                    r.SupplierId,
                    r.SupplierName,
                    r.BillBalance,
                    r.DebitBalance
                }).OrderBy(e => e.SupplierName).ToList();

            return Ok(result);
        }



        [Route("supplier-balances-rc")]
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
                group e by e.SupplierId into g
                select new
                {
                    g.First().SupplierId,
                    SupplierName = g.First().SupplierName,
                    BillBalance = g.Where(j => j.Nature == "C").Sum(s => s.Balance),
                    DebitBalance = g.Where(j => j.Nature == "D").Sum(s => s.Balance)
                }
                ).ToList()
                .Where(e => e.BillBalance > 0)
                .Select(r => new
                {
                    r.SupplierId,
                    r.SupplierName,
                    r.BillBalance,
                    r.DebitBalance
                }).OrderBy(e => e.SupplierName).ToList();

            return Ok(result);
        }


        [Route("aging-summary")]
        [HttpGet]
        public async Task<ActionResult> GetAgingSummary(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FindAsync(selectCriteria.UserConfigId);

            var apTradeAccountId = config.APTradeAccountId;
            //var asOfDate = (DateTime)selectCriteria.PeriodEnd;

            var agingPeriod = new
            {
                ShowCurrent = config.APAgingShowCurrent,
                Period1 = config.APAgingPeriod1,
                Period2 = config.APAgingPeriod2,
                Period3 = config.APAgingPeriod3,
                Period4 = config.APAgingPeriod4,
            };

            var list = await (
                from e in _context.JournalEntries
                where ((selectCriteria.AgingBaseDate == AGING_BY_BILL_DATE || e.Nature == "D") ? e.JournalDate : e.DueDate) <= selectCriteria.PeriodEnd &&
                    e.Balance > 0 && e.AccountId == apTradeAccountId && e.Status == GeneralJournalsController.STATUS_POSTED
                select new
                {
                    e.SupplierId,
                    SupplierName = e.Supplier.Name,
                    e.Nature,
                    e.Balance,
                    BaseDate = (selectCriteria.AgingBaseDate == AGING_BY_BILL_DATE) ? e.JournalDate : e.DueDate
                }).ToListAsync();

            var result = (
                from e in list
                group e by e.SupplierId into g
                select new
                {
                    g.First().SupplierId,
                    SupplierName = g.First().SupplierName,

                    CurrentAmount = (agingPeriod.ShowCurrent) ? g.Where(e => e.Nature == "C").Where(g => g.BaseDate == DateTime.Today).Sum(s => s.Balance) : 0,

                    Period1Amount = (agingPeriod.ShowCurrent)
                        ? g.Where(e => e.Nature == "C").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period1) && e.BaseDate < DateTime.Today).Sum(s => s.Balance)
                        : g.Where(e => e.Nature == "C").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period1) && e.BaseDate <= DateTime.Today).Sum(s => s.Balance),
                  
                    Period2Amount = g.Where(e => e.Nature == "C").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period2) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period1)).Sum(s => s.Balance),

                    Period3Amount = g.Where(e => e.Nature == "C").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period3) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period2)).Sum(s => s.Balance),

                    Period4Amount = (agingPeriod.Period4 == 0)
                        ? g.Where(e => e.Nature == "C").Where(e => e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period3)).Sum(s => s.Balance)
                        : g.Where(e => e.Nature == "C").Where(e => e.BaseDate >= DateTime.Today.AddDays(-agingPeriod.Period4) && e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period3)).Sum(s => s.Balance),

                    Period5Amount = (agingPeriod.Period4 == 0) ? 0 : g.Where(e => e.BaseDate < DateTime.Today.AddDays(-agingPeriod.Period4)).Sum(s => s.Balance),

                    DebitBalance = -g.Where(e => e.Nature == "D").Sum(s => s.Balance),

                    Balance = g.Where(e => e.Nature == "C").Sum(s => s.Balance) - g.Where(e => e.Nature == "D").Sum(s => s.Balance)

                }).OrderBy(e => e.SupplierName).ToList();

            return Ok(result);

        }


        [Route("aging-details")]
        [HttpGet]
        public async Task<ActionResult> GetAgingDetails(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var config = await _context.Configs.FindAsync(selectCriteria.UserConfigId);

            var arTradeAccountId = config.APTradeAccountId;

            var agingPeriod = new
            {
                ShowCurrent = config.APAgingShowCurrent,
                Period1 = config.APAgingPeriod1,
                Period2 = config.APAgingPeriod2,
                Period3 = config.APAgingPeriod3,
                Period4 = config.APAgingPeriod4,
            };

            var result = await (
                from e in _context.JournalEntries
                where ((selectCriteria.AgingBaseDate == AGING_BY_BILL_DATE || e.Nature == "D") ? e.JournalDate : e.DueDate) <= selectCriteria.PeriodEnd &&
                    ((selectCriteria != null && selectCriteria.SupplierId.HasValue) ? e.SupplierId == selectCriteria.SupplierId : true) &&
                    e.Balance > 0 && e.AccountId == arTradeAccountId && e.Status == GeneralJournalsController.STATUS_POSTED
                select new
                {
                    e.JournalDate,
                    e.DueDate,
                    e.ReferenceNo,
                    e.Nature,
                    e.Supplier,
                    e.SupplierId,
                    SupplierName = e.Supplier.Name,
                    Amount = e.Nature == "C" ? e.Amount : (decimal?)null,
                    Balance = e.Nature == "C" ? e.Balance : -e.Balance,
                    DebitBalance = e.Nature == "D" ? e.Balance : 0,
                }).OrderBy(e => e.JournalDate).ThenBy(e => e.ReferenceNo).ToListAsync();

            return Ok(result);

        }

        /*
        private static string GetAgingPeriod(JournalEntry e, Config config)
        {
            string p = "";
            DateTime? baseDate = DateTime.Today;

            if (config.APAgingBaseDate == AGING_BY_BILL_DATE)
            {
                baseDate = e.JournalDate;
            }
            if (config.APAgingBaseDate == AGING_BY_BILL_DUE_DATE)
            {
                baseDate = e.DueDate;
            }

            // Period 1
            if (baseDate >= DateTime.Today.AddDays(-config.APAgingPeriod1) && baseDate <= DateTime.Today)
            {
                p = $"0-{config.APAgingPeriod1} Days";
            }

            // Period 2
            if (baseDate >= DateTime.Today.AddDays(-config.APAgingPeriod2) && baseDate < DateTime.Today.AddDays(-config.APAgingPeriod1))
            {
                p = $"{config.APAgingPeriod1 + 1}-{config.APAgingPeriod2} Days";
            }

            // Period 3
            if (baseDate >= DateTime.Today.AddDays(-config.APAgingPeriod3) && baseDate < DateTime.Today.AddDays(-config.APAgingPeriod2))
            {
                p = $"{config.APAgingPeriod2 + 1}-{config.APAgingPeriod3} Days";
            }

            // Period 4 / Over
            if (config.APAgingPeriod4 == 0)
            {
                if (baseDate < DateTime.Today.AddDays(-config.APAgingPeriod3))
                {
                    p = $"{config.APAgingPeriod3}+ Days";
                }
            }
            else
            {
                if (baseDate >= DateTime.Today.AddDays(-config.APAgingPeriod4) && baseDate < DateTime.Today.AddDays(-config.APAgingPeriod3))
                {
                    p = $"{-config.APAgingPeriod3 + 1}-{-config.APAgingPeriod4} Days";
                }
            }

            // Period Over
            if (config.APAgingPeriod4 != 0)
            {
                if (baseDate < DateTime.Today.AddDays(-config.APAgingPeriod4))
                {
                    p = $"{config.APAgingPeriod4}+ Days";
                }
            }

            return p;
        }
        */

    }

}
