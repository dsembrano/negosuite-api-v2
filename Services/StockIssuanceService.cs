using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
namespace negosuite_api.Services;

public sealed class StockIssuanceService
{
    private readonly negosuiteContext db;
    public StockIssuanceService(negosuiteContext db) => this.db = db;
    public IQueryable<StockIssuanceListItemDto> Query(int company, TransactionListCriteria filter, string centers, short? status)
    {
        var start = filter.PeriodStart?.Date; var end = filter.PeriodEnd?.Date;
        var usePeriod = start.HasValue && end.HasValue;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        return db.Database.SqlQuery<StockIssuanceListItemDto>($"""
            SELECT h.Id, h.ReferenceNo, h.ReferenceDate, h.CustomerId, c.Name AS CustomerName, h.Notes, l.Name AS InventoryLocationName, h.ResponsibilityCenterEntry, h.Status, CASE h.Status WHEN -1 THEN 'Deleted' WHEN 0 THEN 'Draft' WHEN 1 THEN 'Posted' ELSE '' END AS StatusName
            FROM stockissuance h 
            LEFT JOIN inventorylocation l ON l.Id = h.InventoryLocationId
            LEFT JOIN customer c ON c.Id = h.CustomerId
            WHERE h.UserConfigId = {company} AND h.Status = {status ?? 1}
                AND ({!usePeriod} OR (h.ReferenceDate >= {start} AND h.ReferenceDate <= {end}))
                AND ({reference} IS NULL OR h.ReferenceNo = {reference}) 
                AND ({filter.SupplierId} IS NULL OR h.SupplierId = {filter.SupplierId})
                AND ({filter.CustomerId} IS NULL OR h.CustomerId = {filter.CustomerId})
                AND ({centers} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(h.ResponsibilityCenterEntry, '$[*].id'), {centers}, '$'))
            """);
    }
    public async Task<object> ListAsync(int company, TransactionListCriteria filter, string centers, int? page, int? size, string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = StockIssuanceQuery.Search(Query(company, filter, centers, status), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = StockIssuanceQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<StockIssuanceListItemDto>(items, page.Value, size.Value, count) : items;
    }
    public async Task<StockIssuanceDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var entity = await db.StockIssuances.AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(e => e.Id == id && e.UserConfigId == company)
                .Include(e => e.StockIssuanceDetails).ThenInclude(e => e.Item)
                .Include(e => e.InventoryLocation)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Customer)
                .Include(e => e.JournalEntries).ThenInclude(e => e.Supplier)
                .Include(e => e.Customer).Include(e => e.Supplier)
                .SingleOrDefaultAsync(ct);

        return new TransactionResponseMapping().Map(entity);
    }
    public async Task<string> ValidateWriteAsync(int company, int? id, StockIssuance entity, CancellationToken ct)
    {
        var validator = new TransactionWriteValidator(db);
        var error = await validator.HeaderAsync(company, entity.SupplierId, entity.CustomerId, new int?[] { entity.InventoryLocationId }, Array.Empty<int?>(), ct);
        if (error != null) return error;
        if (entity.StockIssuanceDetails == null || entity.StockIssuanceDetails.Any(d => d == null)) return "Details must be a non-null array without null entries.";
        error = await validator.LinesAsync<StockIssuanceDetail>(company, id, "StockIssuanceId", entity.StockIssuanceDetails.Select(d => (d.Id, d.StockIssuanceId, d.ItemId, (int?)null, (int?)null)).ToArray(), ct);
        if (error != null) return error;
        return await validator.JournalsAsync(company, id, "StockIssuanceId", entity.JournalEntries, j => null, ct);
    }
}