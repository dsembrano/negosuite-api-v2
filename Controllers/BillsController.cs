using System;
using negosuite_api.Contracts.Transactions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Bills;
using negosuite_api.Services;
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
    [Route("api/bills")]
    [ApiController]
    public class BillsController : ControllerBase
    {
        private readonly negosuiteContext _context;
        private readonly BillService service;

        [ActivatorUtilitiesConstructor]
        public BillsController(negosuiteContext context, BillService service)
        {
            _context = context;
            this.service = service;
        }

        public BillsController(negosuiteContext context) : this(context, new BillService(context)) { }
        private int? CompanyId => HttpContext?.Items[ConfigUuidFilter.CompanyIdKey] as int?;

        [HttpGet]
        public async Task<ActionResult> GetBills(string criteria, [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default,
            [FromQuery] string search = null, [FromQuery] string sortBy = null, [FromQuery] string sortDirection = null,
            [FromQuery] short? status = null)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            if (!CustomerPagination.IsValid(pageNumber, pageSize)) return BadRequest("Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
            if (!BillQuery.IsValidSort(sortBy, sortDirection)) return BadRequest("Unsupported sortBy or sortDirection. Use a supported column and asc or desc.");
            if (status.HasValue && status is not (-1 or 0 or 1)) return BadRequest("status must be -1 (deleted), 0 (draft) or 1 (posted).");
            BillListCriteria filter;
            try { filter = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<BillListCriteria>(criteria); }
            catch (JsonException) { return BadRequest("Invalid criteria JSON."); }
            if (filter?.UserConfigId == null) return BadRequest("criteria.userConfigId is required.");
            if (filter.UserConfigId != CompanyId) return Forbid();
            if (!SalesInvoiceQuery.TryParseCenters(filter.ArrayString, out var centers)) return BadRequest("criteria.arrayString must contain comma-separated positive responsibility center IDs.");
            return Ok(await service.ListAsync(CompanyId.Value, filter, centers, pageNumber, pageSize, search, sortBy, sortDirection, status, cancellationToken));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BillDetailDto>> GetBill(int id, CancellationToken cancellationToken = default)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var result = await service.GetAsync(CompanyId.Value, id, cancellationToken);
            return result == null ? NotFound() : result;
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBill(int id, BillUpdateRequest request)
        {
            var bill = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (bill.UserConfigId != CompanyId) return Forbid();
            if (id != bill.Id)
            {
                return BadRequest();
            }

            if (!await _context.Bills.AnyAsync(b => b.Id == id && b.UserConfigId == CompanyId.Value)) return NotFound();
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, bill, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var inventory = await BillInventorySnapshot.LoadAsync(_context, CompanyId.Value, bill.BillDetails, id, HttpContext.RequestAborted);
            var b = await _context.Bills.FirstOrDefaultAsync(e => e.BillNo == bill.BillNo && e.UserConfigId == bill.UserConfigId && e.Id != id);
            if (b != null)
            {
                return Conflict($"Bill# {bill.BillNo} already exist.");
            }

            bill.LastUpdatedDate = DateTime.Now;

            if (bill.Balance < 0)
            {
                return BadRequest("Invalid amount. Payment already applied to this Bill is more than the new amount.");
            }

            // Invoice Details
            foreach (var e in bill.BillDetails.ToList())
            {
                e.BillId = id;
                // Convert date to local timezone
                var itemInventory = inventory.Summaries.GetValueOrDefault(e.ItemId);

                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.BillDetails.Add(e);

                    // Calculate average cost based on landed cost ////////////////////////////////////////////////////////////////////////////

                    decimal? newAverageCost = 0;

                    var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                    if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0 && itemInventory.Quantity + e.Quantity != 0)
                    {
                        newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * e.Quantity)) / (itemInventory.Quantity + e.Quantity);
                    }

                    var item = inventory.Items[e.ItemId];
                    if (item.LastPurchasedDate == null || item.LastPurchasedDate <= bill.BillDate || !item.Cost.HasValue)
                    {
                        item.LastPurchasedDate = bill.BillDate;
                        item.Cost = e.Rate;
                    }

                    item.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;

                    _context.Entry(item).State = EntityState.Modified;
                    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                }
                else
                {
                    if (e.Deleted == true)
                    {
                        // Calculate average cost based on landed cost ////////////////////////////////////////////////////////////////////////////

                        decimal? newAverageCost = 0;

                        var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                        if (itemInventory != null && itemInventory.AverageCost.HasValue &&
                            itemInventory.AverageCost > 0 && itemInventory.Quantity > 0 &&
                            itemInventory.Quantity > e.Quantity)
                        {
                            newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * -e.Quantity)) / (itemInventory.Quantity + -e.Quantity);
                        }

                        var item = inventory.Items[e.ItemId];
                        item.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;

                        _context.Entry(item).State = EntityState.Modified;
                        /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 

                        var entry = await _context.BillDetails.FindAsync(e.Id);
                        _context.BillDetails.Remove(entry);
                    }
                    else
                    {

                        if (e.Touched == true)
                        {

                            // Remove quantity of original value of item and recalculate average cost  ////////////////////////////////////////////
                            var d = inventory.Originals[e.Id];
                            var landedCost = d.Rate + (d.LandedCost.HasValue ? d.LandedCost / d.Quantity : 0);
                            decimal? newAverageCost = 0;

                            var itemInventory0 = inventory.Summaries.GetValueOrDefault(d.ItemId);

                            if (itemInventory0 != null && itemInventory0.AverageCost.HasValue &&
                                itemInventory0.AverageCost > 0 && itemInventory0.Quantity > 0 &&
                                itemInventory0.Quantity > d.Quantity)
                            {
                                newAverageCost = ((itemInventory0.Quantity * itemInventory0.AverageCost) - (landedCost * d.Quantity)) / (itemInventory0.Quantity - d.Quantity);
                                newAverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;
                            }

                            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                            if (d.ItemId == e.ItemId)
                            {
                                var item0 = inventory.Items[d.ItemId];

                                landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);
                                var itemQuantity = (itemInventory0?.Quantity ?? 0) - d.Quantity;

                                if (newAverageCost > 0 && itemQuantity > 0 && itemQuantity + e.Quantity != 0)
                                {
                                    newAverageCost = ((itemQuantity * newAverageCost) + (landedCost * e.Quantity)) / (itemQuantity + e.Quantity);
                                }

                                item0.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;
                                if (item0.LastPurchasedDate == null || item0.LastPurchasedDate <= bill.BillDate || !item0.Cost.HasValue)
                                {
                                    item0.LastPurchasedDate = bill.BillDate;
                                    item0.Cost = e.Rate;
                                }
                                _context.Entry(item0).State = EntityState.Modified;

                                e.LastUpdatedDate = DateTime.Now;
                                _context.Entry(e).State = EntityState.Modified;
                            }
                            else
                            {
                                // Restore previous cost of replaced item /////////////////////////////////////////////////////////
                                var item0 = inventory.Items[d.ItemId];

                                item0.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;
                                if (item0.LastPurchasedDate == null || item0.LastPurchasedDate <= bill.BillDate || !item0.Cost.HasValue)
                                {
                                    item0.LastPurchasedDate = bill.BillDate;
                                    item0.Cost = e.Rate;
                                }
                                _context.Entry(item0).State = EntityState.Modified;


                                // Update cost of new item //////////////////////////////////////////////////////////////////////////
                                newAverageCost = 0;
                                landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                                if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0 && itemInventory.Quantity + e.Quantity != 0)
                                {
                                    newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * e.Quantity)) / (itemInventory.Quantity + e.Quantity);
                                }

                                var item1 = inventory.Items[e.ItemId];
                                item1.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;
                                if (item1.LastPurchasedDate == null || item1.LastPurchasedDate <= bill.BillDate || !item1.Cost.HasValue)
                                {
                                    item1.LastPurchasedDate = bill.BillDate;
                                    item1.Cost = e.Rate;
                                }

                                _context.Entry(item1).State = EntityState.Modified;

                                e.LastUpdatedDate = DateTime.Now;
                                _context.Entry(e).State = EntityState.Modified;
                            }

                        }
                    }
                }
            }

            JournalEntriesController.SanitizeEntries(bill.JournalEntries);

            // Journal Entries
            foreach (var e in bill.JournalEntries.ToList())
            {
                e.BillId = id;
                e.JournalDate = bill.BillDate;
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

            _context.Entry(bill).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BillExists(id))
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

        // POST: api/Bills
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<BillDetailDto>> PostBill(BillCreateRequest request)
        {
            var bill = TransactionWriteMapping.Map(request);
            if (!CompanyId.HasValue) return Unauthorized();
            if (bill.UserConfigId != CompanyId) return Forbid();

            if (bill.Id != 0) return BadRequest("New bill ID must be zero or omitted.");
            var validationError = await service.ValidateWriteAsync(CompanyId.Value, null, bill, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var inventory = await BillInventorySnapshot.LoadAsync(_context, CompanyId.Value, bill.BillDetails, null, HttpContext.RequestAborted);
            var b = await _context.Bills.FirstOrDefaultAsync(e => e.BillNo == bill.BillNo && e.UserConfigId == bill.UserConfigId);
            if (b != null)
            {
                return Conflict($"Bill# {bill.BillNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(bill.JournalEntries);
            bill.CreatedDate = DateTime.Now;



            // Details
            foreach (var e in bill.BillDetails)
            {
                e.CreatedDate = DateTime.Now;

                // Calculate average cost based on landed cost ////////////////////////////////////////////////////////////////////////////
                var itemInventory = inventory.Summaries.GetValueOrDefault(e.ItemId);
                decimal? newAverageCost = 0;

                var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0 && itemInventory.Quantity + e.Quantity != 0)
                {
                    newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * e.Quantity)) / (itemInventory.Quantity + e.Quantity);
                }

                var item = inventory.Items[e.ItemId];
                if (item.LastPurchasedDate == null || item.LastPurchasedDate <= bill.BillDate || !item.Cost.HasValue)
                {
                    item.LastPurchasedDate = bill.BillDate;
                    item.Cost = e.Rate;
                }

                item.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;

                _context.Entry(item).State = EntityState.Modified;
                ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            }

            foreach (var e in bill.JournalEntries)
            {
                e.JournalDate = bill.BillDate;
                e.CreatedDate = DateTime.Now;
            }

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBill", new { id = bill.Id }, new TransactionResponseMapping().Map(bill));
        }

        // DELETE: api/Bills/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBill(int id)
        {
            if (!CompanyId.HasValue) return Unauthorized();
            var bill = await _context.Bills.Where(e => e.Id == id && e.UserConfigId == CompanyId.Value)
                .Include(e => e.JournalEntries)
                .Include(e => e.BillDetails)
                .SingleOrDefaultAsync();

            if (bill == null)
            {
                return NotFound();
            }

            if (bill.Balance < bill.Amount)
            {
                return BadRequest("Can not delete this Bill because payment was already applied.");
            }

            var validationError = await service.ValidateWriteAsync(CompanyId.Value, id, bill, HttpContext.RequestAborted);
            if (validationError != null) return BadRequest(validationError);
            var inventory = await BillInventorySnapshot.LoadAsync(_context, CompanyId.Value, bill.BillDetails, id, HttpContext.RequestAborted);
            // Journal Entries
            foreach (var e in bill.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            // Bill Details
            foreach (var e in bill.BillDetails.ToList())
            {
                e.BillId = id;
                var itemInventory = inventory.Summaries.GetValueOrDefault(e.ItemId);
                decimal? newAverageCost = 0;
                var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > e.Quantity)
                {
                    newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * -e.Quantity)) / (itemInventory.Quantity + -e.Quantity);
                }

                var item = inventory.Items[e.ItemId];
                item.AverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;

                _context.Entry(item).State = EntityState.Modified;
                /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////// 

                _context.Entry(e).State = EntityState.Deleted;
            }

            _context.Entry(bill).State = EntityState.Deleted;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BillExists(id))
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

        private bool BillExists(int id)
        {
            return _context.Bills.Any(e => e.Id == id && e.UserConfigId == CompanyId.Value);
        }

    }
}
