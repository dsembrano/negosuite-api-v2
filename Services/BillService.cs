using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Bills;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class BillService
{
    private readonly negosuiteContext db;
    public BillService(negosuiteContext db) => this.db = db;

    public IQueryable<BillListItemDto> Query(int company, BillListCriteria filter, string centersJson, short? status, DateTime today)
    {
        // Match the live GetBills SELECT, including both-bound dates and all-center containment.
        // Unlike CALL, this parameterized SELECT can be composed with SQL COUNT, ORDER BY and LIMIT.
        var usePeriod = filter.PeriodStart.HasValue && filter.PeriodEnd.HasValue;
        var start = filter.PeriodStart?.Date;
        var end = filter.PeriodEnd?.Date;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        var state = status ?? 1;
        return db.Database.SqlQuery<BillListItemDto>($"""
            SELECT si.Id, si.BillNo, si.BillDate, si.DueDate, 
                si.SupplierId, c.Name AS SupplierName, c.TIN AS SupplierTIN,
                si.Amount, si.Balance, si.PaymentTermId, pt.Name AS PaymentTermName,
                si.Notes, si.Status, si.ResponsibilityCenterEntry,
                CASE
                    WHEN si.Status = -1 THEN 'Deleted'
                    WHEN si.Status = 0 THEN 'Draft'
                    WHEN si.Status = 1 AND si.Balance = si.Amount AND si.DueDate = {today} THEN 'Due today'
                    WHEN si.Status = 1 AND si.Balance = si.Amount AND si.DueDate > {today}
                        THEN CONCAT('Due in ', TIMESTAMPDIFF(DAY, {today}, si.DueDate), ' days')
                    WHEN si.Status = 1 AND si.Balance = si.Amount AND si.DueDate < {today}
                        THEN CONCAT(TIMESTAMPDIFF(DAY, si.DueDate, {today}), ' days overdue')
                    WHEN si.Status = 1 AND si.Balance = 0 THEN 'Paid'
                    WHEN si.Status = 1 AND si.Balance < si.Amount AND si.Balance > 0 THEN 'Partially paid'
                    ELSE ''
                END AS StatusName
            FROM bill AS si
            LEFT JOIN supplier AS c ON c.Id = si.SupplierId
            LEFT JOIN paymentterm AS pt ON pt.Id = si.PaymentTermId
            WHERE si.UserConfigId = {company} AND si.Status = {state}
                AND ({!usePeriod} OR (si.BillDate >= {start} AND si.BillDate <= {end}))
                AND ({filter.SupplierId} IS NULL OR si.SupplierId = {filter.SupplierId})
                AND ({reference} IS NULL OR si.BillNo = {reference})
                AND ({centersJson} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(si.ResponsibilityCenterEntry, '$[*].id'), {centersJson}, '$'))
            """);
    }

    public async Task<object> ListAsync(int company, BillListCriteria filter, string centersJson, int? page, int? size,
        string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = BillQuery.Search(Query(company, filter, centersJson, status, DateTime.Today), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = BillQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<BillListItemDto>(items, page.Value, size.Value, count) : items;
    }

    public async Task<BillDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var bill = await db.Bills.AsNoTrackingWithIdentityResolution().AsSplitQuery()
        .Where(b => b.Id == id && b.UserConfigId == company)
        .Include(b => b.Supplier).ThenInclude(s => s.SupplierAddresses).ThenInclude(a => a.CityMunicipality).ThenInclude(c => c.StateProvince)
        .Include(b => b.Supplier).ThenInclude(s => s.SupplierContacts).Include(b => b.PaymentTerm)
        .Include(b => b.BillDetails).ThenInclude(d => d.Item).ThenInclude(i => i.PurchaseTaxRate)
        .Include(b => b.BillDetails).ThenInclude(d => d.TaxRate)
        .Include(b => b.JournalEntries).ThenInclude(j => j.Account).ThenInclude(a => a.Category)
        .Include(b => b.JournalEntries).ThenInclude(j => j.Customer)
        .Include(b => b.JournalEntries).ThenInclude(j => j.Supplier)
        .Include(b => b.InventoryLocation).SingleOrDefaultAsync(ct);
        var result = new TransactionResponseMapping().Map(bill);
        if (result != null && await PurchaseWorkflowService.Installed(db, ct)) result.PurchaseWorkflowId = await db.PurchaseWorkflowDocuments.Where(d => d.UserConfigId == company && d.Kind == "PB" && d.LegacyId == id).Select(d => (int?)d.Id).SingleOrDefaultAsync(ct);
        return result;
    }

    public async Task<string> ValidateWriteAsync(int company, int? id, Bill bill, CancellationToken ct)
    {
        if (bill.BillDetails == null || bill.JournalEntries == null || bill.BillDetails.Any(d => d == null) || bill.JournalEntries.Any(j => j == null))
            return "Bill details and journal entries must be non-null arrays without null entries.";
        if (!await db.Suppliers.AnyAsync(s => s.Id == bill.SupplierId && s.UserConfigId == company, ct)) return "Invalid supplier for this company.";
        if (bill.PaymentTermId.HasValue && !await db.PaymentTerms.AnyAsync(p => p.Id == bill.PaymentTermId, ct)) return "Invalid payment term.";
        var details = bill.BillDetails;
        var journals = bill.JournalEntries;
        if (details.Any(d => d.Id < 0 || (d.BillId != 0 && d.BillId != id)) || journals.Any(j => j.Id < 0 || j.UserConfigId != company || (j.BillId.HasValue && j.BillId != 0 && j.BillId != id)))
            return "Entries must belong to this bill and company.";
        var detailIds = details.Where(d => d.Id != 0).Select(d => d.Id).ToArray();
        var journalIds = journals.Where(j => j.Id != 0).Select(j => j.Id).ToArray();
        if (detailIds.Distinct().Count() != detailIds.Length || journalIds.Distinct().Count() != journalIds.Length) return "Duplicate entry IDs are not allowed.";
        if ((!id.HasValue && (detailIds.Length > 0 || journalIds.Length > 0)) ||
            detailIds.Length != await db.BillDetails.CountAsync(d => detailIds.Contains(d.Id) && d.BillId == id, ct) ||
            journalIds.Length != await db.JournalEntries.CountAsync(j => journalIds.Contains(j.Id) && j.BillId == id && j.UserConfigId == company, ct)) return "One or more entries do not belong to this bill.";
        if (details.Any(d => d.Quantity == 0 && d.LandedCost.HasValue)) return "A bill detail with landed cost must have a nonzero quantity.";
        var originals = await db.BillDetails.AsNoTracking().Where(d => detailIds.Contains(d.Id)).ToListAsync(ct);
        if (originals.Any(d => d.Quantity == 0 && d.LandedCost.HasValue)) return "An existing bill detail has landed cost with zero quantity.";
        var items = details.Select(d => d.ItemId).Concat(originals.Select(d => d.ItemId)).Distinct().ToArray();
        if (items.Length != await db.Items.CountAsync(i => items.Contains(i.Id) && i.UserConfigId == company, ct)) return "Invalid item for this company.";
        var accounts = journals.Where(j => j.Deleted != true).Select(j => j.AccountId).Distinct().ToArray();
        if (accounts.Length != await db.Accounts.CountAsync(a => accounts.Contains(a.Id) && a.UserConfigId == company, ct)) return "Invalid account for this company.";
        var suppliers = journals.Where(j => j.Deleted != true && j.SupplierId.HasValue).Select(j => j.SupplierId.Value).Distinct().ToArray();
        if (suppliers.Length != await db.Suppliers.CountAsync(s => suppliers.Contains(s.Id) && s.UserConfigId == company, ct)) return "Invalid journal supplier for this company.";
        var customers = journals.Where(j => j.Deleted != true && j.CustomerId.HasValue).Select(j => j.CustomerId.Value).Distinct().ToArray();
        if (customers.Length != await db.Customers.CountAsync(c => customers.Contains(c.Id) && c.UserConfigId == company, ct)) return "Invalid journal customer for this company.";
        var locations = details.Where(d => d.InventoryLocationId.HasValue).Select(d => d.InventoryLocationId.Value)
            .Concat(bill.InventoryLocationId.HasValue ? new[] { bill.InventoryLocationId.Value } : Array.Empty<int>()).Distinct().ToArray();
        if (locations.Length != await db.InventoryLocations.CountAsync(l => locations.Contains(l.Id) && l.UserConfigId == company, ct)) return "Invalid inventory location for this company.";
        var taxes = details.Where(d => d.TaxRateId.HasValue).Select(d => d.TaxRateId.Value).Distinct().ToArray();
        if (taxes.Length != await db.TaxRates.CountAsync(t => taxes.Contains(t.Id) && t.UserConfigId == company, ct)) return "Invalid tax rate for this company.";
        return null;
    }
}
