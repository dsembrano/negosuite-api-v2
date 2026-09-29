using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public static class ItemQuery
{
    public static IQueryable<Item> Search(IQueryable<Item> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.Code != null && i.Code.Contains(term)) ||
            (i.Name != null && i.Name.Contains(term)) || (i.Unit != null && i.Unit.Contains(term)) ||
            (i.ItemCategory != null && i.ItemCategory.Name.Contains(term)) ||
            (i.Type == "G" ? "Goods" : i.Type == "S" ? "Service" : "").Contains(term));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "code" or "name" or "itemCategoryName" or "type" or "typeName" or "unit" or "rate" or "cost" or "status" or "toSell" or "toPurchase" or "trackInventory" or "reorderPoint") &&
        (direction is null or "asc" or "desc");

    public static IOrderedQueryable<Item> Sort(IQueryable<Item> query, string field = null, string direction = null)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported item sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "code" => Order(query, i => i.Code, desc),
            "itemCategoryName" => Order(query, i => i.ItemCategory == null ? null : i.ItemCategory.Name, desc),
            "type" => Order(query, i => i.Type, desc),
            "typeName" => Order(query, i => i.Type == "G" ? "Goods" : i.Type == "S" ? "Service" : "", desc),
            "unit" => Order(query, i => i.Unit, desc),
            "rate" => Order(query, i => i.Rate, desc),
            "cost" => Order(query, i => i.Cost, desc),
            "status" => Order(query, i => i.Status, desc),
            "toSell" => Order(query, i => i.ToSell, desc),
            "toPurchase" => Order(query, i => i.ToPurchase, desc),
            "trackInventory" => Order(query, i => i.TrackInventory, desc),
            "reorderPoint" => Order(query, i => i.ReorderPoint, desc),
            _ => Order(query, i => i.Name, desc)
        };
        return ordered.ThenBy(i => i.Id);
    }
    private static IOrderedQueryable<Item> Order<T>(IQueryable<Item> query, Expression<Func<Item, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
