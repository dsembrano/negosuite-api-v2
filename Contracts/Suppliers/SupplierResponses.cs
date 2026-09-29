using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
namespace negosuite_api.Contracts.Suppliers;

public class SupplierDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Tin { get; set; }
    public int? TaxRateId { get; set; }
    public int? PaymentTermId { get; set; }
    public string Notes { get; set; }
    public bool Status { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public TaxRateDto TaxRate { get; set; }
    public PaymentTermDto PaymentTerm { get; set; }
    public ICollection<SupplierAddressDto> SupplierAddresses { get; set; } = new List<SupplierAddressDto>();
    public ICollection<SupplierContactDto> SupplierContacts { get; set; } = new List<SupplierContactDto>();
}
public class SupplierAddressDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public int CityMunicipalityId { get; set; }
    public string PostalCode { get; set; }
    public bool IsPrimaryAddress { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public CityMunicipalityDto CityMunicipality { get; set; }
    public bool? Deleted { get; set; }
}
public class SupplierContactDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
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
