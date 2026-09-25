using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/sales-reports")]
    [ApiController]
    public class SalesReportsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public SalesReportsController(negosuiteContext context)
        {
            _context = context;
        }


        [Route("sales-transactions")]
        [HttpGet]
        public async Task<ActionResult> GetSalesTransactions(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var list = await _context.SalesTransactions
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd)
                .Where(e => selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource == e.Source : true)
                .Where(e => selectCriteria.CustomerId != null ? selectCriteria.CustomerId == e.CustomerId : true)
                .Where(e => e.Status != GeneralJournalsController.STATUS_DELETED)
                .OrderBy(e => e.ReferenceDate).ThenBy(e  => e.ReferenceNo)
                .Select(e => new
                {
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    Taxes = JsonConvert.DeserializeObject<ICollection<Tax>>(e.Taxes),
                    e.DiscountAmount
                }).ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    TaxAmount = e.Taxes.Sum(t => t.Amount),
                    Sales = e.Amount - e.Taxes.Sum(t => t.Amount),
                    SalesWithTax = e.Amount,
                    e.DiscountAmount
                }).ToList();

            return Ok(result);

        }


        [Route("sales-transactions-rc")]
        [HttpGet]
        public async Task<ActionResult> GetSalesTransactionsRC(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var source = selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource : "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SalesTransactionFunction
                .FromSqlInterpolated($"CALL GetSalesTransactions({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {source}, {arrayString})")
                .ToListAsync();

            var result = list
            .Select(e => new
                {
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    Taxes = JsonConvert.DeserializeObject<ICollection<Tax>>(e.Taxes),
                    e.DiscountAmount,
                    e.ResponsibilityCenterEntry
            }).ToList().OrderBy(e => e.ReferenceDate).ThenBy(e => e.ReferenceNo)
                .Select(e => new
                {
                    e.ReferenceNo,
                    e.ReferenceDate,
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    TaxAmount = e.Taxes.Sum(t => t.Amount),
                    Sales = e.Amount - e.Taxes.Sum(t => t.Amount),
                    SalesWithTax = e.Amount,
                    e.DiscountAmount,
                    e.ResponsibilityCenterEntry
                }).ToList();

            return Ok(result);

        }



        [Route("sales-transaction-details-rc")]
        [HttpGet]
        public async Task<ActionResult> GetSalesTransactionsDetailsRC(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var source = selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource : "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SalesTransactionDetails
                .FromSqlInterpolated($"CALL GetSalesTransactionDetails({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {source}, {arrayString})")
                .ToListAsync();

            return Ok(list);

        }


        [Route("sales-by-customer")]
        [HttpGet]
        public async Task<ActionResult> GetSalesByCustomer(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var transactionSource = (selectCriteria.TransactionSource != null) ? selectCriteria.TransactionSource : "";
            var arrayString = selectCriteria.ArrayString;

            var result = (await _context.CustomerSales
                .FromSqlInterpolated($"CALL GetSalesByCustomer({userConfigId}, {periodStart}, {periodEnd}, {transactionSource}, {arrayString})")
                .ToListAsync())
                .Select(r => new
             {
                 CustomerId = r.CustomerId,
                 CustomerName = r.CustomerName,
                 InvoiceCount = r.InvoiceCount,
                 Cost = r.Cost ?? 0,
                 Sales = r.Sales ?? 0,
                 SalesWithTax = r.SalesWithTax ?? 0,
                 TaxAmount = r.TaxAmount ?? 0
             }).ToList();

            return Ok(result);
        }

        
        /*
        [Route("sales-by-customer")]
        [HttpGet]
        public async Task<ActionResult> GetSalesByCustomer(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var list = await _context.SalesTransactions
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd)
                .Where(e => selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource == e.Source : true)
                .Where(e => e.Status != GeneralJournalsController.STATUS_DELETED)
                .OrderBy(e => e.CustomerName)
                .Select(e => new
                {
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    Taxes = JsonConvert.DeserializeObject<ICollection<Tax>>(e.Taxes),
                    e.DiscountAmount
                }).ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    TaxAmount = e.Taxes.Sum(t => t.Amount),
                    e.DiscountAmount
                }).ToList().GroupBy(e => e.CustomerId)
                .Select(r => new
                {
                    CustomerId = r.First().CustomerId,
                    CustomerName = r.First().CustomerName,
                    InvoiceCount = r.Count(),
                    Cost = r.Sum(s => s.Cost),
                    Sales = r.Sum(s => s.Amount) - r.Sum(s => s.TaxAmount),
                    SalesWithTax = r.Sum(s => s.Amount),
                    TaxAmount = r.Sum(s => s.TaxAmount),
                    DiscountAmount = r.Sum(s => s.DiscountAmount)
                }).ToList();

            return Ok(result);
        }*/


        /*
        [Route("sales-by-customer-rc")]
        [HttpGet]
        public async Task<ActionResult> GetSalesByCustomerRc(string criteria)
        {

            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var source = selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SalesTransactionFunction
                .FromSqlInterpolated($"CALL GetSalesTransactions({userConfigId}, {periodStart}, {periodEnd}, {source}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.CustomerId,
                    e.CustomerName,
                    e.Cost,
                    e.Amount,
                    e.Balance,
                    TaxAmount = JsonConvert.DeserializeObject<ICollection<Tax>>(e.Taxes).Sum(t => t.Amount),
                    e.DiscountAmount
                }).ToList().GroupBy(e => e.CustomerId)
                .Select(r => new
                {
                    CustomerId = r.First().CustomerId,
                    CustomerName = r.First().CustomerName,
                    InvoiceCount = r.Count(),
                    Cost = r.Sum(s => s.Cost),
                    Sales = r.Sum(s => s.Amount) - r.Sum(s => s.TaxAmount),
                    SalesWithTax = r.Sum(s => s.Amount),
                    TaxAmount = r.Sum(s => s.TaxAmount),
                    DiscountAmount = r.Sum(s => s.DiscountAmount)
                }).ToList().OrderBy(e => e.CustomerName);

            return Ok(result);

        }*/


        [Route("sales-by-item")]
        [HttpGet]
        public async Task<ActionResult> GetSalesByItem(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var transactionSource = (selectCriteria.TransactionSource != null) ? selectCriteria.TransactionSource : "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var arrayString = selectCriteria.ArrayString;

            var result = await _context.ItemSales
                .FromSqlInterpolated($"CALL GetSalesByItem({userConfigId}, {periodStart}, {periodEnd}, {transactionSource}, {customerId}, {arrayString})")
                .ToListAsync();

            return Ok(result);
        }


        [Route("sales-monthly-trend")]
        [HttpGet]
        public async Task<ActionResult> GetSalesMonthlyTrend(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var arrayString = selectCriteria.ArrayString;

            var result = await _context.SalesMonthlyTrend
                .FromSqlInterpolated($"CALL GetSalesMonthlyTrend({userConfigId}, {periodStart}, {periodEnd}, {customerId}, {arrayString})")
                .ToListAsync();

            return Ok(result);
        }


        /*
        [Route("sales-by-item")]
        [HttpGet]
        public async Task<ActionResult> GetSalesByItem(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var list = await (
                from e in _context.SalesTransactionDetails
                where ( e.UserConfigId == selectCriteria.UserConfigId) &&
                      ( e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd ) &&
                      ( selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource == e.Source : true) &&
                      ( selectCriteria.CustomerId != null ? selectCriteria.CustomerId == e.CustomerId : true ) &&
                      e.Status != GeneralJournalsController.STATUS_DELETED
                select new
                {
                    e.ItemId,
                    e.ItemName,
                    Cost = (e.Cost * e.Quantity),
                    Amount = e.IsTaxExclusive || e.TaxRate == 0 ? (e.Amount - e.DiscountAmount) : ( (e.Amount / (1 + e.TaxRate/100)) - e.DiscountAmount),
                    e.TaxAmount,
                    e.DiscountAmount,
                    e.Quantity,
                    e.Rate
                }
            ).ToListAsync();

            var result = list.GroupBy(e => e.ItemId)
                .Select(r => new
                {
                    ItemId = r.First().ItemId,
                    ItemName = r.First().ItemName,
                    Cost = r.Sum(s => s.Cost),
                    Sales = r.Sum(s => s.Amount),
                    SalesWithTax = r.Sum(s => s.Amount) + r.Sum(s => s.TaxAmount),
                    TaxAmount = r.Sum(s => s.TaxAmount),
                    Quantity = r.Sum(s => s.Quantity),
                    AveragePrice = r.Sum(s => s.Rate) / r.Count(),
                    Count = r.Count()
                }).ToList().OrderBy(e => e.ItemName);

            return Ok(result);

        }*/


        /*
        [Route("sales-by-item-rc")]
        [HttpGet]
        public async Task<ActionResult> GetSalesByItemRc(string criteria)
        {

            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var source = selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = ( await _context.SalesTransactionDetails
                .FromSqlInterpolated($"CALL GetSalesTransactionDetails({userConfigId}, {periodStart}, {periodEnd}, {source}, {arrayString})")
                .ToListAsync() )
                .Select(e => new
                {
                    e.ItemId,
                    e.ItemName,
                    Cost = (e.Cost * e.Quantity),
                    Amount = e.IsTaxExclusive || e.TaxRate == 0 ? (e.Amount - e.DiscountAmount) : ((e.Amount / (1 + e.TaxRate / 100)) - e.DiscountAmount),
                    e.TaxAmount,
                    e.DiscountAmount,
                    e.Quantity,
                    e.Rate
                }
            ).ToList();

            var result = list.GroupBy(e => e.ItemId)
                .Select(r => new
                {
                    ItemId = r.First().ItemId,
                    ItemName = r.First().ItemName,
                    Cost = r.Sum(s => s.Cost),
                    Sales = r.Sum(s => s.Amount),
                    SalesWithTax = r.Sum(s => s.Amount) + r.Sum(s => s.TaxAmount),
                    TaxAmount = r.Sum(s => s.TaxAmount),
                    Quantity = r.Sum(s => s.Quantity),
                    AveragePrice = r.Sum(s => s.Rate) / r.Count(),
                    Count = r.Count()
                }).ToList().OrderBy(e => e.ItemName);

            return Ok(result);

        }*/


    }


}
