using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.SalesInvoices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace negosuite_api.Services;

public static class SalesInvoiceQuery
{
    public static bool TryParseCenters(string input, out string json)
    {
        json = null;
        if (string.IsNullOrWhiteSpace(input)) return true;
        try
        {
            var ids = JArray.Parse("[" + input + "]");
            if (ids.Any(id => id.Type != JTokenType.Integer || !int.TryParse(id.ToString(), out var value) || value <= 0)) return false;
            json = ids.ToString(Formatting.None);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or OverflowException or ArgumentException) { return false; }
    }

    public static IQueryable<SalesInvoiceListItemDto> Search(IQueryable<SalesInvoiceListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.InvoiceNo != null && i.InvoiceNo.Contains(term)) ||
            (i.PurchaseOrderNo != null && i.PurchaseOrderNo.Contains(term)) ||
            (i.CustomerName != null && i.CustomerName.Contains(term)) || (i.CustomerTIN != null && i.CustomerTIN.Contains(term)) ||
            (i.PaymentTermName != null && i.PaymentTermName.Contains(term)) || (i.Notes != null && i.Notes.Contains(term)) ||
            (i.BillingAddress != null && i.BillingAddress.Contains(term)) || (i.BillingContactName != null && i.BillingContactName.Contains(term)) ||
            (i.BillingContactEmail != null && i.BillingContactEmail.Contains(term)) || (i.ShippingAddress != null && i.ShippingAddress.Contains(term)) ||
            (i.ShippingContactName != null && i.ShippingContactName.Contains(term)) || (i.ShippingContactEmail != null && i.ShippingContactEmail.Contains(term)) ||
            i.StatusName.Contains(term));
    }

    public static bool IsValidSort(string field, string direction) =>
        (field is null or "invoiceNo" or "invoiceDate" or "dueDate" or "purchaseOrderNo" or "customerName" or "customerTIN" or
            "billingAddress" or "billingContactName" or "billingContactEmail" or "shippingAddress" or "shippingContactName" or
            "shippingContactEmail" or "amount" or "balance" or "paymentTermName" or "notes" or "status" or "statusName") &&
        (direction is null or "asc" or "desc");

    public static IOrderedQueryable<SalesInvoiceListItemDto> Sort(IQueryable<SalesInvoiceListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported sales invoice sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "invoiceNo" => Order(query, i => i.InvoiceNo, desc),
            "dueDate" => Order(query, i => i.DueDate, desc),
            "purchaseOrderNo" => Order(query, i => i.PurchaseOrderNo, desc),
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
            "paymentTermName" => Order(query, i => i.PaymentTermName, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.InvoiceDate, desc).ThenBy(i => i.InvoiceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }

    private static IOrderedQueryable<SalesInvoiceListItemDto> Order<T>(IQueryable<SalesInvoiceListItemDto> query, Expression<Func<SalesInvoiceListItemDto, T>> key, bool desc) =>
        desc ? query.OrderByDescending(key) : query.OrderBy(key);
}
