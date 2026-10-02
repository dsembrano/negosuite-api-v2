using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Accounts;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class AccountCategoryService
{
    private readonly negosuiteContext db;
    public AccountCategoryService(negosuiteContext db) => this.db = db;

    public IQueryable<AccountCategoryListDto> ListQuery(int company) => db.AccountCategories.AsNoTracking()
        .Where(c => c.UserConfigId == company).Select(c => new AccountCategoryListDto
        {
            Id = c.Id, Name = c.Name, Type = c.Type, AccountCodePrefix = c.AccountCodePrefix, OrderNo = c.OrderNo,
            AccountCount = db.Accounts.Count(a => a.CategoryId == c.Id && a.UserConfigId == company)
        });

    public async Task<object> ListAsync(int company, int? page, int? size, string search, string sort, string direction, CancellationToken ct)
    {
        var query = AccountCategoryQuery.Search(ListQuery(company), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = AccountCategoryQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<AccountCategoryListDto>(items, page.Value, size.Value, count) : items;
    }

    public async Task<AccountCategoryDetailDto> GetAsync(int company, int id, CancellationToken ct) => AccountMapping.ToDto(
        await db.AccountCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UserConfigId == company, ct));

    public async Task<(AccountCategoryDetailDto Item, string Error, bool Missing)> SaveAsync(int company, int? id, AccountCategoryWriteRequest input, CancellationToken ct)
    {
        var category = id.HasValue ? await db.AccountCategories.SingleOrDefaultAsync(c => c.Id == id && c.UserConfigId == company, ct)
            : new AccountCategory { UserConfigId = company, CreatedDate = DateTime.Now };
        if (category == null) return (null, null, true);
        if (input.UserConfigId != company) return (null, "Category company does not match configUuid.", false);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150) return (null, "Name is required and must not exceed 150 characters.", false);
        if (string.IsNullOrWhiteSpace(input.Type) || input.Type.Length > 10) return (null, "Type is required and must not exceed 10 characters.", false);
        AccountMapping.Apply(input, category);
        if (id.HasValue) category.LastUpdatedDate = DateTime.Now; else db.AccountCategories.Add(category);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.AccountCategories.AnyAsync(c => c.Id == id && c.UserConfigId == company, ct)) return (null, null, true);
            throw;
        }
        return (id.HasValue ? null : await GetAsync(company, category.Id, ct), null, false);
    }

    public async Task<bool> DeleteAsync(int company, int id, CancellationToken ct)
    {
        var category = await db.AccountCategories.SingleOrDefaultAsync(c => c.Id == id && c.UserConfigId == company, ct);
        if (category == null) return false;
        db.AccountCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
