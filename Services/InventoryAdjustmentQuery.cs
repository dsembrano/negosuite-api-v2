using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Transactions;
namespace negosuite_api.Services;

public static class InventoryAdjustmentQuery
{
    public static bool IsValidSort(string field, string direction) => (field is null or "referenceNo" or "referenceDate" or "customerName" or "notes" or "inventoryLocationName" or "status" or "statusName") && (direction is null or "asc" or "desc");
    public static IQueryable<InventoryAdjustmentListItemDto> Search(IQueryable<InventoryAdjustmentListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.ReferenceNo != null && i.ReferenceNo.Contains(term)) || (i.CustomerName != null && i.CustomerName.Contains(term)) || (i.Notes != null && i.Notes.Contains(term)) || (i.InventoryLocationName != null && i.InventoryLocationName.Contains(term)) || (i.StatusName != null && i.StatusName.Contains(term)));
    }
    public static IOrderedQueryable<InventoryAdjustmentListItemDto> Sort(IQueryable<InventoryAdjustmentListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported transaction sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "referenceNo" => Order(query, i => i.ReferenceNo, desc),
            "customerName" => Order(query, i => i.CustomerName, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "inventoryLocationName" => Order(query, i => i.InventoryLocationName, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReferenceDate, desc).ThenBy(i => i.ReferenceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }
    private static IOrderedQueryable<InventoryAdjustmentListItemDto> Order<T>(IQueryable<InventoryAdjustmentListItemDto> query, Expression<Func<InventoryAdjustmentListItemDto, T>> key, bool desc) => desc ? query.OrderByDescending(key) : query.OrderBy(key);
}