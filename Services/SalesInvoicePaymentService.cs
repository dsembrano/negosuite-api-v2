using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.SalesInvoicePayments;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class SalesInvoicePaymentService
{
    private readonly negosuiteContext db;
    public SalesInvoicePaymentService(negosuiteContext db) => this.db = db;

    public IQueryable<SalesInvoicePaymentListItemDto> Query(int company, SalesInvoicePaymentListCriteria filter, string centersJson, short? status)
    {
        var usePeriod = filter.PeriodStart.HasValue && filter.PeriodEnd.HasValue;
        var start = filter.PeriodStart?.Date;
        var end = filter.PeriodEnd?.Date;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        var state = status ?? 1;
        return db.Database.SqlQuery<SalesInvoicePaymentListItemDto>($"""
            SELECT p.Id, p.ReferenceNo, p.ReferenceDate, p.CustomerId, c.Name AS CustomerName,
                p.PaymentModeId, m.Name AS PaymentModeName, p.DepositToAccountId, a.Name AS DepositToAccountName,
                p.Amount, p.Balance, p.Notes, p.Status, p.ResponsibilityCenterEntry,
                CASE WHEN p.Status = -1 THEN 'Deleted' WHEN p.Status = 0 THEN 'Draft'
                    WHEN p.Status = 1 AND (p.Balance <=> p.Amount) THEN 'Unapplied'
                    WHEN p.Status = 1 AND p.Balance = 0 THEN 'Fully applied'
                    WHEN p.Status = 1 AND p.Balance > 0 THEN 'Partially applied'
                    ELSE '' END AS StatusName
            FROM salesinvoicepayment AS p
            LEFT JOIN customer AS c ON c.Id = p.CustomerId
            LEFT JOIN paymentmode AS m ON m.Id = p.PaymentModeId
            LEFT JOIN account AS a ON a.Id = p.DepositToAccountId
            WHERE p.UserConfigId = {company} AND p.Status = {state}
                AND ({!usePeriod} OR (p.ReferenceDate >= {start} AND p.ReferenceDate <= {end}))
                AND ({filter.CustomerId} IS NULL OR p.CustomerId = {filter.CustomerId})
                AND ({reference} IS NULL OR p.ReferenceNo = {reference})
                AND ({centersJson} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(p.ResponsibilityCenterEntry, '$[*].id'), {centersJson}, '$'))
            """);
    }

    public async Task<object> ListAsync(int company, SalesInvoicePaymentListCriteria filter, string centersJson, int? page, int? size,
        string search, string sort, string direction, short? status, CancellationToken ct)
    {
        var query = SalesInvoicePaymentQuery.Search(Query(company, filter, centersJson, status), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = SalesInvoicePaymentQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<SalesInvoicePaymentListItemDto>(items, page.Value, size.Value, count) : items;
    }

    public Task<SalesInvoicePayment> GetAsync(int company, int id, CancellationToken ct) => db.SalesInvoicePayments
        .AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(e => e.Id == id && e.UserConfigId == company)
        .Include(e => e.Customer).Include(e => e.PaymentMode).Include(e => e.DepositToAccount)
        .Include(e => e.JournalEntries).ThenInclude(e => e.Account).ThenInclude(a => a.Category)
        .Include(e => e.JournalEntries).ThenInclude(e => e.PaymentToJournalEntry).SingleOrDefaultAsync(ct);

    public async Task<string> ValidateWriteAsync(int company, int? id, SalesInvoicePayment payment, CancellationToken ct)
    {
        if (payment.JournalEntries == null || payment.JournalEntries.Any(j => j == null)) return "Journal entries must be a non-null array without null entries.";
        var error = await new SalesCollectionValidation(db).ReferencesAsync(company, payment.CustomerId, payment.PaymentModeId, payment.DepositToAccountId, payment.JournalEntries, ct);
        if (error != null) return error;
        var journals = payment.JournalEntries;
        if (journals.Any(j => j.Id < 0 || (j.SalesInvoicePaymentId.HasValue && j.SalesInvoicePaymentId != 0 && j.SalesInvoicePaymentId != id))) return "Journal entries must belong to this payment.";
        var journalIds = journals.Where(j => j.Id != 0).Select(j => j.Id).ToArray();
        if (journalIds.Distinct().Count() != journalIds.Length) return "Duplicate journal entry IDs are not allowed.";
        if ((!id.HasValue && journalIds.Length > 0) || journalIds.Length != await db.JournalEntries.CountAsync(j => journalIds.Contains(j.Id) && j.SalesInvoicePaymentId == id && j.UserConfigId == company, ct))
            return "One or more entries do not belong to this payment.";
        // Check every payment target before the controller changes any balances, including deleted entries.
        var targets = journals.Where(j => j.PaymentToJournalEntryId.HasValue).Select(j => j.PaymentToJournalEntryId.Value).Distinct().ToArray();
        if (targets.Length != await db.JournalEntries.CountAsync(j => targets.Contains(j.Id) && j.UserConfigId == company &&
            (j.Source != "SI" || db.SalesInvoices.Any(i => i.Id == j.SalesInvoiceId && i.UserConfigId == company)), ct))
            return "Invoice journal entry not found for this company.";
        return null;
    }
}
