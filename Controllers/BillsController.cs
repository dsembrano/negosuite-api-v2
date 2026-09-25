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
    [Route("api/bills")]
    [ApiController]
    public class BillsController : ControllerBase
    {
        private readonly negosuiteContext _context;

        public BillsController(negosuiteContext context)
        {
            _context = context;
        }

        // GET: api/Bills
        /*
        [HttpGet]
        public async Task<ActionResult> GetBills(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var result = await _context.Bills
                .Where(e => selectCriteria.ReferenceNo != null ? e.BillNo == selectCriteria.ReferenceNo : true)
                .Where(e => e.UserConfigId == selectCriteria.UserConfigId)
                .Where(e => (selectCriteria != null && selectCriteria.SupplierId.HasValue) ? e.SupplierId == selectCriteria.SupplierId : true)
                .Where(e => selectCriteria.PeriodStart.HasValue && selectCriteria.PeriodEnd.HasValue ? e.BillDate >= selectCriteria.PeriodStart && e.BillDate <= selectCriteria.PeriodEnd : true)
                .Where(e => selectCriteria != null && selectCriteria.ShowDeleted == true ? true : e.Status != GeneralJournalsController.STATUS_DELETED)
               .Select(e => new
               {
                   e.Id,
                   e.BillNo,
                   e.BillDate,
                   e.DueDate,
                   e.SupplierId,
                   SupplierName = e.Supplier.Name,
                   e.Supplier,
                   e.Amount,
                   e.Balance,
                   e.PaymentTermId,
                   PaymentTermName = e.PaymentTerm.Name,
                   e.PaymentTerm,
                   e.Status,
                   StatusName = GetStatusName(e)
               }).OrderBy(e => e.BillDate).ThenBy(e => e.BillNo).ToListAsync();

            return Ok(result);
        }*/


        [HttpGet]
        public async Task<ActionResult> GetBills(string criteria)
        {
            SelectCriteria selectCriteria = JsonConvert.DeserializeObject<SelectCriteria>(criteria);

            var userConfigId = selectCriteria.UserConfigId.ToString();
            var periodStart = selectCriteria.PeriodStart?.ToString("yyyy-MM-dd");
            var periodEnd = selectCriteria.PeriodEnd?.ToString("yyyy-MM-dd"); ;
            var supplierId = (selectCriteria.SupplierId != null) ? selectCriteria.SupplierId.ToString() : "";
            var referenceNo = (selectCriteria.ReferenceNo != null) ? selectCriteria.ReferenceNo.ToString() : "";
            var arrayString = string.IsNullOrEmpty(selectCriteria.ArrayString) ? "" : selectCriteria.ArrayString;

            var list = await _context.SPBills
                .FromSqlInterpolated($"CALL GetBills({userConfigId}, {periodStart}, {periodEnd}, {supplierId}, {referenceNo}, {arrayString})")
                .ToListAsync();

            var result = list
                .Select(e => new
                {
                    e.Id,
                    e.BillNo,
                    e.BillDate,
                    e.DueDate,
                    e.SupplierId,
                    e.SupplierName,
                    e.SupplierTIN,
                    e.Amount,
                    e.Balance,
                    e.PaymentTermId,
                    e.PaymentTermName,
                    e.Notes,
                    e.Status,
                    e.ResponsibilityCenterEntry,
                    StatusName = GetStatusName(e)
                }).OrderBy(e => e.BillDate).ThenBy(e => e.BillNo).ToList();

            return Ok(result);
        }

        // GET: api/Bills/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Bill>> GetBill(int id)
        {
            var bill = await _context.Bills.Where(e => e.Id == id)
                .Include(e => e.Supplier).ThenInclude(e => e.SupplierAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
                .Include(e => e.Supplier).ThenInclude(e => e.SupplierContacts)
                .Include(e => e.PaymentTerm)
                .Include(e => e.BillDetails).ThenInclude(e => e.Item).ThenInclude(e => e.PurchaseTaxRate)
                .Include(e => e.BillDetails).ThenInclude(e => e.TaxRate)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.InventoryLocation)
                .SingleOrDefaultAsync();

            if (bill == null)
            {
                return NotFound();
            }

            return bill;
        }

        // PUT: api/Bills/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBill(int id, Bill bill)
        {
            if (id != bill.Id)
            {
                return BadRequest();
            }

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
                // Convert date to local timezone
                var itemInventory = ItemsController.GetInventoryItem(_context, e.ItemId).Result;

                if (e.Id == 0)
                {
                    e.CreatedDate = DateTime.Now;
                    _context.BillDetails.Add(e);

                    // Calculate average cost based on landed cost ////////////////////////////////////////////////////////////////////////////
                    
                    decimal? newAverageCost = 0;

                    var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                    if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0)
                    {
                        newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * e.Quantity)) / (itemInventory.Quantity + e.Quantity);
                    }

                    var item = _context.Items.FindAsync(e.ItemId).Result;
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
                            itemInventory.Quantity > e.Quantity )
                        {
                            newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * -e.Quantity)) / (itemInventory.Quantity + -e.Quantity);
                        }

                        var item = _context.Items.FindAsync(e.ItemId).Result;
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
                            var d = await _context.BillDetails.AsNoTracking().Where(d => d.Id == e.Id).FirstOrDefaultAsync();
                            var landedCost = d.Rate + (d.LandedCost.HasValue ? d.LandedCost / d.Quantity : 0);
                            decimal? newAverageCost = 0;

                            var itemInventory0 = ItemsController.GetInventoryItem(_context, d.ItemId).Result;

                            if (itemInventory0 != null && itemInventory0.AverageCost.HasValue && 
                                itemInventory0.AverageCost > 0 && itemInventory0.Quantity > 0 &&
                                itemInventory0.Quantity > d.Quantity )
                            {
                                newAverageCost = ((itemInventory0.Quantity * itemInventory0.AverageCost) - (landedCost * d.Quantity)) / (itemInventory0.Quantity - d.Quantity);
                                newAverageCost = (newAverageCost > 0) ? newAverageCost : landedCost;
                            }                 

                            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                            if (d.ItemId == e.ItemId)
                            {
                                var item0 = _context.Items.FindAsync(d.ItemId).Result;

                                landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);
                                var itemQuantity = itemInventory0.Quantity - d.Quantity;

                                if (newAverageCost > 0 && itemQuantity > 0)
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
                                var item0 = _context.Items.FindAsync(d.ItemId).Result;

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

                                if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0)
                                {
                                    newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * e.Quantity)) / (itemInventory.Quantity + e.Quantity);
                                }

                                var item1 = _context.Items.FindAsync(e.ItemId).Result;
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
        public async Task<ActionResult<Bill>> PostBill(Bill bill)
        {

            var b = await _context.Bills.FirstOrDefaultAsync(e => e.BillNo == bill.BillNo && e.UserConfigId == bill.UserConfigId);
            if (b != null)
            {
                return Conflict($"Bill# {bill.BillNo} already exist.");
            }

            JournalEntriesController.SanitizeEntries(bill.JournalEntries);
            bill.CreatedDate = DateTime.Now;

            BillDetail[] distinctItems = new BillDetail[0];

            // Details
            foreach (var e in bill.BillDetails)
            {
                e.CreatedDate = DateTime.Now;

                // Calculate average cost based on landed cost ////////////////////////////////////////////////////////////////////////////
                var itemInventory = ItemsController.GetInventoryItem(_context, e.ItemId).Result;
                decimal? newAverageCost = 0;

                var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0)
                {
                    newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * e.Quantity)) / (itemInventory.Quantity + e.Quantity);
                }

                var item = _context.Items.FindAsync(e.ItemId).Result;
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

            return CreatedAtAction("GetBill", new { id = bill.Id }, bill);
        }

        // DELETE: api/Bills/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBill(int id)
        {
            var bill = await _context.Bills.Where(e => e.Id == id)
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

            // Journal Entries
            foreach (var e in bill.JournalEntries.ToList())
            {
                _context.Entry(e).State = EntityState.Deleted;
            }

            // Bill Details
            foreach (var e in bill.BillDetails.ToList())
            {
                var itemInventory = ItemsController.GetInventoryItem(_context, e.ItemId).Result;
                decimal? newAverageCost = 0;
                var landedCost = e.Rate + (e.LandedCost.HasValue ? e.LandedCost / e.Quantity : 0);

                if (itemInventory != null && itemInventory.AverageCost.HasValue && itemInventory.AverageCost > 0 && itemInventory.Quantity > 0)
                {
                    newAverageCost = ((itemInventory.Quantity * itemInventory.AverageCost) + (landedCost * -e.Quantity)) / (itemInventory.Quantity + -e.Quantity);
                }

                var item = _context.Items.FindAsync(e.ItemId).Result;
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
            return _context.Bills.Any(e => e.Id == id);
        }

        private static string GetStatusName(SPBill bill)
        {
            string status = "";
            switch (bill.Status)
            {
                case -1:
                    status = "Deleted";
                    break;
                case 0:
                    status = "Draft";
                    break;
                case 1:
                    if (bill.Balance == 0) status = "Paid";
                    if (bill.Balance < bill.Amount && bill.Balance > 0) status = "Partially paid";
                    if (bill.Balance == bill.Amount && bill.DueDate == DateTime.Now.Date) status = "Due today";
                    if (bill.Balance == bill.Amount && bill.DueDate > DateTime.Now.Date) status = $"Due in {(bill.DueDate - DateTime.Now.Date).Days} days";
                    if (bill.Balance == bill.Amount && bill.DueDate < DateTime.Now.Date) status = $"{(DateTime.Now.Date - bill.DueDate).Days} days overdue";
                    break;
            }
            return status;
        }
    }
}
