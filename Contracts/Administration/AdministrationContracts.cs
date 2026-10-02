using System;
using System.ComponentModel.DataAnnotations;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Contracts.Administration;

public class ConfigDetailDto
{
    public int Id { get; set; }
    public string Uuid { get; set; }
    public string CompanyName { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string PhoneNo { get; set; }
    public string FaxNo { get; set; }
    public string Email { get; set; }
    public string Website { get; set; }
    public string Tin { get; set; }
    public int? IndustryId { get; set; }
    public string CompanyAbout { get; set; }
    public int? CountryId { get; set; }
    public int? ARTradeAccountId { get; set; }
    public short ARAgingBaseDate { get; set; }
    public bool ARAgingShowCurrent { get; set; }
    public short ARAgingPeriod1 { get; set; }
    public short ARAgingPeriod2 { get; set; }
    public short ARAgingPeriod3 { get; set; }
    public short ARAgingPeriod4 { get; set; }
    public int? APTradeAccountId { get; set; }
    public short APAgingBaseDate { get; set; }
    public bool APAgingShowCurrent { get; set; }
    public short APAgingPeriod1 { get; set; }
    public short APAgingPeriod2 { get; set; }
    public short APAgingPeriod3 { get; set; }
    public short APAgingPeriod4 { get; set; }
    public int? DiscountAccountId { get; set; }
    public int? PurchaseDiscountAccountId { get; set; }
    public string DateFormat { get; set; }
    public string CompanyLogoURL { get; set; }
    public bool? InvoiceShowShippingAddress { get; set; }
    public string InvoiceMargin { get; set; }
    public string InvoiceLogoURL { get; set; }
    public string InvoiceLogoPosition { get; set; }
    public string InvoiceLogoStyleClass { get; set; }
    public string InvoiceLogoWidth { get; set; }
    public string InvoiceLogoHeight { get; set; }
    public string InvoiceTemplate { get; set; }
    public string SalesReceiptTemplate { get; set; }
    public string PaymentAdjustmentTypes { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public string PaymentVerifier { get; set; }
    public string PaymentVerifierPosition { get; set; }
    public string PaymentApprover { get; set; }
    public string PaymentApproverPosition { get; set; }
    public string PaymentVoucherTemplate { get; set; }
    public string JournalVoucherVerifier { get; set; }
    public string JournalVoucherVerifierPosition { get; set; }
    public string JournalVoucherApprover { get; set; }
    public string JournalVoucherApproverPosition { get; set; }
    public string JournalVoucherTemplate { get; set; }
    public string IncomeStatementConfig { get; set; }
    public bool ShowAccountCodeInList { get; set; }
    public byte RequireAccountCode { get; set; }
    public bool? IsTemplate { get; set; }
    public int? SubscriptionPlanId { get; set; }
    public DateTime? SubscriptionDate { get; set; }
    public bool? Trial { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public string BillingMode { get; set; }
    public byte? MaxUserCount { get; set; }
    public String LandedCostItemsJson { get; set; }
    public String PaymentModes { get; set; }
    public String AutoReferenceNoConfig { get; set; }
    public bool? MetabaseDashboard { get; set; }
    public bool? PointOfSales { get; set; }
    public string TaxRatesJson { get; set; }
    public AdminIndustryDto Industry { get; set; }
    public AdminCountryDto Country { get; set; }
    public AccountDetailDto ARTradeAccount { get; set; }
    public AccountDetailDto APTradeAccount { get; set; }
    public AccountDetailDto DiscountAccount { get; set; }
    public AccountDetailDto PurchaseDiscountAccount { get; set; }
    public object[] TaxRates { get; set; } = Array.Empty<object>();
}

public class UserRoleDetailDto
{
    public int Id { get; set; }
    public int? UserConfigId { get; set; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public bool IsAdmin { get; set; }
    public string Permission { get; set; }
    public string AdvancePermission { get; set; }
    public string ColumnRestriction { get; set; }
    public string MobileAppPermission { get; set; }
    public bool? EnableAIChatBot { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class UserDetailDto
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string MobileNo { get; set; }
    public string Name { get; set; }
    public int? UserTypeId { get; set; }
    public int? UserRoleId { get; set; }
    public string Avatar { get; set; }
    public bool Status { get; set; }
    public int? ConfigId { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public string UserUIConfig { get; set; }
    public string Identifier { get; set; }
    public UserRoleDetailDto UserRole { get; set; }
    public AdminUserTypeDto UserType { get; set; }
    public ConfigDetailDto Config { get; set; }
}

public class AdminIndustryDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class AdminCountryDto
{
    public object[] StateProvinces { get; set; } = Array.Empty<object>();
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class AdminUserTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }

}

public class AdminCurrencyDto
{
    public short Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsBase { get; set; }
    public string AltCode { get; set; }

}

public class ConfigWriteRequest
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string CompanyName { get; set; }
    public string Address1 { get; set; }
    public string Address2 { get; set; }
    public string PhoneNo { get; set; }
    public string FaxNo { get; set; }
    public string Email { get; set; }
    public string Website { get; set; }
    public string Tin { get; set; }
    public int? IndustryId { get; set; }
    public string CompanyAbout { get; set; }
    public int? CountryId { get; set; }
    public int? ARTradeAccountId { get; set; }
    public short ARAgingBaseDate { get; set; }
    public bool ARAgingShowCurrent { get; set; }
    public short ARAgingPeriod1 { get; set; }
    public short ARAgingPeriod2 { get; set; }
    public short ARAgingPeriod3 { get; set; }
    public short ARAgingPeriod4 { get; set; }
    public int? APTradeAccountId { get; set; }
    public short APAgingBaseDate { get; set; }
    public bool APAgingShowCurrent { get; set; }
    public short APAgingPeriod1 { get; set; }
    public short APAgingPeriod2 { get; set; }
    public short APAgingPeriod3 { get; set; }
    public short APAgingPeriod4 { get; set; }
    public int? DiscountAccountId { get; set; }
    public int? PurchaseDiscountAccountId { get; set; }
    public string DateFormat { get; set; }
    public string CompanyLogoURL { get; set; }
    public bool? InvoiceShowShippingAddress { get; set; }
    public string InvoiceMargin { get; set; }
    public string InvoiceLogoURL { get; set; }
    public string InvoiceLogoPosition { get; set; }
    public string InvoiceLogoStyleClass { get; set; }
    public string InvoiceLogoWidth { get; set; }
    public string InvoiceLogoHeight { get; set; }
    public string InvoiceTemplate { get; set; }
    public string SalesReceiptTemplate { get; set; }
    public string PaymentAdjustmentTypes { get; set; }
    public string PaymentVerifier { get; set; }
    public string PaymentVerifierPosition { get; set; }
    public string PaymentApprover { get; set; }
    public string PaymentApproverPosition { get; set; }
    public string PaymentVoucherTemplate { get; set; }
    public string JournalVoucherVerifier { get; set; }
    public string JournalVoucherVerifierPosition { get; set; }
    public string JournalVoucherApprover { get; set; }
    public string JournalVoucherApproverPosition { get; set; }
    public string JournalVoucherTemplate { get; set; }
    public string IncomeStatementConfig { get; set; }
    public bool ShowAccountCodeInList { get; set; }
    public byte RequireAccountCode { get; set; }
    public String LandedCostItemsJson { get; set; }
    public String PaymentModes { get; set; }
    public String AutoReferenceNoConfig { get; set; }
}
public sealed class ConfigUpdateRequest : ConfigWriteRequest { }
public class CompanySetupRequest
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string CompanyName { get; set; }
    public string Address1 { get; set; }
    public string PhoneNo { get; set; }
    public string Email { get; set; }
    public string Website { get; set; }
    public string Tin { get; set; }
    public int? IndustryId { get; set; }
    public string CompanyAbout { get; set; }
    public int? CountryId { get; set; }
    public int? SubscriptionPlanId { get; set; }
    public bool? Trial { get; set; }
    public string BillingMode { get; set; }
    public string TaxRatesJson { get; set; }
}
public sealed class ConfigCreateRequest : CompanySetupRequest { }
public class UserRoleWriteRequest
{
    public int Id { get; set; }
    public int? UserConfigId { get; set; }
    [Required, StringLength(50)] public string Name { get; set; }
    public string Notes { get; set; }
    public bool IsAdmin { get; set; }
    public string Permission { get; set; }
    public string AdvancePermission { get; set; }
    public string ColumnRestriction { get; set; }
    public string MobileAppPermission { get; set; }
    public bool? EnableAIChatBot { get; set; }
}
public sealed class UserRoleCreateRequest : UserRoleWriteRequest { }
public sealed class UserRoleUpdateRequest : UserRoleWriteRequest { }
public sealed class UserCreateRequest
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; }
    [StringLength(250)] public string Password { get; set; }
    [Required] public string Identifier { get; set; }
    public int? ConfigId { get; set; }
    public int? UserRoleId { get; set; }
}
public sealed class UserUpdateRequest
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; }
    public int? UserRoleId { get; set; }
}
public sealed class UserUIConfigRequest { public string UserUIConfigString { get; set; } }
public sealed class AdministrationCriteria { public int? UserConfigId { get; set; } }
public sealed class AdministrationListOptions
{
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public string Search { get; set; }
    public string SortBy { get; set; }
    public string SortDirection { get; set; }
    public bool? Status { get; set; }
}
public sealed class UserListDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string MobileNo { get; set; }
    public int? UserTypeId { get; set; }
    public string UserTypeName { get; set; }
    public string Avatar { get; set; }
    public bool Status { get; set; }
    public int? UserRoleId { get; set; }
    public string UserRoleName { get; set; }
    public UserRoleDetailDto UserRole { get; set; }
    public string UserUIConfig { get; set; }
}
public sealed class UserRoleListDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public string Permission { get; set; }
    public bool IsAdmin { get; set; }
}
public sealed class ConfigTemplateDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string About { get; set; }
    public int? DiscountAccountId { get; set; }
    public int? PurchaseDiscountAccountId { get; set; }
}
public sealed class CompanySetupResponse
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string MobileNo { get; set; }
    public int? UserTypeId { get; set; }
    public string UserTypeName { get; set; }
    public string Avatar { get; set; }
    public bool Status { get; set; }
    public int? ConfigId { get; set; }
    public ConfigDetailDto Config { get; set; }
    public AdminCurrencyDto BaseCurrency { get; set; }
    public int? UserRoleId { get; set; }
    public UserRoleDetailDto UserRole { get; set; }
}
