using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.SalesInvoicePayments;

namespace negosuite_api.Services;

public static class SalesInvoicePaymentQuery
{
    public static IQueryable<SalesInvoicePaymentListItemDto> Search(IQueryable<SalesInvoicePaymentListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.ReferenceNo != null && i.ReferenceNo.Contains(term)) ||
            (i.CustomerName != null && i.CustomerName.Contains(term)) ||
            (i.PaymentModeName != null && i.PaymentModeName.Contains(term)) ||
            (i.DepositToAccountName != null && i.DepositToAccountName.Contains(term)) ||
            (i.Notes != null && i.Notes.Contains(term)) ||
            (i.StatusName != null && i.StatusName.Contains(term)));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "referenceNo" or "referenceDate" or "customerName" or "paymentModeName" or "depositToAccountName" or "amount" or "balance" or "notes" or "status" or "statusName") && (direction is null or "asc" or "desc");

    public static IOrderedQueryable<SalesInvoicePaymentListItemDto> Sort(IQueryable<SalesInvoicePaymentListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported SalesInvoicePayment sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "referenceNo" => Order(query, i => i.ReferenceNo, desc),
            "customerName" => Order(query, i => i.CustomerName, desc),
            "paymentModeName" => Order(query, i => i.PaymentModeName, desc),
            "depositToAccountName" => Order(query, i => i.DepositToAccountName, desc),
            "amount" => Order(query, i => i.Amount, desc),
            "balance" => Order(query, i => i.Balance, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReferenceDate, desc).ThenBy(i => i.ReferenceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }

    private static IOrderedQueryable<SalesInvoicePaymentListItemDto> Order<T>(IQueryable<SalesInvoicePaymentListItemDto> query, Expression<Func<SalesInvoicePaymentListItemDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
