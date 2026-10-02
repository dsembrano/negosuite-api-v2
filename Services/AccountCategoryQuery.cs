using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Services;

public static class AccountCategoryQuery
{
    public static bool IsValidSort(string field, string direction) =>
        (field is null or "name" or "type" or "accountCodePrefix" or "orderNo" or "accountCount") && (direction is null or "asc" or "desc");

    public static IQueryable<AccountCategoryListDto> Search(IQueryable<AccountCategoryListDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(c => (c.Name != null && c.Name.Contains(term)) || (c.Type != null && c.Type.Contains(term)) || (c.AccountCodePrefix != null && c.AccountCodePrefix.Contains(term)));
    }

    public static IOrderedQueryable<AccountCategoryListDto> Sort(IQueryable<AccountCategoryListDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported sort.");
        var desc = direction == "desc";
        var ordered = (field ?? "orderNo") switch
        {
            "name" => Order(query, c => c.Name, desc),
            "type" => Order(query, c => c.Type, desc),
            "accountCodePrefix" => Order(query, c => c.AccountCodePrefix, desc),
            "orderNo" => Order(query, c => c.OrderNo, desc),
            "accountCount" => Order(query, c => c.AccountCount, desc),
            _ => throw new ArgumentException("Unsupported sort.")
        };
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<AccountCategoryListDto> Order<T>(IQueryable<AccountCategoryListDto> query, Expression<Func<AccountCategoryListDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
