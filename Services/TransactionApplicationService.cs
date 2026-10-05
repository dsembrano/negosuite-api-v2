using System;
using System.Collections.Generic;
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

// Reconciles persisted old effects with the final saved document, regardless of URL or deleted flags.
// The caller must hold the company lock until commit. No client-supplied old amount is trusted.
public sealed class TransactionApplicationService(negosuiteContext db)
{
    public static DateTime Revision(DateTime? previous)
    {
        var now = new DateTime(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond);
        return previous >= now ? previous.Value.AddMilliseconds(1) : now;
    }

    public async Task ReconcileAsync(int company, object before, object after, JournalEntry[] oldEntries, JournalEntry[] newEntries, UserRole role, CancellationToken ct)
    {
        Require(db.Database.CurrentTransaction != null, "Transaction application requires a transaction.");
        var oldPosted = (short?)Get(before, "Status") == 1;
        var newPosted = (short?)Get(after, "Status") == 1;
        var oldLinks = oldEntries.Where(j => oldPosted && j.Status == 1 && j.PaymentToJournalEntryId.HasValue).ToArray();
        var newLinks = newEntries.Where(j => newPosted && j.Status == 1 && j.PaymentToJournalEntryId.HasValue).ToArray();
        var config = newLinks.Length == 0 ? null : await db.Configs.AsNoTracking().SingleAsync(c => c.Id == company, ct);
        if (after is GeneralJournal or SalesInvoice or Bill)
            foreach (var entry in newEntries.Where(j => !j.PaymentToJournalEntryId.HasValue && !oldEntries.Any(old => old.Id == j.Id)))
            {
                // A newly created receivable/payable cannot already have external applications.
                var tracked = await db.JournalEntries.FindAsync(new object[] { entry.Id }, ct);
                tracked.Balance = entry.Amount;
            }
        var ids = oldLinks.Concat(newLinks).Select(j => j.PaymentToJournalEntryId.Value).Distinct().OrderBy(x => x).ToArray();
        foreach (var id in ids)
        {
            var target = (await db.JournalEntries.FromSqlInterpolated($"SELECT * FROM journalentry WHERE Id={id} AND UserConfigId={company} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
            Require(target != null, "Application target not found for this company.", 409);
            PostedJournalValidator.DemandScope(role, target.ResponsibilityCenterEntry);
            Require(!oldEntries.Any(j => j.Id == id) && !newEntries.Any(j => j.Id == id), "A transaction cannot apply to itself.");
            var incoming = newLinks.Where(j => j.PaymentToJournalEntryId == id).ToArray();
            SalesInvoice invoice = null;
            Bill bill = null;
            if (target.SalesInvoiceId.HasValue)
                invoice = await db.SalesInvoices.SingleOrDefaultAsync(i => i.Id == target.SalesInvoiceId && i.UserConfigId == company, ct);
            if (target.BillId.HasValue)
                bill = await db.Bills.SingleOrDefaultAsync(b => b.Id == target.BillId && b.UserConfigId == company, ct);
            PostedJournalValidator.DemandScope(role, invoice?.ResponsibilityCenterEntry);
            PostedJournalValidator.DemandScope(role, bill?.ResponsibilityCenterEntry);
            foreach (var link in incoming)
            {
                Require(target.Status == 1 && link.AccountId == target.AccountId && link.Nature != target.Nature, "Select a posted application target with the same account and opposite direction.");
                var customer = link.CustomerId ?? (int?)Get(after, "CustomerId");
                var supplier = link.SupplierId ?? (int?)Get(after, "SupplierId");
                Require(Get(after, "CustomerId") is not int headerCustomer || customer == headerCustomer, "Journal customer must match the payment customer.");
                Require(Get(after, "SupplierId") is not int headerSupplier || supplier == headerSupplier, "Journal supplier must match the payment supplier.");
                var targetCustomer = target.CustomerId ?? invoice?.CustomerId;
                var targetSupplier = target.SupplierId ?? bill?.SupplierId;
                var historicalLink = oldLinks.Any(j => j.PaymentToJournalEntryId == id && j.AccountId == target.AccountId);
                Require(historicalLink || invoice != null && target.AccountId == config.ARTradeAccountId || bill != null && target.AccountId == config.APTradeAccountId ||
                    invoice == null && bill == null && (targetCustomer.HasValue && target.AccountId == config.ARTradeAccountId || targetSupplier.HasValue && target.AccountId == config.APTradeAccountId),
                    "Select a receivable or payable trade-account journal. Existing applications can still be reversed after configuration changes.");
                Require(targetCustomer.HasValue && customer == targetCustomer || targetSupplier.HasValue && supplier == targetSupplier, "The application target must belong to the same customer or supplier.");
                if (after is SalesInvoicePayment) Require(target.Nature == "D" && targetCustomer == customer, "Customer payments must apply to receivables.");
                if (after is Payment payment && payment.IsBillPayment || after is BillPayment)
                    Require(target.Nature == "C" && targetSupplier == supplier, "Supplier payments must apply to payables.");
                Require(!target.SalesInvoiceId.HasValue || invoice != null && invoice.Status == 1 && target.Nature == "D", "The invoice is no longer posted or available.");
                Require(!target.BillId.HasValue || bill != null && bill.Status == 1 && target.Nature == "C", "The bill is no longer posted or available.");
                Require(link.Amount > 0, "Applied amounts must be positive.");
            }
            var delta = incoming.Sum(j => j.Amount) - oldLinks.Where(j => j.PaymentToJournalEntryId == id).Sum(j => j.Amount);
            var balance = target.Balance - delta;
            Require(balance >= 0 && balance <= target.Amount, "Application exceeds the available balance. Reload and review the allocation.", 409);
            if (delta == 0) continue;
            target.Balance = balance;
            target.LastUpdatedDate = Revision(target.LastUpdatedDate);
            if (invoice != null)
            {
                Require(invoice.Balance - delta >= 0 && invoice.Balance - delta <= invoice.Amount, "Invoice balance is inconsistent. Review its applications.", 409);
                invoice.Balance -= delta; invoice.LastUpdatedDate = Revision(invoice.LastUpdatedDate);
            }
            if (bill != null)
            {
                Require(bill.Balance - delta >= 0 && bill.Balance - delta <= bill.Amount, "Bill balance is inconsistent. Review its applications.", 409);
                bill.Balance -= delta; bill.LastUpdatedDate = Revision(bill.LastUpdatedDate);
            }
        }
        // Preserve consumption on journals which themselves receive applications.
        var entryIds = oldEntries.Select(j => j.Id).ToArray();
        var receiving = await db.JournalEntries.AsNoTracking().Where(j => j.Status == 1 && j.PaymentToJournalEntryId.HasValue && entryIds.Contains(j.PaymentToJournalEntryId.Value)).Select(j => j.PaymentToJournalEntryId.Value).Distinct().ToListAsync(ct);
        foreach (var old in oldEntries.Where(j => j.Status == 1 && !j.PaymentToJournalEntryId.HasValue && j.Balance < j.Amount && (receiving.Contains(j.Id) || j.SalesInvoiceId.HasValue || j.BillId.HasValue || j.GeneralJournalId.HasValue)))
        {
            var current = newEntries.SingleOrDefault(j => j.Id == old.Id);
            Require(newPosted && current != null && current.AccountId == old.AccountId && current.Nature == old.Nature && current.CustomerId == old.CustomerId && current.SupplierId == old.SupplierId,
                "Reverse linked applications before removing or changing this journal.", 409);
            var consumed = old.Amount - old.Balance;
            Require(current.Amount >= consumed, "The revised amount is less than the amount already applied.", 409);
            var tracked = await db.JournalEntries.FindAsync(new object[] { current.Id }, ct);
            tracked.Balance = current.Amount - consumed;
        }
        // Invoice/bill balance is derived from the persisted consumption, never a stale browser value.
        if (after is SalesInvoice or Bill)
        {
            var amount = (decimal?)Get(after, "Amount") ?? 0;
            var consumed = before == null ? 0 : ((decimal?)Get(before, "Amount") ?? 0) - ((decimal?)Get(before, "Balance") ?? 0);
            Require(amount >= consumed, "The revised amount is less than the amount already applied.", 409);
            if (consumed != 0)
                Require(Get(before, "CustomerId")?.Equals(Get(after, "CustomerId")) != false && Get(before, "SupplierId")?.Equals(Get(after, "SupplierId")) != false, "Cannot change the party while applications exist.", 409);
            var tracked = await db.FindAsync(after.GetType(), new object[] { (int)Get(after, "Id") }, ct);
            Set(tracked, "Balance", amount - consumed);
        }
        if (after is SalesInvoicePayment or BillPayment || after is Payment p && (p.IsBillPayment || newLinks.Length > 0 || p.Balance.HasValue))
        {
            var amount = (decimal?)Get(after, "Amount") ?? 0;
            // V1 separates cash paid from AR/AP settled by discount/write-off adjustments.
            var adjustmentRows = newEntries.Where(j => !j.PaymentToJournalEntryId.HasValue).GroupBy(j => (j.AccountId, j.Nature)).ToDictionary(g => g.Key, g => g.Sum(j => j.Amount));
            decimal cashApplied = 0;
            foreach (var link in newLinks)
            {
                decimal cash = link.Amount;
                if (!string.IsNullOrWhiteSpace(link.PaymentAdjustmentEntry))
                {
                    JObject adjustment;
                    try { adjustment = JObject.Parse(link.PaymentAdjustmentEntry); }
                    catch (JsonException) { throw new AdministrationException("Invalid payment adjustment."); }
                    Require(adjustment["amount"]?.Type is JTokenType.Integer or JTokenType.Float && adjustment["accountId"]?.Type == JTokenType.Integer, "Invalid payment adjustment amount or account.");
                    var value = (decimal)adjustment["amount"]; var account = (int)adjustment["accountId"]; var nature = (string)adjustment["nature"];
                    Require(value > 0 && decimal.Round(value, 4) == value && nature is "D" or "C", "Invalid payment adjustment.");
                    var key = (account, nature);
                    Require(adjustmentRows.GetValueOrDefault(key) >= value, "Payment adjustment has no matching journal entry.");
                    adjustmentRows[key] -= value;
                    cash += nature == link.Nature ? value : -value;
                }
                Require(cash >= 0, "Payment adjustment exceeds its allocation.");
                cashApplied += cash;
            }
            var balance = amount - cashApplied;
            Require(balance >= 0, "Applied amounts exceed the payment total.");
            var tracked = await db.FindAsync(after.GetType(), new object[] { (int)Get(after, "Id") }, ct);
            Set(tracked, "Balance", balance);
        }
        if (after != null)
        {
            var tracked = await db.FindAsync(after.GetType(), new object[] { (int)Get(after, "Id") }, ct);
            Set(tracked, "LastUpdatedDate", Revision((DateTime?)Get(before, "LastUpdatedDate")));
        }
    }
}
