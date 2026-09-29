using System.Linq;
using negosuite_api.Models;

namespace negosuite_api.Services;

public static class CustomerSearch
{
    public static IQueryable<Customer> Apply(IQueryable<Customer> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        // EXISTS preserves one row per customer even when several children match.
        // Apply to the already tenant/status-scoped query, before count, sort and paging.
        return query.Where(c => (c.Name != null && c.Name.Contains(term)) || (c.Tin != null && c.Tin.Contains(term)) ||
            (c.TaxRate != null && c.TaxRate.Name.Contains(term)) ||
            (c.PaymentTerm != null && c.PaymentTerm.Name.Contains(term)) ||
            c.CustomerContacts.Any(contact =>
                (contact.Name != null && contact.Name.Contains(term)) ||
                (contact.PhoneNo != null && contact.PhoneNo.Contains(term)) ||
                (contact.Email != null && contact.Email.Contains(term))) ||
            c.CustomerAddresses.Any(address =>
                (address.AddressLine1 != null && address.AddressLine1.Contains(term)) ||
                (address.AddressLine2 != null && address.AddressLine2.Contains(term)) ||
                (address.PostalCode != null && address.PostalCode.Contains(term)) ||
                (address.CityMunicipality != null &&
                    ((address.CityMunicipality.Name != null && address.CityMunicipality.Name.Contains(term)) ||
                     (address.CityMunicipality.PostalCode != null && address.CityMunicipality.PostalCode.Contains(term)) ||
                     (address.CityMunicipality.StateProvince != null && address.CityMunicipality.StateProvince.Name.Contains(term))))));
    }
}
