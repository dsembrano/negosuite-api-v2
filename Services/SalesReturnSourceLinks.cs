using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

// Caller owns the company lock and transaction. Never detach active returns.
public sealed class SalesReturnSourceLinks(negosuiteContext db)
{
    public async Task ReleaseDeletedAsync(int company, int sourceId, bool charge, int[] removedLines, CancellationToken ct)
    {
        var wholeInvoice = removedLines == null;
        var label = charge ? "Charge Invoice" : "Cash Invoice";
        var query = db.SalesReturns.Where(r => r.UserConfigId == company &&
            (charge ? r.SalesInvoiceId == sourceId : r.SalesReceiptId == sourceId));
        if (!wholeInvoice)
            query = query.Where(r => r.SalesReturnDetails.Any(d => charge
                ? d.SalesInvoiceDetailId.HasValue && removedLines.Contains(d.SalesInvoiceDetailId.Value)
                : d.SalesReceiptDetailId.HasValue && removedLines.Contains(d.SalesReceiptDetailId.Value)));
        var returns = await query.Include(r => r.SalesReturnDetails).Include(r => r.JournalEntries).ToListAsync(ct);
        Require(returns.All(r => r.Status == -1), $"Cannot {(wholeInvoice ? "delete" : "remove a line from")} this {label} because it is referenced by a draft or posted Sales Return. Delete the related return first.", 409);
        foreach (var row in returns)
        {
            Require(row.JournalEntries.All(j => j.Status == -1), "The deleted Sales Return still has active journals. Its reversal must be reviewed before deleting the invoice.", 409);
            ReturnSource snapshot = null;
            try { snapshot = JsonConvert.DeserializeObject<ReturnSource>(row.SourceSnapshotJson ?? "null"); }
            catch (JsonException) { }
            Require(snapshot != null && snapshot.Source == (charge ? "SI" : "SR") && snapshot.Id == sourceId &&
                !string.IsNullOrWhiteSpace(snapshot.ReferenceNo), "The deleted Sales Return has incomplete invoice history. Restore its snapshot before deleting the invoice.", 409);
            foreach (var line in row.SalesReturnDetails)
            {
                var originalId = line.SalesInvoiceDetailId ?? line.SalesReceiptDetailId;
                if (!originalId.HasValue || (!wholeInvoice && !removedLines.Contains(originalId.Value))) continue;
                Require(snapshot.Lines != null && snapshot.Lines.Any(l => l.Id == originalId && l.ItemId == line.ItemId), "The deleted Sales Return has incomplete line history. Restore its snapshot before deleting the invoice line.", 409);
                line.OriginalSourceDetailId = originalId;
                line.SalesInvoiceDetailId = null;
                line.SalesReceiptDetailId = null;
            }
            if (wholeInvoice)
            {
                // Preserve reversed application history before source journals can be removed.
                if (row.ApplicationsJson == null)
                {
                    var targetIds = row.JournalEntries.Where(j => j.PaymentToJournalEntryId.HasValue).Select(j => j.PaymentToJournalEntryId.Value).ToArray();
                    var names = await db.JournalEntries.AsNoTracking().Where(j => j.UserConfigId == company && targetIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, j => j.ReferenceNo, ct);
                    row.ApplicationsJson = JsonConvert.SerializeObject(row.JournalEntries.Where(j => j.PaymentToJournalEntryId.HasValue)
                        .Select(j => new ReceivableApplication { JournalEntryId = j.PaymentToJournalEntryId.Value, Amount = j.Amount, InvoiceNo = names.GetValueOrDefault(j.PaymentToJournalEntryId.Value) }));
                }
                row.SalesInvoiceId = null;
                row.SalesReceiptId = null;
            }
        }
        if (returns.Count > 0) await db.SaveChangesAsync(ct);
    }
}
