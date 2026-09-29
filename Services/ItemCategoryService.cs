using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.ItemCategories;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class ItemCategoryService
{
    private readonly negosuiteContext db;
    public ItemCategoryService(negosuiteContext db) => this.db = db;

    private static readonly Expression<Func<ItemCategory, ItemCategoryDetailDto>> Projection = c => new ItemCategoryDetailDto
    {
        Id = c.Id,
        UserConfigId = c.UserConfigId,
        Name = c.Name,
        Status = c.Status,
        CreatedDate = c.CreatedDate,
        LastUpdatedDate = c.LastUpdatedDate,
        CreatedByUserId = c.CreatedByUserId,
        LastUpdatedByUserId = c.LastUpdatedByUserId
    };

    public async Task<object> ListAsync(int company, int? page, int? size, string search, string sort, string direction, bool? status, CancellationToken ct)
    {
        var query = db.ItemCategories.AsNoTracking().Where(c => c.UserConfigId == company);
        // Categories historically include inactive rows. Filtering is explicitly opt-in.
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        query = ItemCategoryQuery.Search(query, search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        // Leave the legacy unordered query alone; pages always have deterministic ordering.
        if (page.HasValue || sort != null || direction != null) query = ItemCategoryQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.Select(Projection).ToListAsync(ct);
        return page.HasValue ? new PagedResult<ItemCategoryDetailDto>(items, page.Value, size.Value, count) : items;
    }

    public Task<ItemCategoryDetailDto> GetAsync(int company, int id, CancellationToken ct) => db.ItemCategories.AsNoTracking()
        .Where(c => c.Id == id && c.UserConfigId == company).Select(Projection).SingleOrDefaultAsync(ct);

    public async Task<(ItemCategoryDetailDto Item, string Error, bool Missing)> SaveAsync(int company, int? id, ItemCategoryWriteRequest input, CancellationToken ct)
    {
        var category = id.HasValue ? await db.ItemCategories.SingleOrDefaultAsync(c => c.Id == id && c.UserConfigId == company, ct)
            : new ItemCategory { UserConfigId = company, CreatedDate = DateTime.Now };
        if (category == null) return (null, null, true);
        if (input.UserConfigId != company) return (null, "Item category company does not match configUuid.", false);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150)
            return (null, "Item category name is required and must not exceed 150 characters.", false);
        category.Name = input.Name;
        category.Status = input.Status;
        if (id.HasValue) category.LastUpdatedDate = DateTime.Now; else db.ItemCategories.Add(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.ItemCategories.AsNoTracking().AnyAsync(c => c.Id == category.Id && c.UserConfigId == company, ct)) return (null, null, true);
            throw;
        }
        return (id.HasValue ? null : await GetAsync(company, category.Id, ct), null, false);
    }

    public async Task<bool> DeleteAsync(int company, int id, CancellationToken ct)
    {
        var category = await db.ItemCategories.SingleOrDefaultAsync(c => c.Id == id && c.UserConfigId == company, ct);
        if (category == null) return false;
        db.ItemCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
