using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Items;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class ItemService
{
    private readonly negosuiteContext db;
    public ItemService(negosuiteContext db) => this.db = db;
    public const string DuplicateSku = "SKU already exist. Duplicate is not allowed";

    public async Task<object> ListAsync(int company, ItemListCriteria filter, int? page, int? size, string search, string sort, string direction, bool? toSell, bool? toPurchase, CancellationToken ct)
    {
        var query = db.Items.AsNoTracking().Where(i => i.UserConfigId == company);
        if (filter.ShowInactive != true) query = query.Where(i => i.Status);
        if (filter.ItemType != null) query = query.Where(i => i.Type == filter.ItemType);
        if (filter.ItemCategoryId.HasValue) query = query.Where(i => i.ItemCategoryId == filter.ItemCategoryId);
        // New explicit query flags avoid changing historically ignored legacy criteria properties.
        if (toSell.HasValue) query = query.Where(i => i.ToSell == toSell);
        if (toPurchase.HasValue) query = query.Where(i => i.ToPurchase == toPurchase);
        query = ItemQuery.Search(query, search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = ItemQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.Select(i => new ItemListItemDto
        {
            Id = i.Id,
            Code = i.Code,
            Name = i.Name,
            ItemCategoryId = i.ItemCategoryId,
            ItemCategoryName = i.ItemCategory.Name,
            TypeName = i.Type == "G" ? "Goods" : i.Type == "S" ? "Service" : "",
            Type = i.Type,
            Unit = i.Unit,
            Rate = i.Rate,
            Cost = i.Cost,
            Status = i.Status,
            ToSell = i.ToSell,
            ToPurchase = i.ToPurchase,
            TrackInventory = i.TrackInventory,
            ReorderPoint = i.ReorderPoint
        }).ToListAsync(ct);
        return page.HasValue ? new PagedResult<ItemListItemDto>(items, page.Value, size.Value, count) : items;
    }

    public async Task<ItemDetailDto> GetAsync(int company, int id, CancellationToken ct)
    {
        var item = await db.Items.AsNoTrackingWithIdentityResolution().Where(i => i.Id == id && i.UserConfigId == company)
            .Include(i => i.ItemCategory)
            .Include(i => i.PurchaseAccount).ThenInclude(a => a.Category).Include(i => i.PurchaseTaxRate)
            .Include(i => i.SalesAccount).ThenInclude(a => a.Category).Include(i => i.SalesTaxRate)
            .Include(i => i.InventoryAccount).ThenInclude(a => a.Category).SingleOrDefaultAsync(ct);
        return ItemMapping.Map(item);
    }

    public Task<List<ItemUnitDto>> UnitsAsync(int company, CancellationToken ct) => db.Items.AsNoTracking()
        .Where(i => i.UserConfigId == company && i.Unit != null && i.Unit != "")
        .Select(i => new ItemUnitDto(i.Unit)).Distinct().ToListAsync(ct);

    public async Task<decimal?> AverageCostAsync(int company, int id, CancellationToken ct)
    {
        if (!await db.Items.AnyAsync(i => i.Id == id && i.UserConfigId == company, ct)) return null;
        // Keep division in MySQL to preserve the existing decimal precision/rounding behavior.
        return await db.BillDetails.AsNoTracking().Where(d => d.ItemId == id && db.Bills.Any(b => b.Id == d.BillId && b.UserConfigId == company))
            .GroupBy(d => true).Select(g => g.Sum(d => d.Quantity) == 0 ? (decimal?)null :
                g.Sum(d => d.Rate * d.Quantity) / g.Sum(d => d.Quantity)).FirstOrDefaultAsync(ct);
    }

    public async Task<(ItemDetailDto Item, string Error, bool Missing)> SaveAsync(int company, int? id, ItemWriteRequest input, CancellationToken ct)
    {
        var item = id.HasValue ? await db.Items.SingleOrDefaultAsync(i => i.Id == id && i.UserConfigId == company, ct) : new Item { UserConfigId = company, CreatedDate = DateTime.UtcNow };
        if (item == null) return (null, null, true);
        if (input.UserConfigId != company) return (null, "Item company does not match configUuid.", false);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150) return (null, "Item name is required and must not exceed 150 characters.", false);
        if (input.Code != null && await db.Items.AnyAsync(i => i.UserConfigId == company && i.Code == input.Code && i.Id != item.Id, ct)) return (null, DuplicateSku, false);
        if (input.ItemCategoryId.HasValue && !await db.ItemCategories.AnyAsync(c => c.Id == input.ItemCategoryId && c.UserConfigId == company, ct)) return (null, "Invalid item category for this company.", false);
        var accountIds = new[] { input.PurchaseAccountId, input.SalesAccountId, input.InventoryAccountId }.Where(i => i.HasValue).Select(i => i.Value).Distinct().ToArray();
        if (accountIds.Length != await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.UserConfigId == company, ct)) return (null, "Invalid account for this company.", false);
        var taxIds = new[] { input.PurchaseTaxRateId, input.SalesTaxRateId }.Where(i => i.HasValue).Select(i => i.Value).Distinct().ToArray();
        if (taxIds.Length != await db.TaxRates.CountAsync(t => taxIds.Contains(t.Id) && t.UserConfigId == company, ct)) return (null, "Invalid tax rate for this company.", false);
        item.Code = input.Code; item.Name = input.Name; item.Type = input.Type; item.Unit = input.Unit;
        item.ItemCategoryId = input.ItemCategoryId; item.ToPurchase = input.ToPurchase; item.Cost = input.Cost;
        item.PurchaseAccountId = input.PurchaseAccountId; item.PurchaseTaxRateId = input.PurchaseTaxRateId;
        item.ToSell = input.ToSell; item.Rate = input.Rate; item.SalesAccountId = input.SalesAccountId; item.SalesTaxRateId = input.SalesTaxRateId;
        item.Notes = input.Notes; item.TrackInventory = input.TrackInventory; item.InventoryAccountId = input.InventoryAccountId;
        item.OpeningQuantity = input.OpeningQuantity; item.ReorderPoint = input.ReorderPoint; item.Status = input.Status;
        if (id.HasValue) item.LastUpdatedDate = DateTime.UtcNow; else db.Items.Add(item);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            // The database's company/SKU unique index also handles concurrent requests.
            if (input.Code != null && await db.Items.AsNoTracking().AnyAsync(i => i.UserConfigId == company && i.Code == input.Code && i.Id != item.Id, ct))
                return (null, DuplicateSku, false);
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.Items.AsNoTracking().AnyAsync(i => i.Id == item.Id && i.UserConfigId == company, ct)) return (null, null, true);
            throw;
        }
        return (id.HasValue ? null : await GetAsync(company, item.Id, ct), null, false);
    }

    public async Task<bool> DeleteAsync(int company, int id, CancellationToken ct)
    {
        var item = await db.Items.SingleOrDefaultAsync(i => i.Id == id && i.UserConfigId == company, ct);
        if (item == null) return false;
        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
