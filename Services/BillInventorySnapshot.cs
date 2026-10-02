using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

namespace negosuite_api.Services;

// A request sees the same pre-save inventory state as the legacy per-line queries, without repeating reads.
public sealed class BillInventorySnapshot
{
    public Dictionary<int, Item> Items { get; private set; }
    public Dictionary<int, BillDetail> Originals { get; private set; }
    public Dictionary<int, InventorySummary> Summaries { get; private set; }

    public sealed class InventorySummary
    {
        public int ItemId { get; set; }
        public decimal? AverageCost { get; set; }
        public decimal Quantity { get; set; }
    }

    public static async Task<BillInventorySnapshot> LoadAsync(negosuiteContext db, int company, IEnumerable<BillDetail> details, int? billId, CancellationToken ct)
    {
        var originals = billId.HasValue ? await db.BillDetails.AsNoTracking().Where(d => d.BillId == billId && db.Bills.Any(b => b.Id == d.BillId && b.UserConfigId == company)).ToDictionaryAsync(d => d.Id, ct) : new();
        var ids = details.Select(d => d.ItemId).Concat(originals.Values.Select(d => d.ItemId)).Distinct().ToArray();
        var items = await db.Items.Where(i => ids.Contains(i.Id) && i.UserConfigId == company).ToDictionaryAsync(i => i.Id, ct);
        var summaries = await db.InventoryTransactions.AsNoTracking().Where(t => t.UserConfigId == company && ids.Contains(t.ItemId) && t.Status == 1)
            .GroupBy(t => new { t.ItemId, t.ItemName, t.ItemCost, t.AverageCost, t.ItemReorderPoint })
            .Select(g => new InventorySummary
            {
                ItemId = g.Key.ItemId,
                AverageCost = g.Key.AverageCost,
                Quantity = g.Sum(t => t.QuantityIn - t.QuantityOut)
            }).ToListAsync(ct);
        return new() { Items = items, Originals = originals, Summaries = summaries.GroupBy(s => s.ItemId).ToDictionary(g => g.Key, g => g.First()) };
    }
}
