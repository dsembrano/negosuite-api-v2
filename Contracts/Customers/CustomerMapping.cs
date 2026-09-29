using negosuite_api.Models;
namespace negosuite_api.Contracts.Customers;

internal static class CustomerMapping
{
    public static CustomerDetailDto Map(Customer value) => value == null ? null : new CustomerDetailDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Code = value.Code,
        Name = value.Name,
        CreditLimit = value.CreditLimit,
        Tin = value.Tin,
        TaxRateId = value.TaxRateId,
        PaymentTermId = value.PaymentTermId,
        Notes = value.Notes,
        BusinessStyle = value.BusinessStyle,
        Status = value.Status,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        TaxRate = Map(value.TaxRate),
        PaymentTerm = Map(value.PaymentTerm),
    };
    public static CustomerAddressDto Map(CustomerAddress value) => value == null ? null : new CustomerAddressDto
    {
        Id = value.Id,
        CustomerId = value.CustomerId,
        AddressLine1 = value.AddressLine1,
        AddressLine2 = value.AddressLine2,
        CityMunicipalityId = value.CityMunicipalityId,
        PostalCode = value.PostalCode,
        IsDeliveryAddress = value.IsDeliveryAddress,
        IsBillingAddress = value.IsBillingAddress,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        CityMunicipality = Map(value.CityMunicipality),
        Deleted = value.Deleted,
    };
    public static CustomerContactDto Map(CustomerContact value) => value == null ? null : new CustomerContactDto
    {
        Id = value.Id,
        CustomerId = value.CustomerId,
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
    public static CityMunicipalityDto Map(CityMunicipality value) => value == null ? null : new CityMunicipalityDto
    {
        Id = value.Id,
        Name = value.Name,
        StateProvinceId = value.StateProvinceId,
        PostalCode = value.PostalCode,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        StateProvince = Map(value.StateProvince),
    };
    public static StateProvinceDto Map(StateProvince value) => value == null ? null : new StateProvinceDto
    {
        Id = value.Id,
        Name = value.Name,
        Capital = value.Capital,
        CountryId = value.CountryId,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
    };
    public static TaxRateDto Map(TaxRate value) => value == null ? null : new TaxRateDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Rate = value.Rate,
        TaxAccountId = value.TaxAccountId,
        SalesAccountId = value.SalesAccountId,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        ApplyToSalesOrPurchase = value.ApplyToSalesOrPurchase,
        Deleted = value.Deleted,
    };
    public static PaymentTermDto Map(PaymentTerm value) => value == null ? null : new PaymentTermDto
    {
        Id = value.Id,
        Code = value.Code,
        Name = value.Name,
        Days = value.Days,
        IsActive = value.IsActive,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
    };
}

