using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/inventory-reports")]
    [ApiController]
    public class InventoryReportsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        public InventoryReportsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/sales-invoices
        /*
        [Route("inventory-transactions")]
        [HttpGet]
        public async Task<ActionResult> GetInventoryTransactions(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var result = await _context.InventoryTransactions
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(c => c.ReferenceDate >= selectCriteria.PeriodStart && c.ReferenceDate <= selectCriteria.PeriodEnd)
                .Where(e => selectCriteria.InventoryLocationId != null ? selectCriteria.InventoryLocationId == e.InventoryLocationId : true)
                .Where(e => selectCriteria.TransactionSource != null ? selectCriteria.TransactionSource == e.Source : true)
                .Where(e => selectCriteria.ItemId != null ? selectCriteria.ItemId == e.ItemId : true)
                .Where(e => selectCriteria.CustomerId != null ? selectCriteria.CustomerId == e.CustomerId : true) 
                .Where(e => selectCriteria.SupplierId != null ? selectCriteria.SupplierId == e.SupplierId : true)
                .OrderBy(c => c.ReferenceDate)
                .ToListAsync();

            return Ok(result);
        }*/


        [Route("inventory-transactions")]
        [HttpGet]
        public async Task<ActionResult> GetInventoryTransactions(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var inventoryLocationId = (selectCriteria.InventoryLocationId != null) ? selectCriteria.InventoryLocationId.ToString() : "";
            var transSource = (selectCriteria.TransactionSource != null) ? selectCriteria.TransactionSource : "";
            var itemId = (selectCriteria.ItemId != null) ? selectCriteria.ItemId.ToString() : "";
            var customerId = (selectCriteria.CustomerId != null) ? selectCriteria.CustomerId.ToString() : "";
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPInventoryTransactions
                .FromSqlInterpolated($"CALL GetInventoryTransactions({userConfigId}, {periodStart}, {periodEnd}, {inventoryLocationId}, {transSource}, {itemId}, {customerId}, {supplierId}, {arrayString})")
                .ToListAsync();

            var result = list.OrderBy(c => c.ReferenceDate).ToList();

            return Ok(result);
        }


        // GET: api/sales-invoices
        /*
        [Route("stock-summary")]
        [HttpGet]
        public async Task<ActionResult> GetStockSummary(string criteria)
        {
            SelectCriteria selectCriteria = !string.IsNullOrEmpty(criteria) ? JsonConvert.DeserializeObject<SelectCriteria>(criteria) : null;

            var opening = await (
                from e in _context.InventoryTransactions
                where (e.UserConfigId == selectCriteria.UserConfigId) &&
                    (e.ReferenceDate < selectCriteria.PeriodStart) &&
                    (selectCriteria.InventoryLocationId != null ? selectCriteria.InventoryLocationId == e.InventoryLocationId : true)
                group e by e.ItemId into g
                select new
                {
                    g.First().ItemId,
                    g.First().ItemName,
                    g.First().ItemReorderPoint,
                    OpeningStock = (decimal)0,
                    QuantityIn = g.Sum(t => t.QuantityIn),
                    QuantityOut = g.Sum(t => t.QuantityOut)
                }
                ).ToListAsync();

            var transactions = await (
               from e in _context.InventoryTransactions
               where (e.UserConfigId == selectCriteria.UserConfigId) &&
                    (e.ReferenceDate >= selectCriteria.PeriodStart && e.ReferenceDate <= selectCriteria.PeriodEnd) &&
                   (selectCriteria != null && selectCriteria.InventoryLocationId != null ? selectCriteria.InventoryLocationId == e.InventoryLocationId : true)
               group e by e.ItemId into g
               select new
               {
                   g.First().ItemId,
                   g.First().ItemName,
                   g.First().ItemReorderPoint,
                   OpeningStock = (decimal)0,
                   QuantityIn = g.Sum(t => t.QuantityIn),
                   QuantityOut = g.Sum(t => t.QuantityOut)
               }).ToListAsync();

            var result = (
                from e in transactions
                join o in opening on e.ItemId equals o.ItemId into openings
                from o in openings.DefaultIfEmpty()
                select new
                {
                    e.ItemId,
                    e.ItemName,
                    e.ItemReorderPoint,
                    OpeningStock = o != null ? (o.QuantityIn - o.QuantityOut) : 0,
                    e.QuantityIn,
                    e.QuantityOut
                }).ToList();

            var finalResult = result
                .Select(e => new 
                {
                    e.ItemId,
                    e.ItemName,
                    e.ItemReorderPoint,
                    e.OpeningStock,
                    e.QuantityIn,
                    e.QuantityOut
                }).Union(opening
                .Where(e => !result.Select(r => r.ItemId).ToList() .Contains(e.ItemId))
                .Select(e => new 
                {
                    e.ItemId,
                    e.ItemName,
                    e.ItemReorderPoint,
                    OpeningStock = e.QuantityIn - e.QuantityOut,
                    QuantityIn = (decimal)0,
                    QuantityOut = (decimal)0
                })).OrderBy(e => e.ItemName).ToList();

            return Ok(finalResult);

        }*/


        [Route("stock-summary")]
        [HttpGet]
        public async Task<ActionResult> GetStockSummary(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var inventoryLocationId = (selectCriteria.InventoryLocationId != null) ? selectCriteria.InventoryLocationId.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;
            var itemId = (selectCriteria.ItemId != null) ? selectCriteria.ItemId.ToString() : "";

            var list = await _context.InventoryStockSummaries
                .FromSqlInterpolated($"CALL GetInventoryStockSummary({userConfigId}, {periodStart}, {periodEnd}, {itemId}, {inventoryLocationId}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.ItemId,
                    e.ItemName,
                    e.ItemReorderPoint,
                    e.OpeningStock,
                    e.QuantityIn,
                    e.QuantityOut,
                    //e.TotalCost,
                    //e.PeriodTotalCost,
                    AverageCost = (e.AverageCost != null ? e.AverageCost : 0),
                    //PeriodAverageCost = (e.PeriodAverageCost != null ? e.PeriodAverageCost : 0),
                    LastInCost = (e.LastInCost != null ? e.LastInCost : 0),
                    e.LastPurchasedDate
                }).OrderBy(e => e.ItemName).ToList();

            return Ok(result);
        }


        [Route("low-inventory")]
        [HttpGet]
        public async Task<ActionResult> GetLowStockSummary(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var inventoryLocationId = (selectCriteria.InventoryLocationId != null) ? selectCriteria.InventoryLocationId.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;
            var itemId = "";

            var list = await _context.InventoryStockSummaries
                .FromSqlInterpolated($"CALL GetInventoryStockSummary({userConfigId}, {periodStart}, {periodEnd}, {itemId}, {inventoryLocationId}, {arrayString})")
                .ToListAsync();

            var result = list.Where(e => (e.OpeningStock + e.QuantityIn - e.QuantityOut) < e.ItemReorderPoint)
                .Select(e => new
                {
                    e.ItemId,
                    e.ItemName,
                    e.ItemReorderPoint,
                    e.OpeningStock,
                    e.QuantityIn,
                    e.QuantityOut
                }).OrderBy(e => e.ItemName).ToList();

            return Ok(result);
        }


        [AllowAnonymous]
        [Route("inventory-by-location")]
        [HttpGet]
        public async Task<ActionResult> GetPivotedInventoryWithJsonArray(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd");
            var itemId = (selectCriteria.ItemId != null) ? selectCriteria.ItemId.ToString() : "";

            var list = await _context.PivotedInventoryWithJsonArray
                .FromSqlInterpolated($"CALL GetPivotedInventoryWithJsonArray({userConfigId}, {periodEnd}, {itemId})")
                .ToListAsync();

            return Ok(list);
        }

    }

}