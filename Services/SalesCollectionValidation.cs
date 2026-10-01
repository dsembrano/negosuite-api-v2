using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

namespace negosuite_api.Services;

internal sealed class SalesCollectionValidation
{
    private readonly negosuiteContext db;
    public SalesCollectionValidation(negosuiteContext db) => this.db = db;

    public async Task<string> ReferencesAsync(int company, int customer, int? mode, int? deposit, ICollection<JournalEntry> journals, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customer && c.UserConfigId == company, ct)) return "Invalid customer for this company.";
        if (mode.HasValue && !await db.PaymentModes.AnyAsync(m => m.Id == mode, ct)) return "Invalid payment mode.";
        if (deposit.HasValue && !await db.Accounts.AnyAsync(a => a.Id == deposit && a.UserConfigId == company, ct)) return "Invalid deposit account for this company.";
        if (journals.Any(j => j.UserConfigId != company)) return "Journal entries must belong to this company.";
        var accountIds = journals.Where(j => j.Deleted != true).Select(j => j.AccountId).Distinct().ToArray();
        if (accountIds.Length != await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.UserConfigId == company, ct)) return "Invalid account for this company.";
        var customers = journals.Where(j => j.Deleted != true && j.CustomerId.HasValue).Select(j => j.CustomerId.Value).Distinct().ToArray();
        if (customers.Length != await db.Customers.CountAsync(c => customers.Contains(c.Id) && c.UserConfigId == company, ct)) return "Invalid journal customer for this company.";
        var suppliers = journals.Where(j => j.Deleted != true && j.SupplierId.HasValue).Select(j => j.SupplierId.Value).Distinct().ToArray();
        if (suppliers.Length != await db.Suppliers.CountAsync(s => suppliers.Contains(s.Id) && s.UserConfigId == company, ct)) return "Invalid journal supplier for this company.";
        return null;
    }
}
