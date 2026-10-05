using System;
using System.Collections.Generic;
using static negosuite_api.Services.AdministrationSupport;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;

namespace negosuite_api.Services;

// One locked pre-save state per item; aggregate old/new movements before updating weighted cost.
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

    public async Task ApplyAsync(negosuiteContext db, Bill bill, bool deleting, CancellationToken ct)
    {
        Require(db.Database.CurrentTransaction != null, "Purchase costing requires a transaction.");
        var oldStatus = bill.Id == 0 ? (short)0 : await db.Bills.AsNoTracking().Where(b => b.Id == bill.Id).Select(b => b.Status).SingleAsync(ct);
        var final = Originals.Values.ToDictionary(d => d.Id);
        var added = new List<BillDetail>();
        if (!deleting)
            foreach (var line in bill.BillDetails)
            {
                if (line.Deleted == true) final.Remove(line.Id);
                else if (line.Id == 0) added.Add(line);
                else if (line.Touched == true) final[line.Id] = line;
            }
        var oldLines = Originals.Values.Where(d => oldStatus == 1 && d.Status == 1).ToArray();
        var newLines = deleting || bill.Status != 1 ? Array.Empty<BillDetail>() : final.Values.Concat(added).Where(d => d.Status == 1).ToArray();
        Require(newLines.All(d => d.Quantity > 0 && d.Rate >= 0), "Posted purchase lines require a positive quantity and nonnegative rate.");
        foreach (var item in Items.Values)
        {
            var old = oldLines.Where(d => d.ItemId == item.Id).ToArray();
            var current = newLines.Where(d => d.ItemId == item.Id).ToArray();
            var oldQuantity = old.Sum(d => d.Quantity); var newQuantity = current.Sum(d => d.Quantity);
            var oldValue = old.Sum(d => d.Quantity * d.Rate + (d.LandedCost ?? 0));
            var newValue = current.Sum(d => d.Quantity * d.Rate + (d.LandedCost ?? 0));
            if (oldQuantity == newQuantity && oldValue == newValue) continue;
            var snapshot = Summaries.GetValueOrDefault(item.Id);
            var quantity = snapshot?.Quantity ?? 0;
            var cost = snapshot?.AverageCost ?? item.AverageCost ?? 0;
            var remaining = quantity - oldQuantity + newQuantity;
            var value = quantity * cost - oldValue + newValue;
            var fallback = newQuantity > 0 ? newValue / newQuantity : oldQuantity > 0 ? oldValue / oldQuantity : cost;
            item.AverageCost = remaining > 0 && value > 0 ? value / remaining : fallback;
            if (current.Length > 0 && (item.LastPurchasedDate == null || item.LastPurchasedDate <= bill.BillDate || !item.Cost.HasValue))
            { item.LastPurchasedDate = bill.BillDate; item.Cost = current.Last().Rate; }
        }
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
