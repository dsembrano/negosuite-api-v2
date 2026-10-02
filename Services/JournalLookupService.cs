using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class JournalLookupService
{
    private readonly negosuiteContext db;
    public JournalLookupService(negosuiteContext db) => this.db = db;

    public static bool IsValidSort(string kind, string field, string direction) =>
        (direction is null or "asc" or "desc") && (field is null or "dueDate" or "amount" or "balance" ||
            (kind == "invoice" && field is "invoiceNo" or "invoiceDate") ||
            (kind == "bill" && field is "billNo" or "billDate") ||
            (kind == "credit" && field is "referenceNo" or "referenceDate" or "customerName" or "source" or "sourceName"));

    private IQueryable<JournalEntry> Query(int company, JournalLookupCriteria filter) => db.JournalEntries.AsNoTracking()
        .Where(j => j.UserConfigId == company)
        .Where(j => !filter.PeriodStart.HasValue || j.JournalDate >= filter.PeriodStart)
        .Where(j => !filter.PeriodEnd.HasValue || j.JournalDate <= filter.PeriodEnd);

    public async Task<object> ListAsync(string kind, int company, JournalLookupCriteria filter, int? page, int? size, string search, string field, string direction, CancellationToken ct)
    {
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var query = Query(company, filter).Where(j => j.Balance > 0);
        if (kind == "invoice")
        {
            var rows = query.Where(j => j.Nature == "D" && j.AccountId == filter.AccountId && j.CustomerId == filter.CustomerId)
                .Select(j => new UnpaidInvoiceDto { JournalEntryId = j.Id, InvoiceNo = j.ReferenceNo, InvoiceDate = j.JournalDate, DueDate = j.DueDate, Amount = j.Amount, Balance = j.Balance });
            if (term != null) rows = rows.Where(j => j.InvoiceNo.Contains(term));
            return await Page(rows, page, size, field ?? "invoiceDate", direction, "InvoiceNo", "JournalEntryId", ct);
        }
        if (kind == "bill")
        {
            var rows = query.Where(j => j.Nature == "C" && j.AccountId == filter.AccountId && j.SupplierId == filter.SupplierId)
                .Select(j => new UnpaidBillDto { JournalEntryId = j.Id, BillNo = j.ReferenceNo, BillDate = j.JournalDate, DueDate = j.DueDate, Amount = j.Amount, Balance = j.Balance });
            if (term != null) rows = rows.Where(j => j.BillNo.Contains(term));
            return await Page(rows, page, size, field ?? "billDate", direction, "BillNo", "JournalEntryId", ct);
        }
        var account = await db.Configs.Where(c => c.Id == company).Select(c => c.ARTradeAccountId).SingleOrDefaultAsync(ct);
        var credits = query.Where(j => j.Nature == "C" && j.AccountId == account && (!filter.CustomerId.HasValue || j.CustomerId == filter.CustomerId))
            .Select(j => new UnappliedCreditDto
            {
                Id = j.Id,
                ReferenceNo = j.ReferenceNo,
                ReferenceDate = j.JournalDate,
                CustomerId = j.CustomerId,
                CustomerName = j.Customer.Name,
                DueDate = j.DueDate,
                Amount = j.Amount,
                Balance = j.Balance,
                Source = j.Source,
                SourceName = j.Source == "GJ" ? "General Journal" : j.Source == "SI" ? "Sales Invoice" : j.Source == "SR" ? "Cash Invoice" :
                    j.Source == "PR" ? "Payment Received" : j.Source == "PU" ? "Purchase" : j.Source == "BP" ? "Bill Payment" :
                    j.Source == "OP" ? "Others Payment" : j.Source == "PV" ? "Payment Voucher" : j.Source == "IA" ? "Inventory Adjustment" :
                    j.Source == "II" ? "Stock Issuance" : j.Source == "RR" ? "Receiving Report" : ""
            });
        if (term != null) credits = credits.Where(j => j.ReferenceNo.Contains(term) || (j.CustomerName != null && j.CustomerName.Contains(term)) || j.Source.Contains(term) || j.SourceName.Contains(term));
        return await Page(credits, page, size, field ?? "referenceDate", direction, "ReferenceNo", "Id", ct);
    }

    // Column names originate only from the public allowlist and fixed defaults above.
    private static IOrderedQueryable<T> Order<T>(IQueryable<T> query, string property, string method)
    {
        var item = Expression.Parameter(typeof(T), "row");
        var key = Expression.Property(item, property);
        var expression = Expression.Call(typeof(Queryable), method, new[] { typeof(T), key.Type }, query.Expression, Expression.Quote(Expression.Lambda(key, item)));
        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(expression);
    }

    private static async Task<object> Page<T>(IQueryable<T> query, int? page, int? size, string field, string direction, string number, string id, CancellationToken ct)
    {
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        var property = char.ToUpperInvariant(field[0]) + field.Substring(1);
        query = Order(query, property, direction == "desc" ? "OrderByDescending" : "OrderBy");
        if (property.EndsWith("Date", StringComparison.Ordinal) && property != "DueDate") query = Order(query, number, "ThenBy");
        query = Order(query, id, "ThenBy");
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var rows = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<T>(rows, page.Value, size.Value, count) : rows;
    }

    public async Task<UnappliedCreditDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var entry = await db.JournalEntries.AsNoTracking().Include(j => j.Customer).SingleOrDefaultAsync(j => j.Id == id && j.UserConfigId == company, ct);
        if (entry == null) return null;
        return new()
        {
            Id = entry.Id,
            ReferenceNo = entry.ReferenceNo,
            ReferenceDate = entry.JournalDate,
            CustomerId = entry.CustomerId,
            Customer = new TransactionResponseMapping().Map(entry.Customer),
            CustomerName = entry.Customer?.Name,
            DueDate = entry.DueDate,
            Amount = entry.Amount,
            Balance = entry.Balance,
            Source = entry.Source,
            SourceName = Controllers.FinancialReportsController.GetJournalSourceName(entry.Source)
        };
    }
}
