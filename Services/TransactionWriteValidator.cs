using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class TransactionWriteValidator
{
    private readonly negosuiteContext db;
    public TransactionWriteValidator(negosuiteContext db) => this.db = db;

    private async Task<bool> ReferencesAsync<T>(int company, IEnumerable<int?> references, CancellationToken ct) where T : class
    {
        var ids = references.Where(i => i.HasValue).Select(i => i.Value).Distinct().ToArray();
        return ids.Length == await db.Set<T>().CountAsync(e => ids.Contains(EF.Property<int>(e, "Id")) && EF.Property<int>(e, "UserConfigId") == company, ct);
    }

    public async Task<string> HeaderAsync(int company, int? supplier, int? customer, int?[] locations, int?[] accounts, CancellationToken ct)
    {
        if (!await ReferencesAsync<Supplier>(company, new[] { supplier }, ct)) return "Invalid supplier for this company.";
        if (!await ReferencesAsync<Customer>(company, new[] { customer }, ct)) return "Invalid customer for this company.";
        if (!await ReferencesAsync<InventoryLocation>(company, locations, ct)) return "Invalid inventory location for this company.";
        if (!await ReferencesAsync<Account>(company, accounts, ct)) return "Invalid account for this company.";
        return null;
    }

    public async Task<string> LinesAsync<T>(int company, int? parentId, string parentProperty,
        (int Id, int ParentId, int ItemId, int? TaxRateId, int? LocationId)[] lines, CancellationToken ct) where T : class
    {
        if (lines.Any(l => l.Id < 0 || (l.ParentId != 0 && l.ParentId != parentId))) return "Lines must belong to this transaction.";
        var ids = lines.Where(l => l.Id != 0).Select(l => l.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length) return "Duplicate line IDs are not allowed.";
        if ((!parentId.HasValue && ids.Length > 0) || ids.Length != await db.Set<T>().CountAsync(l => ids.Contains(EF.Property<int>(l, "Id")) && EF.Property<int>(l, parentProperty) == parentId, ct)) return "One or more lines do not belong to this transaction.";
        if (!await ReferencesAsync<Item>(company, lines.Select(l => (int?)l.ItemId), ct)) return "Invalid item for this company.";
        if (!await ReferencesAsync<TaxRate>(company, lines.Select(l => l.TaxRateId), ct)) return "Invalid tax rate for this company.";
        if (!await ReferencesAsync<InventoryLocation>(company, lines.Select(l => l.LocationId), ct)) return "Invalid line inventory location for this company.";
        return null;
    }

    public async Task<string> JournalsAsync(int company, int? parentId, string parentProperty, ICollection<JournalEntry> journals,
        Func<JournalEntry, int?> parent, CancellationToken ct)
    {
        if (journals == null || journals.Any(j => j == null)) return "Journal entries must be a non-null array without null entries.";
        if (journals.Any(j => j.Id < 0 || j.UserConfigId != company || (parent(j).HasValue && parent(j) != 0 && parent(j) != parentId))) return "Journal entries must belong to this transaction and company.";
        var ids = journals.Where(j => j.Id != 0).Select(j => j.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length) return "Duplicate journal IDs are not allowed.";
        if ((!parentId.HasValue && ids.Length > 0) || ids.Length != await db.JournalEntries.CountAsync(j => ids.Contains(j.Id) && j.UserConfigId == company && EF.Property<int?>(j, parentProperty) == parentId, ct)) return "One or more journals do not belong to this transaction.";
        var active = journals.Where(j => j.Deleted != true).ToArray();
        if (!await ReferencesAsync<Account>(company, active.Select(j => (int?)j.AccountId), ct)) return "Invalid journal account for this company.";
        if (!await ReferencesAsync<Customer>(company, active.Select(j => j.CustomerId), ct)) return "Invalid journal customer for this company.";
        if (!await ReferencesAsync<Supplier>(company, active.Select(j => j.SupplierId), ct)) return "Invalid journal supplier for this company.";
        if (!await ReferencesAsync<TaxRate>(company, active.Select(j => j.TaxRateId), ct)) return "Invalid journal tax rate for this company.";
        return await TargetsAsync(company, journals, ct);
    }

    public async Task<string> TargetsAsync(int company, IEnumerable<JournalEntry> journals, CancellationToken ct)
    {
        var ids = journals.Where(j => j.PaymentToJournalEntryId.HasValue).Select(j => j.PaymentToJournalEntryId.Value).Distinct().ToArray();
        var targets = await db.JournalEntries.AsNoTracking().Where(j => ids.Contains(j.Id) && j.UserConfigId == company)
            .Select(j => new { j.Id, j.Source, j.SalesInvoiceId, j.BillId }).ToListAsync(ct);
        if (targets.Count != ids.Length) return "Payment target not found for this company.";
        var invoices = targets.Where(j => j.Source == "SI").Select(j => j.SalesInvoiceId).Distinct().ToArray();
        if (invoices.Length != await db.SalesInvoices.CountAsync(i => invoices.Contains(i.Id) && i.UserConfigId == company, ct)) return "Linked invoice not found for this company.";
        var bills = targets.Where(j => j.Source == "PU").Select(j => j.BillId).Distinct().ToArray();
        if (bills.Length != await db.Bills.CountAsync(b => bills.Contains(b.Id) && b.UserConfigId == company, ct)) return "Linked bill not found for this company.";
        return null;
    }
}
