using System;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Contracts.Maintenance;

public class MaintenanceWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    // Validated for creates/updates by the service; bulk deletions need only IDs.
    public string Name { get; set; }
    public bool? Deleted { get; set; }
}
public sealed class MaintenanceCriteria
{
    public int? UserConfigId { get; set; }
    public bool? ShowInactive { get; set; }
    public int? ResponsibilityCenterTypeId { get; set; }
}
public class TaxRateWriteRequest : MaintenanceWriteRequest
{
    public decimal Rate { get; set; }
    public int? TaxAccountId { get; set; }
    public int? SalesAccountId { get; set; }
    public string ApplyToSalesOrPurchase { get; set; }
}
public sealed class TaxRateCreateRequest : TaxRateWriteRequest { }
public sealed class TaxRateUpdateRequest : TaxRateWriteRequest { }
public sealed class TaxRateDetailDto
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
    public string ApplyToSalesOrPurchase { get; set; }
    public bool? Deleted { get; set; }
    public AccountDetailDto TaxAccount { get; set; }
    public AccountDetailDto SalesAccount { get; set; }
}
public class DiscountTypeWriteRequest : MaintenanceWriteRequest
{
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public int? DiscountAccountId { get; set; }
    public bool? DiscountIsBeforeTax { get; set; }
    public bool? LockedRate { get; set; }
    public int? TaxRateId { get; set; }
}
public sealed class DiscountTypeCreateRequest : DiscountTypeWriteRequest { }
public sealed class DiscountTypeUpdateRequest : DiscountTypeWriteRequest { }
public sealed class DiscountTypeDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public int? DiscountAccountId { get; set; }
    public bool? DiscountIsBeforeTax { get; set; }
    public bool? LockedRate { get; set; }
    public int? TaxRateId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
    public AccountDetailDto DiscountAccount { get; set; }
    public TaxRateDetailDto TaxRate { get; set; }
}
public class ResponsibilityCenterWriteRequest : MaintenanceWriteRequest
{
    public bool Status { get; set; }
    public int ResponsibilityCenterTypeId { get; set; }
    public string Notes { get; set; }
}
public sealed class ResponsibilityCenterCreateRequest : ResponsibilityCenterWriteRequest { }
public sealed class ResponsibilityCenterUpdateRequest : ResponsibilityCenterWriteRequest { }
public sealed class ResponsibilityCenterDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public bool Status { get; set; }
    public int ResponsibilityCenterTypeId { get; set; }
    public string Notes { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}
public class ResponsibilityCenterTypeWriteRequest : MaintenanceWriteRequest
{
    public bool IsActive { get; set; }
    public string RequiredBy { get; set; }
    public string RequiredByTags { get; set; }
}
public sealed class ResponsibilityCenterTypeCreateRequest : ResponsibilityCenterTypeWriteRequest { }
public sealed class ResponsibilityCenterTypeUpdateRequest : ResponsibilityCenterTypeWriteRequest { }
public sealed class ResponsibilityCenterTypeDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public string RequiredBy { get; set; }
    public string RequiredByTags { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
}
public class InventoryLocationWriteRequest : MaintenanceWriteRequest
{
    public string Code { get; set; }
    public bool Status { get; set; }
    public string Notes { get; set; }
}
public sealed class InventoryLocationCreateRequest : InventoryLocationWriteRequest { }
public sealed class InventoryLocationUpdateRequest : InventoryLocationWriteRequest { }
public sealed class InventoryLocationDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool Status { get; set; }
    public string Notes { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}
public sealed class InventoryLocationListDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public bool Status { get; set; }
}
