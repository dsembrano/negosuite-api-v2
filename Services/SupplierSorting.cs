using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public static class SupplierSorting
{
    public static bool IsValid(string sortBy, string sortDirection) =>
        (sortBy is null or "name" or "tin" or "taxRateName" or "paymentTermName" or "status") &&
        (sortDirection is null or "asc" or "desc");

    public static IOrderedQueryable<Supplier> Apply(IQueryable<Supplier> query, string sortBy = null, string sortDirection = null)
    {
        if (!IsValid(sortBy, sortDirection)) throw new ArgumentException("Unsupported supplier sort.");
        var descending = sortDirection == "desc";
        var ordered = sortBy switch
        {
            "tin" => Order(query, c => c.Tin, descending),
            "taxRateName" => Order(query, c => c.TaxRate == null ? null : c.TaxRate.Name, descending),
            "paymentTermName" => Order(query, c => c.PaymentTerm == null ? null : c.PaymentTerm.Name, descending),
            "status" => Order(query, c => c.Status, descending),
            _ => Order(query, c => c.Name, descending)
        };
        // Keep equal values in a deterministic order across page boundaries in either direction.
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<Supplier> Order<T>(IQueryable<Supplier> query, Expression<Func<Supplier, T>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);
}
