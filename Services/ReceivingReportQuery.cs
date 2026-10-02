using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Transactions;
namespace negosuite_api.Services;

public static class ReceivingReportQuery
{
    public static bool IsValidSort(string field, string direction) => (field is null or "referenceNo" or "referenceDate" or "supplierName" or "amount" or "balance" or "deliveryReceiptNo" or "purchaseOrderNo" or "inventoryLocationName" or "notes" or "status" or "statusName") && (direction is null or "asc" or "desc");
    public static IQueryable<ReceivingReportListItemDto> Search(IQueryable<ReceivingReportListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.ReferenceNo != null && i.ReferenceNo.Contains(term)) || (i.SupplierName != null && i.SupplierName.Contains(term)) || (i.DeliveryReceiptNo != null && i.DeliveryReceiptNo.Contains(term)) || (i.PurchaseOrderNo != null && i.PurchaseOrderNo.Contains(term)) || (i.InventoryLocationName != null && i.InventoryLocationName.Contains(term)) || (i.Notes != null && i.Notes.Contains(term)) || (i.StatusName != null && i.StatusName.Contains(term)));
    }
    public static IOrderedQueryable<ReceivingReportListItemDto> Sort(IQueryable<ReceivingReportListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported transaction sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "referenceNo" => Order(query, i => i.ReferenceNo, desc),
            "supplierName" => Order(query, i => i.SupplierName, desc),
            "amount" => Order(query, i => i.Amount, desc),
            "balance" => Order(query, i => i.Balance, desc),
            "deliveryReceiptNo" => Order(query, i => i.DeliveryReceiptNo, desc),
            "purchaseOrderNo" => Order(query, i => i.PurchaseOrderNo, desc),
            "inventoryLocationName" => Order(query, i => i.InventoryLocationName, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReferenceDate, desc).ThenBy(i => i.ReferenceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }
    private static IOrderedQueryable<ReceivingReportListItemDto> Order<T>(IQueryable<ReceivingReportListItemDto> query, Expression<Func<ReceivingReportListItemDto, T>> key, bool desc) => desc ? query.OrderByDescending(key) : query.OrderBy(key);
}