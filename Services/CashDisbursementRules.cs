using System;
using System.Collections.Generic;
using System.Linq;
using negosuite_api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace negosuite_api.Services;

// V2's constrained direct-payment contract. Legacy payments routes retain their contract.
public static class CashDisbursementRules
{
    public static bool Can(UserRole role, string module, string action)
    {
        if (role?.IsAdmin == true) return true;
        try { return JArray.Parse(role?.Permission ?? "[]").OfType<JObject>().Any(p => (string)p["moduleId"] == module && p[action]?.Type == JTokenType.Boolean && (bool)p[action]); }
        catch (JsonException) { return false; }
    }

    public static string Existing(Payment payment, int? apAccount, ISet<int> inventoryAccounts)
    {
        if (payment.IsBillPayment) return "Use Supplier Payments for accounts payable.";
        if (payment.Status == -1) return "Deleted payments cannot be changed.";
        if (payment.JournalEntries.Any(j => j.AccountId == apAccount || inventoryAccounts.Contains(j.AccountId))) return "Use the original AP or inventory workflow for this payment.";
        if (payment.JournalEntries.Any(j => j.PaymentToJournalEntryId.HasValue || j.Amount != j.Balance || j.DebtorId.HasValue || j.CreditorId.HasValue)) return "This payment has linked balances or unsupported subledger assignments.";
        return null;
    }

    public static string Validate(Payment payment, Config config, IReadOnlyDictionary<int, Account> accounts,
        ISet<int> inventoryAccounts, IReadOnlyDictionary<int, TaxRate> taxes,
        IReadOnlyCollection<ResponsibilityCenterType> types, IReadOnlyCollection<ResponsibilityCenter> centers, UserRole role, bool advanced)
    {
        if (payment.IsBillPayment || payment.Status is not (0 or 1) || payment.Balance != null) return "Invalid cash disbursement state.";
        if (string.IsNullOrWhiteSpace(payment.ReferenceNo) || string.IsNullOrWhiteSpace(payment.Payee) || payment.ReferenceDate == default || !payment.PaymentModeId.HasValue || !payment.PaidThroughAccountId.HasValue) return "Complete the required payment fields.";
        if (payment.SupplierId.HasValue && payment.CustomerId.HasValue) return "Select one payee type.";
        if (payment.PaymentModeId == 2 && string.IsNullOrWhiteSpace(payment.CheckNo)) return "Enter the check number.";
        if (!payment.Amount.HasValue || payment.Amount <= 0 || decimal.Round(payment.Amount.Value, 4) != payment.Amount) return "Enter a positive payment amount with at most four decimal places.";
        if (payment.JournalEntries == null || payment.JournalEntries.Any(j => j == null)) return "Invalid journal entries.";
        var journals = payment.JournalEntries.Where(j => j.Deleted != true).ToArray();
        if (journals.Any(j => j.Source != "PV" || j.Status != payment.Status || j.Amount != j.Balance || decimal.Round(j.Amount, 4) != j.Amount || j.Nature is not ("D" or "C") || j.PaymentToJournalEntryId.HasValue || j.DebtorId.HasValue || j.CreditorId.HasValue)) return "Invalid cash disbursement journal.";
        foreach (var j in journals)
        {
            if (!accounts.TryGetValue(j.AccountId, out var account)) return "Invalid account for this company.";
            if (j.AccountId == config.APTradeAccountId) return "Use Supplier Payments to settle accounts payable.";
            if (inventoryAccounts.Contains(j.AccountId)) return "Use Purchase Invoices for tracked inventory.";
            if (account.RequireSupplier && !j.SupplierId.HasValue) return $"Select a supplier for {account.Name}.";
            if ((account.RequireCustomer || account.Id == config.ARTradeAccountId) && !j.CustomerId.HasValue) return $"Select a customer for {account.Name}.";
            var error = ValidateCenters(j.ResponsibilityCenterEntry, account, types, centers, role);
            if (error != null) return error;
        }
        var bases = journals.Where(j => j.IsComputed != true).ToArray();
        if (bases.Length == 0 || bases.Any(j => j.Amount <= 0)) return "Add positive account allocations.";
        if (!advanced && bases.Any(j => j.Nature == "C")) return "General Journal write permission is required for credit allocations.";
        // Match every computed entry to either the paid-through credit or a V1 tax entry.
        var computed = journals.Where(j => j.IsComputed == true).ToList();
        var paid = computed.FirstOrDefault(j => j.AccountId == payment.PaidThroughAccountId && j.Nature == "C" && j.Amount == payment.Amount && j.TaxRateId == null && j.SupplierId == payment.SupplierId && j.CustomerId == payment.CustomerId && SameCenters(j.ResponsibilityCenterEntry, payment.ResponsibilityCenterEntry));
        if (paid == null) return "The paid-through credit must equal the amount paid.";
        computed.Remove(paid);
        foreach (var entry in bases.Where(j => j.TaxRateId.HasValue))
        {
            if (!taxes.TryGetValue(entry.TaxRateId.Value, out var tax) || !tax.TaxAccountId.HasValue) return "Select a configured tax rate.";
            // Same tax-exclusive formula and four-decimal serialization as V1.
            var amount = decimal.Round(entry.Amount * tax.Rate / 100m, 4, MidpointRounding.AwayFromZero);
            var taxEntry = computed.FirstOrDefault(j => j.AccountId == tax.TaxAccountId && j.Nature == entry.Nature && j.Amount == amount && j.TaxRateId == null && j.SupplierId == entry.SupplierId && j.CustomerId == entry.CustomerId && SameCenters(j.ResponsibilityCenterEntry, entry.ResponsibilityCenterEntry));
            if (taxEntry == null) return "Tax entries do not match the selected tax rates. Reload and review the allocations.";
            computed.Remove(taxEntry);
        }
        if (computed.Count != 0) return "Unexpected computed journal entries.";
        if (journals.Sum(j => j.Nature == "D" ? j.Amount : -j.Amount) != 0) return "Journal debits and credits must balance.";
        return null;
    }

