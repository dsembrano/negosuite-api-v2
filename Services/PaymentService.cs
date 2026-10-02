using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Payments;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class PaymentService
{
    private readonly negosuiteContext db;
    public PaymentService(negosuiteContext db) => this.db = db;

    public IQueryable<PaymentListItemDto> Query(int company, PaymentListCriteria filter, string centersJson, short? status, bool? isBillPayment)
    {
        var usePeriod = filter.PeriodStart.HasValue && filter.PeriodEnd.HasValue;
        var start = filter.PeriodStart?.Date;
        var end = filter.PeriodEnd?.Date;
        var reference = string.IsNullOrEmpty(filter.ReferenceNo) ? null : filter.ReferenceNo;
        var state = status ?? 1;
        return db.Database.SqlQuery<PaymentListItemDto>($"""
            SELECT p.Id, p.ReferenceNo, p.ReferenceDate, p.SupplierId, s.Name AS SupplierName,
                p.CustomerId, c.Name AS CustomerrName, p.Payee, p.PaymentModeId, m.Name AS PaymentModeName,
                p.PaidThroughAccountId, a.Name AS PaidThroughAccountName, p.CheckNo, p.Amount, p.Balance,
                p.ResponsibilityCenterEntry, p.Notes, p.Status,
                CASE p.Status WHEN -1 THEN 'Deleted' WHEN 0 THEN 'Draft' WHEN 1 THEN 'Posted' ELSE '' END AS StatusName
            FROM payment AS p
            LEFT JOIN supplier AS s ON s.Id = p.SupplierId
            LEFT JOIN customer AS c ON c.Id = p.CustomerId
            LEFT JOIN paymentmode AS m ON m.Id = p.PaymentModeId
            LEFT JOIN account AS a ON a.Id = p.PaidThroughAccountId
            WHERE p.UserConfigId = {company} AND p.Status = {state}
                AND ({!usePeriod} OR (p.ReferenceDate >= {start} AND p.ReferenceDate <= {end}))
                AND ({filter.SupplierId} IS NULL OR p.SupplierId = {filter.SupplierId})
                AND ({reference} IS NULL OR p.ReferenceNo = {reference})
                AND ({isBillPayment} IS NULL OR p.IsBillPayment = {isBillPayment})
                AND ({centersJson} IS NULL OR JSON_CONTAINS(JSON_EXTRACT(p.ResponsibilityCenterEntry, '$[*].id'), {centersJson}, '$'))
            """);
    }

    public async Task<object> ListAsync(int company, PaymentListCriteria filter, string centersJson, int? page, int? size,
        string search, string sort, string direction, short? status, bool? isBillPayment, CancellationToken ct)
    {
        var query = PaymentQuery.Search(Query(company, filter, centersJson, status, isBillPayment), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = PaymentQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<PaymentListItemDto>(items, page.Value, size.Value, count) : items;
    }

    public async Task<PaymentDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTrackingWithIdentityResolution().AsSplitQuery().Where(p => p.Id == id && p.UserConfigId == company)
            .Include(p => p.Supplier).Include(p => p.Customer).Include(p => p.PaymentMode).Include(p => p.PaidThroughAccount)
            .Include(p => p.JournalEntries).ThenInclude(j => j.Account).ThenInclude(a => a.Category)
            .Include(p => p.JournalEntries).ThenInclude(j => j.Customer)
            .Include(p => p.JournalEntries).ThenInclude(j => j.Supplier)
            .Include(p => p.JournalEntries).ThenInclude(j => j.TaxRate)
            .Include(p => p.JournalEntries).ThenInclude(j => j.PaymentToJournalEntry).SingleOrDefaultAsync(ct);
        if (payment != null && payment.Status != -1) payment.JournalEntries = payment.JournalEntries.Where(j => j.Status != -1).ToList();
        return new TransactionResponseMapping().Map(payment);
    }

    public async Task<string> ValidateWriteAsync(int company, int? id, Payment payment, bool billFlow, CancellationToken ct)
    {
        if (payment.JournalEntries == null || payment.JournalEntries.Any(j => j == null)) return "Journal entries must be a non-null array without null entries.";
        if (payment.SupplierId.HasValue && !await db.Suppliers.AnyAsync(s => s.Id == payment.SupplierId && s.UserConfigId == company, ct)) return "Invalid supplier for this company.";
        if (payment.CustomerId.HasValue && !await db.Customers.AnyAsync(c => c.Id == payment.CustomerId && c.UserConfigId == company, ct)) return "Invalid customer for this company.";
        if (payment.PaymentModeId.HasValue && !await db.PaymentModes.AnyAsync(m => m.Id == payment.PaymentModeId, ct)) return "Invalid payment mode.";
        if (payment.PaidThroughAccountId.HasValue && !await db.Accounts.AnyAsync(a => a.Id == payment.PaidThroughAccountId && a.UserConfigId == company, ct)) return "Invalid paid-through account for this company.";
        var journals = payment.JournalEntries;
        if (journals.Any(j => j.Id < 0 || j.UserConfigId != company || (j.PaymentId.HasValue && j.PaymentId != 0 && j.PaymentId != id))) return "Journal entries must belong to this payment and company.";
        var ids = journals.Where(j => j.Id != 0).Select(j => j.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length) return "Duplicate journal entry IDs are not allowed.";
        if ((!id.HasValue && ids.Length > 0) || ids.Length != await db.JournalEntries.CountAsync(j => ids.Contains(j.Id) && j.PaymentId == id && j.UserConfigId == company, ct)) return "One or more entries do not belong to this payment.";
        var accounts = journals.Where(j => j.Deleted != true).Select(j => j.AccountId).Distinct().ToArray();
        if (accounts.Length != await db.Accounts.CountAsync(a => accounts.Contains(a.Id) && a.UserConfigId == company, ct)) return "Invalid account for this company.";
        var suppliers = journals.Where(j => j.Deleted != true && j.SupplierId.HasValue).Select(j => j.SupplierId.Value).Distinct().ToArray();
        if (suppliers.Length != await db.Suppliers.CountAsync(s => suppliers.Contains(s.Id) && s.UserConfigId == company, ct)) return "Invalid journal supplier for this company.";
        var customers = journals.Where(j => j.Deleted != true && j.CustomerId.HasValue).Select(j => j.CustomerId.Value).Distinct().ToArray();
        if (customers.Length != await db.Customers.CountAsync(c => customers.Contains(c.Id) && c.UserConfigId == company, ct)) return "Invalid journal customer for this company.";
        var taxes = journals.Where(j => j.Deleted != true && j.TaxRateId.HasValue).Select(j => j.TaxRateId.Value).Distinct().ToArray();
        if (taxes.Length != await db.TaxRates.CountAsync(t => taxes.Contains(t.Id) && t.UserConfigId == company, ct)) return "Invalid tax rate for this company.";
        return await ValidateTargetsAsync(company, journals, billFlow, ct);
    }

    public async Task<string> ValidateTargetsAsync(int company, IEnumerable<JournalEntry> journals, bool billFlow, CancellationToken ct)
    {
        var targets = journals.Where(j => j.PaymentToJournalEntryId.HasValue).Select(j => j.PaymentToJournalEntryId.Value).Distinct().ToArray();
        if (targets.Length != await db.JournalEntries.CountAsync(j => targets.Contains(j.Id) && j.UserConfigId == company, ct)) return "Payment target journal not found for this company.";
        if (billFlow)
        {
            var ap = await db.Configs.Where(c => c.Id == company).Select(c => c.APTradeAccountId).SingleOrDefaultAsync(ct);
            var billTargets = journals.Where(j => j.AccountId == ap && j.PaymentToJournalEntryId.HasValue).Select(j => j.PaymentToJournalEntryId.Value).Distinct().ToArray();
            if (billTargets.Length != await db.JournalEntries.CountAsync(j => billTargets.Contains(j.Id) && j.UserConfigId == company && db.Bills.Any(b => b.Id == j.BillId && b.UserConfigId == company), ct)) return "Bill target not found for this company.";
        }
        return null;
    }
}
