using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Transactions;
namespace negosuite_api.Services;

public static class StockTransferQuery
{
    public static bool IsValidSort(string field, string direction) => (field is null or "referenceNo" or "referenceDate" or "notes" or "fromInventoryLocationName" or "toInventoryLocationName" or "status" or "statusName") && (direction is null or "asc" or "desc");
    public static IQueryable<StockTransferListItemDto> Search(IQueryable<StockTransferListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.ReferenceNo != null && i.ReferenceNo.Contains(term)) || (i.Notes != null && i.Notes.Contains(term)) || (i.FromInventoryLocationName != null && i.FromInventoryLocationName.Contains(term)) || (i.ToInventoryLocationName != null && i.ToInventoryLocationName.Contains(term)) || (i.StatusName != null && i.StatusName.Contains(term)));
    }
    public static IOrderedQueryable<StockTransferListItemDto> Sort(IQueryable<StockTransferListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported transaction sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "referenceNo" => Order(query, i => i.ReferenceNo, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "fromInventoryLocationName" => Order(query, i => i.FromInventoryLocationName, desc),
            "toInventoryLocationName" => Order(query, i => i.ToInventoryLocationName, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReferenceDate, desc).ThenBy(i => i.ReferenceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }
    private static IOrderedQueryable<StockTransferListItemDto> Order<T>(IQueryable<StockTransferListItemDto> query, Expression<Func<StockTransferListItemDto, T>> key, bool desc) => desc ? query.OrderByDescending(key) : query.OrderBy(key);
}