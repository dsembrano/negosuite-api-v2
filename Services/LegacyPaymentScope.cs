using System.Linq;
using negosuite_api.Models;

namespace negosuite_api.Services;

public static class LegacyPaymentScope
{
    // ExpensePayment has no company column. Every available ownership reference must agree.
    public static IQueryable<ExpensePayment> Expenses(negosuiteContext db, int company) => db.ExpensePayments
        .Where(e => e.JournalEntries.Any() && e.JournalEntries.All(j => j.UserConfigId == company))
        .Where(e => !e.SupplierId.HasValue || db.Suppliers.Any(s => s.Id == e.SupplierId && s.UserConfigId == company))
        .Where(e => !e.CustomerId.HasValue || db.Customers.Any(c => c.Id == e.CustomerId && c.UserConfigId == company))
        .Where(e => !e.PaidThroughAccountId.HasValue || db.Accounts.Any(a => a.Id == e.PaidThroughAccountId && a.UserConfigId == company));
}
