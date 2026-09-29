using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class CustomerService
{
    private readonly negosuiteContext db;
    public CustomerService(negosuiteContext db) => this.db = db;

    public async Task<object> ListAsync(int companyId, bool showInactive, int? pageNumber, int? pageSize, CancellationToken ct, string search = null, string sortBy = null, string sortDirection = null)
    {
        var query = db.Customers.AsNoTracking().Where(c => c.UserConfigId == companyId);
        if (!showInactive) query = query.Where(c => c.Status);
        query = CustomerSearch.Apply(query, search);
        var total = pageNumber.HasValue ? await query.CountAsync(ct) : 0;
        query = CustomerSorting.Apply(query, sortBy, sortDirection);
        if (pageNumber.HasValue) query = query.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value);
        var items = await query.Select(c => new CustomerListItemDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            Tin = c.Tin,
            Status = c.Status,
            TaxRateId = c.TaxRateId,
            TaxRateName = c.TaxRate.Name,
            PaymentTermId = c.PaymentTermId,
            PaymentTermName = c.PaymentTerm.Name,
            PaymentTermDays = c.PaymentTerm.Days,
            PaymentTermCode = c.PaymentTerm.Code,
            CreditLimit = c.CreditLimit
        }).ToListAsync(ct);
        if (items.Count > 0)
        {
            var ids = items.Select(c => c.Id).ToArray();
            // Pages have at most 200 IDs. Full lists use a scoped subquery rather than a huge IN clause.
            var addressQuery = db.CustomerAddresses.AsNoTracking();
            var contactQuery = db.CustomerContacts.AsNoTracking();
            if (pageNumber.HasValue)
            {
                addressQuery = addressQuery.Where(a => ids.Contains(a.CustomerId));
                contactQuery = contactQuery.Where(c => ids.Contains(c.CustomerId));
            }
            else
            {
                addressQuery = addressQuery.Where(a => query.Any(c => c.Id == a.CustomerId));
                contactQuery = contactQuery.Where(a => query.Any(c => c.Id == a.CustomerId));
            }
            var addresses = (await addressQuery
                .Include(a => a.CityMunicipality).ThenInclude(c => c.StateProvince).OrderBy(a => a.Id).ToListAsync(ct)).ToLookup(a => a.CustomerId);
            var contacts = (await contactQuery.OrderBy(c => c.Id).ToListAsync(ct)).ToLookup(c => c.CustomerId);
            foreach (var item in items)
            {
                item.CustomerAddresses = addresses[item.Id].Select(CustomerMapping.Map).ToList();
                item.CustomerContacts = contacts[item.Id].Select(CustomerMapping.Map).ToList();
                item.StringCustomerAddresses = addresses[item.Id].Select(a =>
                    $"{a.AddressLine1} {a.AddressLine2 ?? ""}{a.CityMunicipality?.Name}, {a.CityMunicipality?.StateProvince?.Name} {a.CityMunicipality?.PostalCode ?? ""}").ToList();
            }
        }
        return pageNumber.HasValue ? new PagedResult<CustomerListItemDto>(items, pageNumber.Value, pageSize.Value, total) : items;
    }

    public async Task<CustomerDetailDto> GetAsync(int companyId, int id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().Where(c => c.Id == id && c.UserConfigId == companyId)
            .Include(c => c.TaxRate).Include(c => c.PaymentTerm).SingleOrDefaultAsync(ct);
        if (customer == null) return null;
        var result = CustomerMapping.Map(customer);
        result.CustomerAddresses = (await db.CustomerAddresses.AsNoTracking().Where(a => a.CustomerId == id)
            .Include(a => a.CityMunicipality).ThenInclude(c => c.StateProvince).OrderBy(a => a.Id).ToListAsync(ct)).Select(CustomerMapping.Map).ToList();
        result.CustomerContacts = (await db.CustomerContacts.AsNoTracking().Where(c => c.CustomerId == id).OrderBy(c => c.Id).ToListAsync(ct)).Select(CustomerMapping.Map).ToList();
        return result;
    }

    // Return validation errors before mutating the tracked aggregate.
    private async Task<string> ValidateAsync(int companyId, CustomerWriteRequest input, Customer customer, CancellationToken ct)
    {
        if (input.UserConfigId != companyId) return "Customer company does not match configUuid.";
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 150) return "Customer name is required and must not exceed 150 characters.";
        if (input.Tin?.Length > 50) return "TIN must not exceed 50 characters.";
        input.CustomerContacts ??= new();
        input.CustomerAddresses ??= new();
        if (input.CustomerContacts.Any(c => c == null) || input.CustomerAddresses.Any(a => a == null)) return "Child records cannot be null.";
        var contacts = input.CustomerContacts;
        var addresses = input.CustomerAddresses;
        if (contacts.Any(c => c.Id < 0 || (c.CustomerId != 0 && c.CustomerId != customer.Id)) ||
            addresses.Any(a => a.Id < 0 || (a.CustomerId != 0 && a.CustomerId != customer.Id))) return "Child record belongs to another customer.";
        if (contacts.Where(c => c.Id != 0).GroupBy(c => c.Id).Any(g => g.Count() > 1) ||
            addresses.Where(a => a.Id != 0).GroupBy(a => a.Id).Any(g => g.Count() > 1)) return "Duplicate child record ID.";
        if (contacts.Any(c => c.Id != 0 && !customer.CustomerContacts.Any(x => x.Id == c.Id)) ||
            addresses.Any(a => a.Id != 0 && !customer.CustomerAddresses.Any(x => x.Id == a.Id))) return "Child record does not belong to this customer.";
        if (addresses.Any(a => a.PostalCode?.Length > 20)) return "Postal code must not exceed 20 characters.";
        if (input.TaxRateId.HasValue && !await db.TaxRates.AnyAsync(t => t.Id == input.TaxRateId && t.UserConfigId == companyId, ct)) return "Invalid tax rate for this company.";
        if (input.PaymentTermId.HasValue && !await db.PaymentTerms.AnyAsync(t => t.Id == input.PaymentTermId, ct)) return "Invalid payment term.";
        var cities = addresses.Where(a => a.Deleted != true).Select(a => a.CityMunicipalityId).Distinct().ToArray();
        if (cities.Length != await db.CityMunicipalities.CountAsync(c => cities.Contains(c.Id), ct)) return "Invalid city or municipality.";
        return null;
    }

    public async Task<(CustomerDetailDto Customer, string Error, bool Missing)> SaveAsync(int companyId, int? id, CustomerWriteRequest input, CancellationToken ct)
    {
        Customer customer;
        if (id.HasValue)
        {
            customer = await db.Customers.Where(c => c.Id == id && c.UserConfigId == companyId)
                .Include(c => c.CustomerAddresses).Include(c => c.CustomerContacts).AsSplitQuery().SingleOrDefaultAsync(ct);
            if (customer == null) return (null, null, true);
        }
        else customer = new Customer { UserConfigId = companyId, CreatedDate = DateTime.Now };
        var error = await ValidateAsync(companyId, input, customer, ct);
        if (error != null) return (null, error, false);
        customer.Code = input.Code; customer.Name = input.Name; customer.Tin = input.Tin;
        customer.CreditLimit = input.CreditLimit; customer.TaxRateId = input.TaxRateId; customer.PaymentTermId = input.PaymentTermId;
        customer.Notes = input.Notes; customer.BusinessStyle = input.BusinessStyle; customer.Status = input.Status;
        var now = DateTime.Now;
        if (id.HasValue) customer.LastUpdatedDate = now;
        foreach (var item in input.CustomerContacts)
        {
            if (item.Id == 0 && item.Deleted == true) continue;
            var entity = item.Id == 0 ? new CustomerContact { CreatedDate = now } : customer.CustomerContacts.Single(c => c.Id == item.Id);
            if (item.Deleted == true) { db.CustomerContacts.Remove(entity); continue; }
            entity.Name = item.Name; entity.Title = item.Title; entity.Email = item.Email; entity.PhoneNo = item.PhoneNo;
            entity.AlternatePhoneNo = item.AlternatePhoneNo; entity.AlternateEmail = item.AlternateEmail; entity.IsPrimary = item.IsPrimary;
            if (item.Id == 0) customer.CustomerContacts.Add(entity); else entity.LastUpdatedDate = now;
        }
        foreach (var item in input.CustomerAddresses)
        {
            if (item.Id == 0 && item.Deleted == true) continue;
            var entity = item.Id == 0 ? new CustomerAddress { CreatedDate = now } : customer.CustomerAddresses.Single(a => a.Id == item.Id);
            if (item.Deleted == true) { db.CustomerAddresses.Remove(entity); continue; }
            entity.AddressLine1 = item.AddressLine1; entity.AddressLine2 = item.AddressLine2; entity.CityMunicipalityId = item.CityMunicipalityId;
            entity.PostalCode = item.PostalCode; entity.IsDeliveryAddress = item.IsDeliveryAddress; entity.IsBillingAddress = item.IsBillingAddress;
            if (item.Id == 0) customer.CustomerAddresses.Add(entity); else entity.LastUpdatedDate = now;
        }
        if (!id.HasValue) db.Customers.Add(customer);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.Customers.AsNoTracking().AnyAsync(c => c.Id == customer.Id && c.UserConfigId == companyId, ct)) return (null, null, true);
            throw;
        }
        return (await GetAsync(companyId, customer.Id, ct), null, false);
    }

    public async Task<bool> DeleteAsync(int companyId, int id, CancellationToken ct)
    {
        var customer = await db.Customers.Where(c => c.Id == id && c.UserConfigId == companyId)
            .Include(c => c.CustomerAddresses).Include(c => c.CustomerContacts).AsSplitQuery().SingleOrDefaultAsync(ct);
        if (customer == null) return false;
        db.CustomerAddresses.RemoveRange(customer.CustomerAddresses);
        db.CustomerContacts.RemoveRange(customer.CustomerContacts);
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
