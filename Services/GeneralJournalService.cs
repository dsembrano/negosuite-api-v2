using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
namespace negosuite_api.Services;

public sealed class GeneralJournalService
{
    private readonly negosuiteContext db;
    public GeneralJournalService(negosuiteContext db) => this.db = db;
    public IQueryable<GeneralJournalListItemDto> Query(int company, TransactionListCriteria filter, string centers, short? status)
    {
        var start = filter.PeriodStart?.Date; var end = filter.PeriodEnd?.Date;
        var usePeriod = start.HasValue && end.HasValue;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        return db.Database.SqlQuery<GeneralJournalListItemDto>($"""
            SELECT h.Id, h.ReferenceNo, h.ReferenceDate, h.Notes, h.ResponsibilityCenterEntry, h.Status, CASE WHEN h.Status = 1 THEN 'Posted' WHEN h.Status = -1 THEN 'Deleted' ELSE 'Draft' END AS StatusName
            FROM generaljournal h 
            WHERE h.UserConfigId = {company} AND (({status} IS NOT NULL AND h.Status = {status}) OR ({status} IS NULL AND ({filter.ShowDeleted == true} OR h.Status <> -1)))
                AND ({!usePeriod} OR (h.ReferenceDate >= {start} AND h.ReferenceDate <= {end}))
                AND ({reference} IS NULL OR h.ReferenceNo = {reference}) 
                AND ({centers} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(h.ResponsibilityCenterEntry, '$[*].id'), {centers}, '$'))
            """);
    }
    public async Task<object> ListAsync(int company, TransactionListCriteria filter, string centers, int? page, int? size, string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = GeneralJournalQuery.Search(Query(company, filter, centers, status), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = GeneralJournalQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<GeneralJournalListItemDto>(items, page.Value, size.Value, count) : items;
    }
    public async Task<GeneralJournalDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var entity = await db.GeneralJournals.AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(j => j.Id == id && j.UserConfigId == company)
                .Include(j => j.JournalEntries).ThenInclude(j => j.Account).ThenInclude(a => a.Category)
                .Include(j => j.JournalEntries).ThenInclude(j => j.Customer)
                .Include(j => j.JournalEntries).ThenInclude(j => j.Supplier)
                .Include(j => j.JournalEntries).ThenInclude(j => j.TaxRate)
                .Include(j => j.JournalEntries).ThenInclude(j => j.PaymentToJournalEntry)
                .SingleOrDefaultAsync(ct);
        if (entity != null && entity.Status != -1) entity.JournalEntries = entity.JournalEntries.Where(j => j.Status != -1).ToList();
        return new TransactionResponseMapping().Map(entity);
    }
    public async Task<string> ValidateWriteAsync(int company, int? id, GeneralJournal entity, CancellationToken ct)
    {
        var validator = new TransactionWriteValidator(db);
        var error = await validator.HeaderAsync(company, null, null, Array.Empty<int?>(), Array.Empty<int?>(), ct);
        if (error != null) return error;
        return await validator.JournalsAsync(company, id, "GeneralJournalId", entity.JournalEntries, j => j.GeneralJournalId, ct);
    }
}