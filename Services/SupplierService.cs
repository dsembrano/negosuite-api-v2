using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;

namespace negosuite_api.Services;

public sealed class SupplierService
{
    private readonly negosuiteContext db;
    public SupplierService(negosuiteContext db) => this.db = db;

    public async Task<object> ListAsync(int companyId, bool showInactive, int? pageNumber, int? pageSize, CancellationToken ct, string search = null, string sortBy = null, string sortDirection = null)
    {
        var query = db.Suppliers.AsNoTracking().Where(c => c.UserConfigId == companyId);
        if (!showInactive) query = query.Where(c => c.Status);
        query = SupplierSearch.Apply(query, search);
        var total = pageNumber.HasValue ? await query.CountAsync(ct) : 0;
        query = SupplierSorting.Apply(query, sortBy, sortDirection);
        if (pageNumber.HasValue) query = query.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value);
        var items = await query.Select(c => new SupplierListItemDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            Tin = c.Tin,
            Status = c.Status,
            TaxRateId = c.TaxRateId,
            TaxRateName = c.TaxRate.Name,
            PaymentTermId = c.PaymentTermId,
            PaymentTermName = c.PaymentTerm.Name
        }).ToListAsync(ct);
        return pageNumber.HasValue ? new PagedResult<SupplierListItemDto>(items, pageNumber.Value, pageSize.Value, total) : items;
    }

    public async Task<SupplierDetailDto> GetAsync(int companyId, int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking().Where(c => c.Id == id && c.UserConfigId == companyId)
            .Include(c => c.TaxRate).Include(c => c.PaymentTerm).SingleOrDefaultAsync(ct);
        if (supplier == null) return null;
        var result = SupplierMapping.Map(supplier);
        result.SupplierAddresses = (await db.SupplierAddresses.AsNoTracking().Where(a => a.SupplierId == id)
            .Include(a => a.CityMunicipality).ThenInclude(c => c.StateProvince).OrderBy(a => a.Id).ToListAsync(ct)).Select(SupplierMapping.Map).ToList();
        result.SupplierContacts = (await db.SupplierContacts.AsNoTracking().Where(c => c.SupplierId == id).OrderBy(c => c.Id).ToListAsync(ct)).Select(SupplierMapping.Map).ToList();
        return result;
    }

    // Return validation errors before mutating the tracked aggregate.
    private async Task<string> ValidateAsync(int companyId, SupplierWriteRequest input, Supplier supplier, CancellationToken ct)
    {
        if (input.UserConfigId != companyId) return "Supplier company does not match configUuid.";
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100) return "Supplier name is required and must not exceed 100 characters.";
        if (input.Tin?.Length > 50) return "TIN must not exceed 50 characters.";
        input.SupplierContacts ??= new();
        input.SupplierAddresses ??= new();
        if (input.SupplierContacts.Any(c => c == null) || input.SupplierAddresses.Any(a => a == null)) return "Child records cannot be null.";
        var contacts = input.SupplierContacts;
        var addresses = input.SupplierAddresses;
        if (contacts.Any(c => c.Id < 0 || (c.SupplierId != 0 && c.SupplierId != supplier.Id)) ||
            addresses.Any(a => a.Id < 0 || (a.SupplierId != 0 && a.SupplierId != supplier.Id))) return "Child record belongs to another supplier.";
        if (contacts.Where(c => c.Id != 0).GroupBy(c => c.Id).Any(g => g.Count() > 1) ||
            addresses.Where(a => a.Id != 0).GroupBy(a => a.Id).Any(g => g.Count() > 1)) return "Duplicate child record ID.";
        if (contacts.Any(c => c.Id != 0 && !supplier.SupplierContacts.Any(x => x.Id == c.Id)) ||
            addresses.Any(a => a.Id != 0 && !supplier.SupplierAddresses.Any(x => x.Id == a.Id))) return "Child record does not belong to this supplier.";
        if (addresses.Any(a => a.PostalCode?.Length > 20)) return "Postal code must not exceed 20 characters.";
        if (input.TaxRateId.HasValue && !await db.TaxRates.AnyAsync(t => t.Id == input.TaxRateId && t.UserConfigId == companyId, ct)) return "Invalid tax rate for this company.";
        if (input.PaymentTermId.HasValue && !await db.PaymentTerms.AnyAsync(t => t.Id == input.PaymentTermId, ct)) return "Invalid payment term.";
        var cities = addresses.Where(a => a.Deleted != true).Select(a => a.CityMunicipalityId).Distinct().ToArray();
        if (cities.Length != await db.CityMunicipalities.CountAsync(c => cities.Contains(c.Id), ct)) return "Invalid city or municipality.";
        return null;
    }

    public async Task<(SupplierDetailDto Supplier, string Error, bool Missing)> SaveAsync(int companyId, int? id, SupplierWriteRequest input, CancellationToken ct)
    {
        Supplier supplier;
        if (id.HasValue)
        {
            supplier = await db.Suppliers.Where(c => c.Id == id && c.UserConfigId == companyId)
                .Include(c => c.SupplierAddresses).Include(c => c.SupplierContacts).AsSplitQuery().SingleOrDefaultAsync(ct);
            if (supplier == null) return (null, null, true);
        }
        else supplier = new Supplier { UserConfigId = companyId, CreatedDate = DateTime.Now };
        var error = await ValidateAsync(companyId, input, supplier, ct);
        if (error != null) return (null, error, false);
        supplier.Code = input.Code; supplier.Name = input.Name; supplier.Tin = input.Tin;
        supplier.TaxRateId = input.TaxRateId; supplier.PaymentTermId = input.PaymentTermId;
        supplier.Notes = input.Notes; supplier.Status = input.Status;
        var now = DateTime.Now;
        if (id.HasValue) supplier.LastUpdatedDate = now;
        foreach (var item in input.SupplierContacts)
        {
            if (item.Id == 0 && item.Deleted == true) continue;
            var entity = item.Id == 0 ? new SupplierContact { CreatedDate = now } : supplier.SupplierContacts.Single(c => c.Id == item.Id);
            if (item.Deleted == true) { db.SupplierContacts.Remove(entity); continue; }
            entity.Name = item.Name; entity.Title = item.Title; entity.Email = item.Email; entity.PhoneNo = item.PhoneNo;
            entity.AlternatePhoneNo = item.AlternatePhoneNo; entity.AlternateEmail = item.AlternateEmail; entity.IsPrimary = item.IsPrimary;
            if (item.Id == 0) supplier.SupplierContacts.Add(entity); else entity.LastUpdatedDate = now;
        }
        foreach (var item in input.SupplierAddresses)
        {
            if (item.Id == 0 && item.Deleted == true) continue;
            var entity = item.Id == 0 ? new SupplierAddress { CreatedDate = now } : supplier.SupplierAddresses.Single(a => a.Id == item.Id);
            if (item.Deleted == true) { db.SupplierAddresses.Remove(entity); continue; }
            entity.AddressLine1 = item.AddressLine1; entity.AddressLine2 = item.AddressLine2; entity.CityMunicipalityId = item.CityMunicipalityId;
            entity.PostalCode = item.PostalCode; entity.IsPrimaryAddress = item.IsPrimaryAddress;
            if (item.Id == 0) supplier.SupplierAddresses.Add(entity); else entity.LastUpdatedDate = now;
        }
        if (!id.HasValue) db.Suppliers.Add(supplier);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            if (!await db.Suppliers.AsNoTracking().AnyAsync(c => c.Id == supplier.Id && c.UserConfigId == companyId, ct)) return (null, null, true);
            throw;
        }
        return (await GetAsync(companyId, supplier.Id, ct), null, false);
    }

    public async Task<bool> DeleteAsync(int companyId, int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.Where(c => c.Id == id && c.UserConfigId == companyId)
            .Include(c => c.SupplierAddresses).Include(c => c.SupplierContacts).AsSplitQuery().SingleOrDefaultAsync(ct);
        if (supplier == null) return false;
        db.SupplierAddresses.RemoveRange(supplier.SupplierAddresses);
        db.SupplierContacts.RemoveRange(supplier.SupplierContacts);
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
