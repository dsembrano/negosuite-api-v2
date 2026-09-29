using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using System.ComponentModel.DataAnnotations;

namespace negosuite_api.Contracts.Suppliers;

public class SupplierListCriteria
{
    public int? UserConfigId { get; set; }
    public bool? ShowInactive { get; set; }
}

public class SupplierWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    [Required, StringLength(100)] public string Name { get; set; }
    [StringLength(50)] public string Tin { get; set; }
    public int? TaxRateId { get; set; }
    public int? PaymentTermId { get; set; }
    public string Notes { get; set; }
    public bool Status { get; set; }
    public List<SupplierAddressRequest> SupplierAddresses { get; set; } = new();
    public List<SupplierContactRequest> SupplierContacts { get; set; } = new();
}

public sealed class SupplierCreateRequest : SupplierWriteRequest { }
public sealed class SupplierUpdateRequest : SupplierWriteRequest { }

public class SupplierAddressRequest
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public int CityMunicipalityId { get; set; }
    [StringLength(20)] public string PostalCode { get; set; }
    public bool IsPrimaryAddress { get; set; }
    public bool? Deleted { get; set; }
}

public class SupplierContactRequest
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
    public bool? Deleted { get; set; }
}

public class SupplierListItemDto
{
    // Opt-in expansion preserves the nine-field legacy list response.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<SupplierAddressDto> SupplierAddresses { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<SupplierContactDto> SupplierContacts { get; set; }
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Tin { get; set; }
    public bool Status { get; set; }
    public int? TaxRateId { get; set; }
    public string TaxRateName { get; set; }
    public int? PaymentTermId { get; set; }
    public string PaymentTermName { get; set; }
}
