using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Bills;

namespace negosuite_api.Services;

public static class BillQuery
{
    public static IQueryable<BillListItemDto> Search(IQueryable<BillListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.BillNo != null && i.BillNo.Contains(term)) ||
            (i.SupplierName != null && i.SupplierName.Contains(term)) ||
            (i.PaymentTermName != null && i.PaymentTermName.Contains(term)) ||
            (i.SupplierTIN != null && i.SupplierTIN.Contains(term)) ||
            (i.Notes != null && i.Notes.Contains(term)) ||
            (i.StatusName != null && i.StatusName.Contains(term)));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "dueDate" or "billNo" or "billDate" or "supplierName" or "paymentTermName" or "supplierTIN" or "amount" or "balance" or "notes" or "status" or "statusName") && (direction is null or "asc" or "desc");

    public static IOrderedQueryable<BillListItemDto> Sort(IQueryable<BillListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported Bill sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "dueDate" => Order(query, i => i.DueDate, desc),
            "billNo" => Order(query, i => i.BillNo, desc),
            "supplierName" => Order(query, i => i.SupplierName, desc),
            "paymentTermName" => Order(query, i => i.PaymentTermName, desc),
            "supplierTIN" => Order(query, i => i.SupplierTIN, desc),
            "amount" => Order(query, i => i.Amount, desc),
            "balance" => Order(query, i => i.Balance, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.BillDate, desc).ThenBy(i => i.BillNo)
        };
        return ordered.ThenBy(i => i.Id);
    }

    private static IOrderedQueryable<BillListItemDto> Order<T>(IQueryable<BillListItemDto> query, Expression<Func<BillListItemDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
