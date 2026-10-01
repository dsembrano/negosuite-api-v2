using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.SalesReceipts;

namespace negosuite_api.Services;

public static class SalesReceiptQuery
{
    public static IQueryable<SalesReceiptListItemDto> Search(IQueryable<SalesReceiptListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.ReceiptNo != null && i.ReceiptNo.Contains(term)) ||
            (i.CustomerName != null && i.CustomerName.Contains(term)) ||
            (i.CustomerTIN != null && i.CustomerTIN.Contains(term)) ||
            (i.BillingAddress != null && i.BillingAddress.Contains(term)) ||
            (i.BillingContactName != null && i.BillingContactName.Contains(term)) ||
            (i.BillingContactEmail != null && i.BillingContactEmail.Contains(term)) ||
            (i.ShippingAddress != null && i.ShippingAddress.Contains(term)) ||
            (i.ShippingContactName != null && i.ShippingContactName.Contains(term)) ||
            (i.ShippingContactEmail != null && i.ShippingContactEmail.Contains(term)) ||
            (i.PaymentModeName != null && i.PaymentModeName.Contains(term)) ||
            (i.Notes != null && i.Notes.Contains(term)) ||
            (i.StatusName != null && i.StatusName.Contains(term)));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "receiptNo" or "receiptDate" or "customerName" or "customerTIN" or "billingAddress" or "billingContactName" or "billingContactEmail" or "shippingAddress" or "shippingContactName" or "shippingContactEmail" or "amount" or "balance" or "paymentModeName" or "notes" or "status" or "createdDate" or "statusName") && (direction is null or "asc" or "desc");

    public static IOrderedQueryable<SalesReceiptListItemDto> Sort(IQueryable<SalesReceiptListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported SalesReceipt sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "receiptNo" => Order(query, i => i.ReceiptNo, desc),
            "customerName" => Order(query, i => i.CustomerName, desc),
            "customerTIN" => Order(query, i => i.CustomerTIN, desc),
            "billingAddress" => Order(query, i => i.BillingAddress, desc),
            "billingContactName" => Order(query, i => i.BillingContactName, desc),
            "billingContactEmail" => Order(query, i => i.BillingContactEmail, desc),
            "shippingAddress" => Order(query, i => i.ShippingAddress, desc),
            "shippingContactName" => Order(query, i => i.ShippingContactName, desc),
            "shippingContactEmail" => Order(query, i => i.ShippingContactEmail, desc),
            "amount" => Order(query, i => i.Amount, desc),
            "balance" => Order(query, i => i.Balance, desc),
            "paymentModeName" => Order(query, i => i.PaymentModeName, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "createdDate" => Order(query, i => i.CreatedDate, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReceiptDate, desc).ThenBy(i => i.ReceiptNo)
        };
        return ordered.ThenBy(i => i.Id);
    }

    private static IOrderedQueryable<SalesReceiptListItemDto> Order<T>(IQueryable<SalesReceiptListItemDto> query, Expression<Func<SalesReceiptListItemDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
