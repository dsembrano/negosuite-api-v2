using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
namespace negosuite_api.Services;

public sealed class StockTransferService
{
    private readonly negosuiteContext db;
    public StockTransferService(negosuiteContext db) => this.db = db;
    public IQueryable<StockTransferListItemDto> Query(int company, TransactionListCriteria filter, string centers, short? status)
    {
        var start = filter.PeriodStart?.Date; var end = filter.PeriodEnd?.Date;
        var usePeriod = start.HasValue && end.HasValue;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        return db.Database.SqlQuery<StockTransferListItemDto>($"""
            SELECT h.Id, h.ReferenceNo, h.ReferenceDate, h.Notes, f.Name AS FromInventoryLocationName, t.Name AS ToInventoryLocationName, h.ResponsibilityCenterEntry, h.Status, CASE h.Status WHEN -1 THEN 'Deleted' WHEN 0 THEN 'Draft' WHEN 1 THEN 'Posted' ELSE '' END AS StatusName
            FROM stocktransfer h 
            LEFT JOIN inventorylocation f ON f.Id = h.FromInventoryLocationId
            LEFT JOIN inventorylocation t ON t.Id = h.ToInventoryLocationId
            WHERE h.UserConfigId = {company} AND h.Status = {status ?? 1}
                AND ({!usePeriod} OR (h.ReferenceDate >= {start} AND h.ReferenceDate <= {end}))
                AND ({reference} IS NULL OR h.ReferenceNo = {reference}) 
                AND ({centers} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(h.ResponsibilityCenterEntry, '$[*].id'), {centers}, '$'))
            """);
    }
    public async Task<object> ListAsync(int company, TransactionListCriteria filter, string centers, int? page, int? size, string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = StockTransferQuery.Search(Query(company, filter, centers, status), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = StockTransferQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<StockTransferListItemDto>(items, page.Value, size.Value, count) : items;
    }
    public async Task<StockTransferDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var entity = await db.StockTransfers.AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(e => e.Id == id && e.UserConfigId == company)
                .Include(e => e.StockTransferDetails).ThenInclude(e => e.Item).ThenInclude(e => e.InventoryAccount).ThenInclude(a => a.Category)
                .Include(e => e.FromInventoryLocation)
                .Include(e => e.ToInventoryLocation)
                .SingleOrDefaultAsync(ct);

        return new TransactionResponseMapping().Map(entity);
    }
    public async Task<string> ValidateWriteAsync(int company, int? id, StockTransfer entity, CancellationToken ct)
    {
        var validator = new TransactionWriteValidator(db);
        var error = await validator.HeaderAsync(company, null, null, new int?[] { entity.FromInventoryLocationId, entity.ToInventoryLocationId }, Array.Empty<int?>(), ct);
        if (error != null) return error;
        if (entity.StockTransferDetails == null || entity.StockTransferDetails.Any(d => d == null)) return "Details must be a non-null array without null entries.";
        error = await validator.LinesAsync<StockTransferDetail>(company, id, "StockTransferId", entity.StockTransferDetails.Select(d => (d.Id, d.StockTransferId, d.ItemId, (int?)null, (int?)null)).ToArray(), ct);
        if (error != null) return error;
        return null;
    }
}