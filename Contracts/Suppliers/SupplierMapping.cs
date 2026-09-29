using negosuite_api.Models;
using negosuite_api.Contracts.Customers;
namespace negosuite_api.Contracts.Suppliers;

internal static class SupplierMapping
{
    public static SupplierDetailDto Map(Supplier value) => value == null ? null : new SupplierDetailDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Code = value.Code,
        Name = value.Name,
        Tin = value.Tin,
        TaxRateId = value.TaxRateId,
        PaymentTermId = value.PaymentTermId,
        Notes = value.Notes,
        Status = value.Status,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        TaxRate = CustomerMapping.Map(value.TaxRate),
        PaymentTerm = CustomerMapping.Map(value.PaymentTerm),
    };
    public static SupplierAddressDto Map(SupplierAddress value) => value == null ? null : new SupplierAddressDto
    {
        Id = value.Id,
        SupplierId = value.SupplierId,
        AddressLine1 = value.AddressLine1,
        AddressLine2 = value.AddressLine2,
        CityMunicipalityId = value.CityMunicipalityId,
        PostalCode = value.PostalCode,
        IsPrimaryAddress = value.IsPrimaryAddress,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        CityMunicipality = CustomerMapping.Map(value.CityMunicipality),
        Deleted = value.Deleted,
    };
    public static SupplierContactDto Map(SupplierContact value) => value == null ? null : new SupplierContactDto
    {
        Id = value.Id,
        SupplierId = value.SupplierId,
        Name = value.Name,
        Title = value.Title,
        Email = value.Email,
        PhoneNo = value.PhoneNo,
        AlternatePhoneNo = value.AlternatePhoneNo,
        AlternateEmail = value.AlternateEmail,
        IsPrimary = value.IsPrimary,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        Deleted = value.Deleted,
    };
}