    private static bool SameCenters(string left, string right)
    {
        try { return JToken.DeepEquals(JToken.Parse(left ?? "[]"), JToken.Parse(right ?? "[]")); }
        catch (JsonException) { return false; }
    }

    public static string ValidateCenters(string json, Account account, IReadOnlyCollection<ResponsibilityCenterType> types, IReadOnlyCollection<ResponsibilityCenter> centers, UserRole role)
    {
        try
        {
            var selected = JArray.Parse(json ?? "[]");
            var permissions = role?.IsAdmin == true ? new JArray() : JArray.Parse(role?.AdvancePermission ?? "[]");
            var seen = new HashSet<int>();
            foreach (var item in selected)
            {
                if (item["id"]?.Type != JTokenType.Integer || item["typeId"]?.Type != JTokenType.Integer) return "Invalid responsibility centers.";
                var id = (int)item["id"]; var type = (int)item["typeId"];
                if (!seen.Add(type) || !types.Any(t => t.Id == type) || !centers.Any(c => c.Id == id && c.ResponsibilityCenterTypeId == type)) return "Invalid responsibility centers for this company.";
                var permission = permissions.FirstOrDefault(p => (int?)p["responsibilityCenterTypeId"] == type);
                if (permission != null && !(permission["responsibilityCenterIds"] is JArray allowed && allowed.Values<int>().Contains(id))) return "Responsibility center access denied.";
            }
            if (account == null) return null; // Header/draft scope check; required account fields apply on posting.
            foreach (var type in types)
            {
                var tags = JArray.Parse(string.IsNullOrEmpty(type.RequiredByTags) ? "[]" : type.RequiredByTags).Values<int>().ToArray();
                var kind = account.Category?.Type;
                var required = tags.Contains(account.Id) || (!string.IsNullOrEmpty(kind) && (type.RequiredBy == "ALL" || type.RequiredBy == "REVENUE" && kind == "Income" || type.RequiredBy == "COST" && kind == "Expense" || type.RequiredBy == "PROFIT" && (kind == "Income" || kind == "Expense") || type.RequiredBy == "ACCTCAT" && tags.Contains(account.CategoryId)));
                if (required && !seen.Contains(type.Id)) return $"Select {type.Name} for {account.Name}.";
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidCastException or ArgumentException or InvalidOperationException or OverflowException) { return "Invalid responsibility center configuration or assignment."; }
        return null;
    }
}
