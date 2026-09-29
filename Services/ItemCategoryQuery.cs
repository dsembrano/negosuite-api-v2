using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public static class ItemCategoryQuery
{
    public static IQueryable<ItemCategory> Search(IQueryable<ItemCategory> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(c => c.Name != null && c.Name.Contains(term));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "name" or "status" or "createdDate" or "lastUpdatedDate") &&
        (direction is null or "asc" or "desc");

    public static IOrderedQueryable<ItemCategory> Sort(IQueryable<ItemCategory> query, string field = null, string direction = null)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported item category sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "status" => Order(query, c => c.Status, desc),
            "createdDate" => Order(query, c => c.CreatedDate, desc),
            "lastUpdatedDate" => Order(query, c => c.LastUpdatedDate, desc),
            _ => Order(query, c => c.Name, desc)
        };
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<ItemCategory> Order<T>(IQueryable<ItemCategory> query, Expression<Func<ItemCategory, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
