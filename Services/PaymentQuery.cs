using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Payments;

namespace negosuite_api.Services;

public static class PaymentQuery
{
    public static IQueryable<PaymentListItemDto> Search(IQueryable<PaymentListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.SupplierName != null && i.SupplierName.Contains(term)) || (i.Payee != null && i.Payee.Contains(term)) || (i.CheckNo != null && i.CheckNo.Contains(term)) || (i.ReferenceNo != null && i.ReferenceNo.Contains(term)) ||
            (i.CustomerrName != null && i.CustomerrName.Contains(term)) ||
            (i.PaymentModeName != null && i.PaymentModeName.Contains(term)) ||
            (i.PaidThroughAccountName != null && i.PaidThroughAccountName.Contains(term)) ||
            (i.Notes != null && i.Notes.Contains(term)) ||
            (i.StatusName != null && i.StatusName.Contains(term)));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "supplierName" or "payee" or "checkNo" or "customerName" or "referenceNo" or "referenceDate" or "customerrName" or "paymentModeName" or "paidThroughAccountName" or "amount" or "balance" or "notes" or "status" or "statusName") && (direction is null or "asc" or "desc");

    public static IOrderedQueryable<PaymentListItemDto> Sort(IQueryable<PaymentListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported Payment sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "supplierName" => Order(query, i => i.SupplierName, desc),
            "payee" => Order(query, i => i.Payee, desc),
            "checkNo" => Order(query, i => i.CheckNo, desc),
            "customerName" => Order(query, i => i.CustomerrName, desc),
            "referenceNo" => Order(query, i => i.ReferenceNo, desc),
            "customerrName" => Order(query, i => i.CustomerrName, desc),
            "paymentModeName" => Order(query, i => i.PaymentModeName, desc),
            "paidThroughAccountName" => Order(query, i => i.PaidThroughAccountName, desc),
            "amount" => Order(query, i => i.Amount, desc),
            "balance" => Order(query, i => i.Balance, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReferenceDate, desc).ThenBy(i => i.ReferenceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }

    private static IOrderedQueryable<PaymentListItemDto> Order<T>(IQueryable<PaymentListItemDto> query, Expression<Func<PaymentListItemDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
