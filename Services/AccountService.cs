using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Accounts;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class AccountService
{
    private readonly negosuiteContext db;
    public AccountService(negosuiteContext db) => this.db = db;

    public IQueryable<AccountListDto> ListQuery(int company, int? categoryId = null)
    {
        var query = db.Accounts.AsNoTracking().Where(a => a.UserConfigId == company);
        if (categoryId.HasValue && categoryId != 0) query = query.Where(a => a.CategoryId == categoryId);
        return query.Select(a => new AccountListDto
        {
            Id = a.Id, Code = a.Code ?? "", Name = a.Name, CategoryId = a.CategoryId,
            CategoryName = a.Category.UserConfigId == company ? a.Category.Name : null,
            ParentAccountId = a.ParentAccountId,
            ParentAccountCode = a.ParentAccount.UserConfigId == company ? a.ParentAccount.Code : null,
            ParentAccountName = a.ParentAccount.UserConfigId == company ? a.ParentAccount.Name : null,
            RequireCustomer = a.RequireCustomer, RequireSupplier = a.RequireSupplier,
            IsInventoryAccount = db.Items.Any(i => i.UserConfigId == company && i.TrackInventory == true && i.InventoryAccountId == a.Id),
            Type = a.Category.UserConfigId == company ? a.Category.Type : null, SortCode = ""
        });
    }

    public async Task<object> ListAsync(int company, int? page, int? size, string search, string sort, string direction, int? categoryId, CancellationToken ct)
    {
        var query = AccountQuery.Search(ListQuery(company, categoryId), search);
        var count = page.HasValue ? await query.CountAsync(ct) : 0;
        query = AccountQuery.Sort(query, sort, direction);
        if (page.HasValue) query = query.Skip((page.Value - 1) * size.Value).Take(size.Value);
        var items = await query.ToListAsync(ct);
        return page.HasValue ? new PagedResult<AccountListDto>(items, page.Value, size.Value, count) : items;
    }

    private IQueryable<Account> Details(int company) => db.Accounts.AsNoTracking()
        .Where(a => a.UserConfigId == company).Include(a => a.Category).Include(a => a.ParentAccount);

    private static AccountDetailDto Detail(Account account, int company)
    {
        var dto = AccountMapping.ToDto(account);
        if (dto?.Category != null && dto.Category.UserConfigId != company) dto.Category = null;
        if (dto?.ParentAccount != null && dto.ParentAccount.UserConfigId != company) dto.ParentAccount = null;
        return dto;
    }

    public async Task<AccountDetailDto> GetAsync(int company, int id, CancellationToken ct) =>
        Detail(await Details(company).SingleOrDefaultAsync(a => a.Id == id, ct), company);

    // These are complete lookup arrays, with the distinct legacy header/link response shapes.
    public async Task<object> LookupsAsync(int company, bool links, CancellationToken ct)
    {
        var accounts = await Details(company).OrderBy(a => a.Code).ThenBy(a => a.Id).ToListAsync(ct);
        var result = new List<AccountHeaderDto>();
        foreach (var account in accounts)
        {
            var detail = Detail(account, company);
            AccountHeaderDto row = links ? new AccountLinkDto { ParentAccount = detail.ParentAccount } : new AccountHeaderDto();
            row.Id = account.Id; row.Code = account.Code; row.Name = account.Name; row.CategoryId = account.CategoryId;
            row.CategoryName = detail.Category?.Name; row.ParentAccountId = account.ParentAccountId;
            row.ParentAccountCode = detail.ParentAccount?.Code; row.Category = detail.Category;
            result.Add(row);
        }
        // Keep the derived properties visible to System.Text.Json without enabling polymorphic input binding.
        return links ? (object)result.Cast<AccountLinkDto>().ToList() : result;
    }

    public async Task<(AccountDetailDto Item, string Error, bool Missing)> SaveAsync(int company, int? id, AccountWriteRequest input, CancellationToken ct)
    {
        var account = id.HasValue ? await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.UserConfigId == company, ct)
            : new Account { UserConfigId = company, CreatedDate = DateTime.Now };
        if (account == null) return (null, null, true);
        if (input.UserConfigId != company) return (null, "Account company does not match configUuid.", false);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150) return (null, "Name is required and must not exceed 150 characters.", false);
        if (input.Code?.Length > 20) return (null, "Code must not exceed 20 characters.", false);
        if (!await db.AccountCategories.AnyAsync(c => c.Id == input.CategoryId && c.UserConfigId == company, ct))
            return (null, "Category must belong to the selected company.", false);
        var parentId = input.ParentAccountId;
        var visited = new HashSet<int>();
        if (id.HasValue) visited.Add(id.Value);
        while (parentId.HasValue)
        {
            if (!visited.Add(parentId.Value)) return (null, "Parent account hierarchy cannot contain a cycle.", false);
            var parent = await db.Accounts.AsNoTracking().Where(a => a.Id == parentId && a.UserConfigId == company)
                .Select(a => new { a.ParentAccountId }).SingleOrDefaultAsync(ct);
            if (parent == null) return (null, "Parent account must belong to the selected company.", false);
            parentId = parent.ParentAccountId;
        }
        AccountMapping.Apply(input, account);
        if (id.HasValue) account.LastUpdatedDate = DateTime.Now; else db.Accounts.Add(account);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.Accounts.AnyAsync(a => a.Id == id && a.UserConfigId == company, ct)) return (null, null, true);
            throw;
        }
        return (await GetAsync(company, account.Id, ct), null, false);
    }

    public async Task<bool> DeleteAsync(int company, int id, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.UserConfigId == company, ct);
        if (account == null) return false;
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
