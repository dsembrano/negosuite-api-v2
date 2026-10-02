using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Services;

public static class AccountQuery
{
    public static bool IsValidSort(string field, string direction) =>
        (field is null or "code" or "name" or "categoryName" or "parentAccountCode" or "parentAccountName" or "requireCustomer" or "requireSupplier" or "type") && (direction is null or "asc" or "desc");

    public static IQueryable<AccountListDto> Search(IQueryable<AccountListDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(c => (c.Code != null && c.Code.Contains(term)) || (c.Name != null && c.Name.Contains(term)) || (c.CategoryName != null && c.CategoryName.Contains(term)) || (c.ParentAccountCode != null && c.ParentAccountCode.Contains(term)) || (c.ParentAccountName != null && c.ParentAccountName.Contains(term)) || (c.Type != null && c.Type.Contains(term)));
    }

    public static IOrderedQueryable<AccountListDto> Sort(IQueryable<AccountListDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported sort.");
        var desc = direction == "desc";
        var ordered = (field ?? "code") switch
        {
            "code" => Order(query, c => c.Code, desc),
            "name" => Order(query, c => c.Name, desc),
            "categoryName" => Order(query, c => c.CategoryName, desc),
            "parentAccountCode" => Order(query, c => c.ParentAccountCode, desc),
            "parentAccountName" => Order(query, c => c.ParentAccountName, desc),
            "requireCustomer" => Order(query, c => c.RequireCustomer, desc),
            "requireSupplier" => Order(query, c => c.RequireSupplier, desc),
            "type" => Order(query, c => c.Type, desc),
            _ => throw new ArgumentException("Unsupported sort.")
        };
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<AccountListDto> Order<T>(IQueryable<AccountListDto> query, Expression<Func<AccountListDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
