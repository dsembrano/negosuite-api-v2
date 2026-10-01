using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.SalesReceipts;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class SalesReceiptService
{
    private readonly negosuiteContext db;
    public SalesReceiptService(negosuiteContext db) => this.db = db;

    public IQueryable<SalesReceiptListItemDto> Query(int company, SalesReceiptListCriteria filter, string centersJson, short? status)
    {
        var usePeriod = filter.PeriodStart.HasValue && filter.PeriodEnd.HasValue;
        var start = filter.PeriodStart?.Date;
        var end = filter.PeriodEnd?.Date;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        var state = status ?? 1;
        // Match GetSalesReceipts; false/omitted IsPOS includes both POS and regular receipts.
        return db.Database.SqlQuery<SalesReceiptListItemDto>($"""
            SELECT r.Id, r.ReceiptNo, r.ReceiptDate, r.CustomerId, c.Name AS CustomerName, c.TIN AS CustomerTIN,
                r.BillingAddress, r.BillingContactName, r.BillingContactEmail,
                r.ShippingAddress, r.ShippingContactName, r.ShippingContactEmail,
                r.Amount, r.Balance, r.PaymentModeId, m.Name AS PaymentModeName, r.Notes, r.Status,
                r.ResponsibilityCenterEntry, r.CreatedDate,
                CASE r.Status WHEN -1 THEN 'Deleted' WHEN 0 THEN 'Draft' WHEN 1 THEN 'Posted' ELSE '' END AS StatusName
            FROM salesreceipt AS r
            LEFT JOIN customer AS c ON c.Id = r.CustomerId
            LEFT JOIN paymentmode AS m ON m.Id = r.PaymentModeId
            WHERE r.UserConfigId = {company} AND r.Status = {state}
                AND ({!usePeriod} OR (r.ReceiptDate >= {start} AND r.ReceiptDate <= {end}))
                AND ({filter.CustomerId} IS NULL OR r.CustomerId = {filter.CustomerId})
                AND ({reference} IS NULL OR r.ReceiptNo = {reference})
                AND ({filter.IsPOS != true} OR r.IsPOS = 1)
                AND ({centersJson} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(r.ResponsibilityCenterEntry, '$[*].id'), {centersJson}, '$'))
            """);
    }

    public async Task<object> ListAsync(int company, SalesReceiptListCriteria filter, string centersJson, int? page, int? size,
        string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = SalesReceiptQuery.Search(Query(company, filter, centersJson, status), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = SalesReceiptQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<SalesReceiptListItemDto>(items, page.Value, size.Value, count) : items;
    }

    public Task<SalesReceipt> GetAsync(int company, int id, CancellationToken ct) => db.SalesReceipts
        .AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(e => e.Id == id && e.UserConfigId == company)
        .Include(e => e.Customer).ThenInclude(e => e.CustomerAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
        .Include(e => e.Customer).ThenInclude(e => e.CustomerContacts)
        .Include(e => e.PaymentMode).Include(e => e.DepositToAccount)
        .Include(e => e.SalesReceiptDetails).ThenInclude(e => e.Item).ThenInclude(e => e.SalesTaxRate)
        .Include(e => e.SalesReceiptDetails).ThenInclude(e => e.TaxRate)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
        .Include(e => e.InventoryLocation).SingleOrDefaultAsync(ct);

    public async Task<string> ValidateWriteAsync(int company, int? id, SalesReceipt receipt, CancellationToken ct)
    {
        if (receipt.SalesReceiptDetails == null || receipt.JournalEntries == null ||
            receipt.SalesReceiptDetails.Any(d => d == null) || receipt.JournalEntries.Any(j => j == null))
            return "Receipt details and journal entries must be non-null arrays without null entries.";
        var error = await new SalesCollectionValidation(db).ReferencesAsync(company, receipt.CustomerId, receipt.PaymentModeId, receipt.DepositToAccountId, receipt.JournalEntries, ct);
        if (error != null) return error;
        if (receipt.InventoryLocationId.HasValue && !await db.InventoryLocations.AnyAsync(l => l.Id == receipt.InventoryLocationId && l.UserConfigId == company, ct)) return "Invalid inventory location for this company.";
        var details = receipt.SalesReceiptDetails;
        var journals = receipt.JournalEntries;
        if (details.Any(d => d.Id < 0 || (d.SalesReceiptId != 0 && d.SalesReceiptId != id)) ||
            journals.Any(j => j.Id < 0 || (j.SalesReceiptId.HasValue && j.SalesReceiptId != 0 && j.SalesReceiptId != id)))
            return "Receipt entries must belong to this receipt.";
        var detailIds = details.Where(d => d.Id != 0).Select(d => d.Id).ToArray();
        var journalIds = journals.Where(j => j.Id != 0).Select(j => j.Id).ToArray();
        if (detailIds.Distinct().Count() != detailIds.Length || journalIds.Distinct().Count() != journalIds.Length) return "Duplicate entry IDs are not allowed.";
        if ((!id.HasValue && (detailIds.Length > 0 || journalIds.Length > 0)) ||
            detailIds.Length != await db.SalesReceiptDetails.CountAsync(d => detailIds.Contains(d.Id) && d.SalesReceiptId == id, ct) ||
            journalIds.Length != await db.JournalEntries.CountAsync(j => journalIds.Contains(j.Id) && j.SalesReceiptId == id && j.UserConfigId == company, ct))
            return "One or more entries do not belong to this receipt.";
        var itemIds = details.Where(d => d.Deleted != true).Select(d => d.ItemId).Distinct().ToArray();
        if (itemIds.Length != await db.Items.CountAsync(i => itemIds.Contains(i.Id) && i.UserConfigId == company, ct)) return "Invalid item for this company.";
        var taxIds = details.Where(d => d.Deleted != true && d.TaxRateId.HasValue).Select(d => d.TaxRateId.Value).Distinct().ToArray();
        if (taxIds.Length != await db.TaxRates.CountAsync(t => taxIds.Contains(t.Id) && t.UserConfigId == company, ct)) return "Invalid tax rate for this company.";
        var locationIds = details.Where(d => d.Deleted != true && d.InventoryLocationId.HasValue).Select(d => d.InventoryLocationId.Value).Distinct().ToArray();
        if (locationIds.Length != await db.InventoryLocations.CountAsync(l => locationIds.Contains(l.Id) && l.UserConfigId == company, ct)) return "Invalid inventory location for this company.";
        return null;
    }
}
