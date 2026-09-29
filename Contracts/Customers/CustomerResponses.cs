using System;
using System.Collections.Generic;
namespace negosuite_api.Contracts.Customers;

public class CustomerDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal? CreditLimit { get; set; }
    public string Tin { get; set; }
    public int? TaxRateId { get; set; }
    public int? PaymentTermId { get; set; }
    public string Notes { get; set; }
    public string BusinessStyle { get; set; }
    public bool Status { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public TaxRateDto TaxRate { get; set; }
    public PaymentTermDto PaymentTerm { get; set; }
    public ICollection<CustomerAddressDto> CustomerAddresses { get; set; } = new List<CustomerAddressDto>();
    public ICollection<CustomerContactDto> CustomerContacts { get; set; } = new List<CustomerContactDto>();
}
public class CustomerAddressDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public int CityMunicipalityId { get; set; }
    public string PostalCode { get; set; }
    public bool IsDeliveryAddress { get; set; }
    public bool IsBillingAddress { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public CityMunicipalityDto CityMunicipality { get; set; }
    public bool? Deleted { get; set; }
}
public class CustomerContactDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Email { get; set; }
    public string PhoneNo { get; set; }
    public string AlternatePhoneNo { get; set; }
    public string AlternateEmail { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
}
public class CityMunicipalityDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int StateProvinceId { get; set; }
    public string PostalCode { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public StateProvinceDto StateProvince { get; set; }
}
public class StateProvinceDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Capital { get; set; }
    public int CountryId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public object Country { get; set; }
}
public class TaxRateDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public decimal Rate { get; set; }
    public int? TaxAccountId { get; set; }
    public int? SalesAccountId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public object TaxAccount { get; set; }
    public object SalesAccount { get; set; }
    public string ApplyToSalesOrPurchase { get; set; }
    public bool? Deleted { get; set; }
}
public class PaymentTermDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public short? Days { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}

