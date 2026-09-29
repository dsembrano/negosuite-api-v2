using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace negosuite_api.Contracts.Customers;

public class CustomerListCriteria
{
    public int? UserConfigId { get; set; }
    public bool? ShowInactive { get; set; }
}

public class CustomerWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    [StringLength(50)] public string Tin { get; set; }
    public decimal? CreditLimit { get; set; }
    public int? TaxRateId { get; set; }
    public int? PaymentTermId { get; set; }
    public string Notes { get; set; }
    public string BusinessStyle { get; set; }
    public bool Status { get; set; }
    public List<CustomerAddressRequest> CustomerAddresses { get; set; } = new();
    public List<CustomerContactRequest> CustomerContacts { get; set; } = new();
}

public sealed class CustomerCreateRequest : CustomerWriteRequest { }
public sealed class CustomerUpdateRequest : CustomerWriteRequest { }

public class CustomerAddressRequest
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }
    public int CityMunicipalityId { get; set; }
    [StringLength(20)] public string PostalCode { get; set; }
    public bool IsDeliveryAddress { get; set; }
    public bool IsBillingAddress { get; set; }
    public bool? Deleted { get; set; }
}

public class CustomerContactRequest
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
    public bool? Deleted { get; set; }
}

public class CustomerListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Tin { get; set; }
    public bool Status { get; set; }
    public int? TaxRateId { get; set; }
    public string TaxRateName { get; set; }
    public int? PaymentTermId { get; set; }
    public string PaymentTermName { get; set; }
    public short? PaymentTermDays { get; set; }
    public string PaymentTermCode { get; set; }
    public decimal? CreditLimit { get; set; }
    public List<string> StringCustomerAddresses { get; set; } = new();
    public List<CustomerAddressDto> CustomerAddresses { get; set; } = new();
    public List<CustomerContactDto> CustomerContacts { get; set; } = new();
}

public record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public static class CustomerPagination
{
    public static bool IsValid(int? pageNumber, int? pageSize) =>
        (!pageNumber.HasValue && !pageSize.HasValue) ||
        (pageNumber.HasValue && pageSize.HasValue && pageNumber > 0 && pageSize >= 1 && pageSize <= 200 &&
         ((long)pageNumber.Value - 1) * pageSize.Value <= int.MaxValue);
}
