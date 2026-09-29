using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Models;

namespace negosuite_api.Services;

public static class CustomerSorting
{
    public static bool IsValid(string sortBy, string sortDirection) =>
        (sortBy is null or "name" or "tin" or "taxRateName" or "paymentTermName" or "creditLimit" or "status") &&
        (sortDirection is null or "asc" or "desc");

    public static IOrderedQueryable<Customer> Apply(IQueryable<Customer> query, string sortBy = null, string sortDirection = null)
    {
        if (!IsValid(sortBy, sortDirection)) throw new ArgumentException("Unsupported customer sort.");
        var descending = sortDirection == "desc";
        var ordered = sortBy switch
        {
            "tin" => Order(query, c => c.Tin, descending),
            "taxRateName" => Order(query, c => c.TaxRate == null ? null : c.TaxRate.Name, descending),
            "paymentTermName" => Order(query, c => c.PaymentTerm == null ? null : c.PaymentTerm.Name, descending),
            "creditLimit" => Order(query, c => c.CreditLimit, descending),
            "status" => Order(query, c => c.Status, descending),
            _ => Order(query, c => c.Name, descending)
        };
        // Keep equal values in a deterministic order across page boundaries in either direction.
        return ordered.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<Customer> Order<T>(IQueryable<Customer> query, Expression<Func<Customer, T>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);
}
