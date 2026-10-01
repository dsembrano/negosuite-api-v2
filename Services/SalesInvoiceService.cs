using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.SalesInvoices;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class SalesInvoiceService
{
    private readonly negosuiteContext db;
    public SalesInvoiceService(negosuiteContext db) => this.db = db;

    public IQueryable<SalesInvoiceListItemDto> Query(int company, SalesInvoiceListCriteria filter, string centersJson, short? status, DateTime today)
    {
        // Match the live GetSalesInvoices SELECT, including both-bound dates and all-center containment.
        // Unlike CALL, this parameterized SELECT can be composed with SQL COUNT, ORDER BY and LIMIT.
        var usePeriod = filter.PeriodStart.HasValue && filter.PeriodEnd.HasValue;
        var start = filter.PeriodStart?.Date;
        var end = filter.PeriodEnd?.Date;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        var state = status ?? 1;
        return db.Database.SqlQuery<SalesInvoiceListItemDto>($"""
            SELECT si.Id, si.InvoiceNo, si.InvoiceDate, si.DueDate, si.PurchaseOrderNo,
                si.CustomerId, c.Name AS CustomerName, c.TIN AS CustomerTIN,
                si.BillingAddress, si.BillingContactName, si.BillingContactEmail,
                si.ShippingAddress, si.ShippingContactName, si.ShippingContactEmail,
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
            FROM salesinvoice AS si
            LEFT JOIN customer AS c ON c.Id = si.CustomerId
            LEFT JOIN paymentterm AS pt ON pt.Id = si.PaymentTermId
            WHERE si.UserConfigId = {company} AND si.Status = {state}
                AND ({!usePeriod} OR (si.InvoiceDate >= {start} AND si.InvoiceDate <= {end}))
                AND ({filter.CustomerId} IS NULL OR si.CustomerId = {filter.CustomerId})
                AND ({filter.SupplierId} IS NULL OR si.SupplierId = {filter.SupplierId})
                AND ({reference} IS NULL OR si.InvoiceNo = {reference})
                AND ({centersJson} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(si.ResponsibilityCenterEntry, '$[*].id'), {centersJson}, '$'))
            """);
    }

    public async Task<object> ListAsync(int company, SalesInvoiceListCriteria filter, string centersJson, int? page, int? size,
        string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = SalesInvoiceQuery.Search(Query(company, filter, centersJson, status, DateTime.Today), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = SalesInvoiceQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<SalesInvoiceListItemDto>(items, page.Value, size.Value, count) : items;
    }

    public Task<SalesInvoice> GetAsync(int company, int id, CancellationToken ct) => db.SalesInvoices
        .AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(e => e.Id == id && e.UserConfigId == company)
        .Include(e => e.Customer).ThenInclude(e => e.CustomerAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
        .Include(e => e.Customer).ThenInclude(e => e.CustomerContacts)
        .Include(e => e.Supplier).Include(e => e.PaymentTerm)
        .Include(e => e.SalesInvoiceDetails).ThenInclude(e => e.Item).ThenInclude(e => e.SalesTaxRate)
        .Include(e => e.SalesInvoiceDetails).ThenInclude(e => e.TaxRate)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
        .Include(e => e.InventoryLocation).SingleOrDefaultAsync(ct);

    public async Task<string> ValidateWriteAsync(int company, int? id, SalesInvoice invoice, CancellationToken ct)
    {
        if (invoice.SalesInvoiceDetails == null || invoice.JournalEntries == null ||
            invoice.SalesInvoiceDetails.Any(d => d == null) || invoice.JournalEntries.Any(j => j == null))
            return "Invoice details and journal entries must be non-null arrays without null entries.";
        if (!await db.Customers.AnyAsync(c => c.Id == invoice.CustomerId && c.UserConfigId == company, ct)) return "Invalid customer for this company.";
        if (invoice.SupplierId.HasValue && !await db.Suppliers.AnyAsync(s => s.Id == invoice.SupplierId && s.UserConfigId == company, ct)) return "Invalid supplier for this company.";
        if (invoice.PaymentTermId.HasValue && !await db.PaymentTerms.AnyAsync(t => t.Id == invoice.PaymentTermId, ct)) return "Invalid payment term.";
        if (invoice.InventoryLocationId.HasValue && !await db.InventoryLocations.AnyAsync(l => l.Id == invoice.InventoryLocationId && l.UserConfigId == company, ct)) return "Invalid inventory location for this company.";
        var details = invoice.SalesInvoiceDetails;
        var journals = invoice.JournalEntries;
        if (details.Any(d => d.Id < 0 || (d.SalesInvoiceId != 0 && d.SalesInvoiceId != id)) ||
            journals.Any(j => j.Id < 0 || j.UserConfigId != company || (j.SalesInvoiceId.HasValue && j.SalesInvoiceId != 0 && j.SalesInvoiceId != id)))
            return "Invoice entries must belong to this invoice and company.";
        var detailIds = details.Where(d => d.Id != 0).Select(d => d.Id).ToArray();
        var journalIds = journals.Where(j => j.Id != 0).Select(j => j.Id).ToArray();
        if (detailIds.Distinct().Count() != detailIds.Length || journalIds.Distinct().Count() != journalIds.Length) return "Duplicate invoice entry IDs are not allowed.";
        if ((!id.HasValue && (detailIds.Length > 0 || journalIds.Length > 0)) ||
            detailIds.Length != await db.SalesInvoiceDetails.CountAsync(d => detailIds.Contains(d.Id) && d.SalesInvoiceId == id, ct) ||
            journalIds.Length != await db.JournalEntries.CountAsync(j => journalIds.Contains(j.Id) && j.SalesInvoiceId == id && j.UserConfigId == company, ct))
            return "One or more entries do not belong to this invoice.";
        var itemIds = details.Where(d => d.Deleted != true).Select(d => d.ItemId).Distinct().ToArray();
        if (itemIds.Length != await db.Items.CountAsync(i => itemIds.Contains(i.Id) && i.UserConfigId == company, ct)) return "Invalid item for this company.";
        var accountIds = journals.Where(j => j.Deleted != true).Select(j => j.AccountId).Distinct().ToArray();
        if (accountIds.Length != await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.UserConfigId == company, ct)) return "Invalid account for this company.";
        return null;
    }
}
