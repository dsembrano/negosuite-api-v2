using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static negosuite_api.Services.AdministrationSupport;
using static negosuite_api.Services.TransactionDocument;

namespace negosuite_api.Services;

public sealed class PostedJournalValidator(negosuiteContext db)
{
    public static void DemandScope(UserRole role, string json)
    {
        if (role?.IsAdmin == true) return;
        try
        {
            var selected = JArray.Parse(json ?? "[]");
            foreach (var permission in JArray.Parse(role?.AdvancePermission ?? "[]"))
            {
                var type = (int)permission["responsibilityCenterTypeId"];
                var allowed = permission["responsibilityCenterIds"].Values<int>().ToArray();
                Require(selected.Where(c => (int?)c["typeId"] == type).All(c => allowed.Contains((int)c["id"])), "This transaction is outside your responsibility center access.", 403);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidCastException or ArgumentException or InvalidOperationException or OverflowException or NullReferenceException)
        { throw new AdministrationException("Invalid responsibility center access or assignment.", 403); }
    }
    public async Task ValidateAsync(int company, object document, JournalEntry[] entries, UserRole role, CancellationToken ct)
    {
        var status = (short)Get(document, "Status");
        Require(entries.All(j => j.UserConfigId == company && j.Status == status), "Journal status must match its document.");
        Require(entries.All(j => j.Nature is "D" or "C" && j.Amount >= 0 && decimal.Round(j.Amount, 4) == j.Amount), "Journal amounts must be nonnegative, with at most four decimal places and a debit/credit direction.");
        if (status == 1 && (document is GeneralJournal or SalesInvoice or SalesReceipt or Bill or Payment or SalesInvoicePayment or BillPayment or ExpensePayment))
            Require(entries.Length > 0, "Posted financial transactions must include journal entries.");
        if (status == 1) Require(entries.Sum(j => j.Nature == "D" ? j.Amount : -j.Amount) == 0, "Posted journal debits and credits must balance.");
        var ids = entries.Select(j => j.AccountId).Distinct().ToArray();
        var accounts = await db.Accounts.AsNoTracking().Include(a => a.Category).Where(a => a.UserConfigId == company && ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        Require(accounts.Count == ids.Length, "Invalid journal account for this company.");
        var types = await db.ResponsibilityCenterTypes.AsNoTracking().Where(t => t.UserConfigId == company).ToListAsync(ct);
        var centers = await db.ResponsibilityCenters.AsNoTracking().Where(c => c.UserConfigId == company).ToListAsync(ct);
        var headerError = CashDisbursementRules.ValidateCenters((string)Get(document, "ResponsibilityCenterEntry"), null, types, centers, role);
        Require(headerError == null, headerError);
        foreach (var entry in entries)
        {
            var account = accounts[entry.AccountId];
            Require(status != 1 || !account.RequireCustomer || entry.CustomerId.HasValue, $"Select a customer for {account.Name}.");
            Require(status != 1 || !account.RequireSupplier || entry.SupplierId.HasValue, $"Select a supplier for {account.Name}.");
            var error = CashDisbursementRules.ValidateCenters(entry.ResponsibilityCenterEntry, status == 1 ? account : null, types, centers, role);
            Require(error == null, error);
        }
    }
}
