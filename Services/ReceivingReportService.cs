using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
namespace negosuite_api.Services;

public sealed class ReceivingReportService
{
    private readonly negosuiteContext db;
    public ReceivingReportService(negosuiteContext db) => this.db = db;
    public IQueryable<ReceivingReportListItemDto> Query(int company, TransactionListCriteria filter, string centers, short? status)
    {
        var start = filter.PeriodStart?.Date; var end = filter.PeriodEnd?.Date;
        var usePeriod = start.HasValue && end.HasValue;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        return db.Database.SqlQuery<ReceivingReportListItemDto>($"""
            SELECT h.Id, h.ReferenceNo, h.ReferenceDate, h.SupplierId, s.Name AS SupplierName, h.Amount, h.Balance, h.DeliveryReceiptNo, h.PurchaseOrderNo, l.Name AS InventoryLocationName, h.Notes, h.ResponsibilityCenterEntry, h.Status, CASE h.Status WHEN -1 THEN 'Deleted' WHEN 0 THEN 'Draft' WHEN 1 THEN 'Posted' ELSE '' END AS StatusName
            FROM receivingreport h 
            LEFT JOIN inventorylocation l ON l.Id = h.InventoryLocationId
            LEFT JOIN supplier s ON s.Id = h.SupplierId
            WHERE h.UserConfigId = {company} AND h.Status = {status ?? 1}
                AND ({!usePeriod} OR (h.ReferenceDate >= {start} AND h.ReferenceDate <= {end}))
                AND ({reference} IS NULL OR h.ReferenceNo = {reference}) 
                AND ({filter.SupplierId} IS NULL OR h.SupplierId = {filter.SupplierId})
                AND ({centers} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(h.ResponsibilityCenterEntry, '$[*].id'), {centers}, '$'))
            """);
    }
    public async Task<object> ListAsync(int company, TransactionListCriteria filter, string centers, int? page, int? size, string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = ReceivingReportQuery.Search(Query(company, filter, centers, status), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = ReceivingReportQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<ReceivingReportListItemDto>(items, page.Value, size.Value, count) : items;
    }
    public async Task<ReceivingReportDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var entity = await db.ReceivingReports.AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(e => e.Id == id && e.UserConfigId == company)
                .Include(e => e.Supplier).ThenInclude(e => e.SupplierAddresses).ThenInclude(e => e.CityMunicipality).ThenInclude(e => e.StateProvince)
                .Include(e => e.Supplier).ThenInclude(e => e.SupplierContacts)
                .Include(e => e.ReceivingReportDetails).ThenInclude(e => e.Item)
                .Include(e => e.ReceivingReportDetails).ThenInclude(e => e.TaxRate)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.InventoryLocation)
                .Include(e => e.CreditAccount)
                .SingleOrDefaultAsync(ct);

        return new TransactionResponseMapping().Map(entity);
    }
    public async Task<string> ValidateWriteAsync(int company, int? id, ReceivingReport entity, CancellationToken ct)
    {
        var validator = new TransactionWriteValidator(db);
        var error = await validator.HeaderAsync(company, entity.SupplierId, null, new int?[] { entity.InventoryLocationId }, new int?[] { entity.CreditAccountId }, ct);
        if (error != null) return error;
        if (entity.ReceivingReportDetails == null || entity.ReceivingReportDetails.Any(d => d == null)) return "Details must be a non-null array without null entries.";
        error = await validator.LinesAsync<ReceivingReportDetail>(company, id, "ReceivingReportId", entity.ReceivingReportDetails.Select(d => (d.Id, d.ReceivingReportId, d.ItemId, d.TaxRateId, d.InventoryLocationId)).ToArray(), ct);
        if (error != null) return error;
        return await validator.JournalsAsync(company, id, "ReceivingReportId", entity.JournalEntries, j => null, ct);
    }
}