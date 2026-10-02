using negosuite_api.Models;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Contracts.Administration;

public static class AdministrationMapping
{
    public static ConfigDetailDto ToDto(Config value) => value == null ? null : new()
    {
        Id = value.Id,
        Uuid = value.Uuid,
        CompanyName = value.CompanyName,
        Address1 = value.Address1,
        Address2 = value.Address2,
        PhoneNo = value.PhoneNo,
        FaxNo = value.FaxNo,
        Email = value.Email,
        Website = value.Website,
        Tin = value.Tin,
        IndustryId = value.IndustryId,
        CompanyAbout = value.CompanyAbout,
        CountryId = value.CountryId,
        ARTradeAccountId = value.ARTradeAccountId,
        ARAgingBaseDate = value.ARAgingBaseDate,
        ARAgingShowCurrent = value.ARAgingShowCurrent,
        ARAgingPeriod1 = value.ARAgingPeriod1,
        ARAgingPeriod2 = value.ARAgingPeriod2,
        ARAgingPeriod3 = value.ARAgingPeriod3,
        ARAgingPeriod4 = value.ARAgingPeriod4,
        APTradeAccountId = value.APTradeAccountId,
        APAgingBaseDate = value.APAgingBaseDate,
        APAgingShowCurrent = value.APAgingShowCurrent,
        APAgingPeriod1 = value.APAgingPeriod1,
        APAgingPeriod2 = value.APAgingPeriod2,
        APAgingPeriod3 = value.APAgingPeriod3,
        APAgingPeriod4 = value.APAgingPeriod4,
        DiscountAccountId = value.DiscountAccountId,
        PurchaseDiscountAccountId = value.PurchaseDiscountAccountId,
        DateFormat = value.DateFormat,
        CompanyLogoURL = value.CompanyLogoURL,
        InvoiceShowShippingAddress = value.InvoiceShowShippingAddress,
        InvoiceMargin = value.InvoiceMargin,
        InvoiceLogoURL = value.InvoiceLogoURL,
        InvoiceLogoPosition = value.InvoiceLogoPosition,
        InvoiceLogoStyleClass = value.InvoiceLogoStyleClass,
        InvoiceLogoWidth = value.InvoiceLogoWidth,
        InvoiceLogoHeight = value.InvoiceLogoHeight,
        InvoiceTemplate = value.InvoiceTemplate,
        SalesReceiptTemplate = value.SalesReceiptTemplate,
        PaymentAdjustmentTypes = value.PaymentAdjustmentTypes,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        PaymentVerifier = value.PaymentVerifier,
        PaymentVerifierPosition = value.PaymentVerifierPosition,
        PaymentApprover = value.PaymentApprover,
        PaymentApproverPosition = value.PaymentApproverPosition,
        PaymentVoucherTemplate = value.PaymentVoucherTemplate,
        JournalVoucherVerifier = value.JournalVoucherVerifier,
        JournalVoucherVerifierPosition = value.JournalVoucherVerifierPosition,
        JournalVoucherApprover = value.JournalVoucherApprover,
        JournalVoucherApproverPosition = value.JournalVoucherApproverPosition,
        JournalVoucherTemplate = value.JournalVoucherTemplate,
        IncomeStatementConfig = value.IncomeStatementConfig,
        ShowAccountCodeInList = value.ShowAccountCodeInList,
        RequireAccountCode = value.RequireAccountCode,
        IsTemplate = value.IsTemplate,
        SubscriptionPlanId = value.SubscriptionPlanId,
        SubscriptionDate = value.SubscriptionDate,
        Trial = value.Trial,
        TrialEndDate = value.TrialEndDate,
        BillingMode = value.BillingMode,
        MaxUserCount = value.MaxUserCount,
        LandedCostItemsJson = value.LandedCostItemsJson,
        PaymentModes = value.PaymentModes,
        AutoReferenceNoConfig = value.AutoReferenceNoConfig,
        MetabaseDashboard = value.MetabaseDashboard,
        PointOfSales = value.PointOfSales,
        TaxRatesJson = value.TaxRatesJson,
        Industry = ToDto(value.Industry),
        Country = ToDto(value.Country),
        ARTradeAccount = AccountMapping.ToDto(value.ARTradeAccount),
        APTradeAccount = AccountMapping.ToDto(value.APTradeAccount),
        DiscountAccount = AccountMapping.ToDto(value.DiscountAccount),
        PurchaseDiscountAccount = AccountMapping.ToDto(value.PurchaseDiscountAccount)
    };
    public static UserRoleDetailDto ToDto(UserRole value) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Notes = value.Notes,
        IsAdmin = value.IsAdmin,
        Permission = value.Permission,
        AdvancePermission = value.AdvancePermission,
        ColumnRestriction = value.ColumnRestriction,
        MobileAppPermission = value.MobileAppPermission,
        EnableAIChatBot = value.EnableAIChatBot,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };
    public static UserDetailDto ToDto(User value) => value == null ? null : new()
    {
        Id = value.Id,
        Username = value.Username,
        Email = value.Email,
        MobileNo = value.MobileNo,
        Name = value.Name,
        UserTypeId = value.UserTypeId,
        UserRoleId = value.UserRoleId,
        Avatar = value.Avatar,
        Status = value.Status,
        ConfigId = value.ConfigId,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        UserUIConfig = value.UserUIConfig,
        Identifier = value.Identifier,
        UserRole = ToDto(value.UserRole), UserType = ToDto(value.UserType), Config = ToDto(value.Config)
    };
    public static AdminCountryDto ToDto(Country value) => value == null ? null : new()
    {
        Id = value.Id,
        Code = value.Code,
        Name = value.Name,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };
    public static AdminIndustryDto ToDto(Industry value) => value == null ? null : new()
    {
        Id = value.Id,
        Name = value.Name,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };
    public static AdminUserTypeDto ToDto(UserType value) => value == null ? null : new()
    {
        Id = value.Id,
        Name = value.Name,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };
    public static AdminCurrencyDto ToDto(Currency value) => value == null ? null : new()
    {
        Id = value.Id,
        Code = value.Code,
        Name = value.Name,
        ExchangeRate = value.ExchangeRate,
        IsBase = value.IsBase,
        AltCode = value.AltCode
    };
    public static void Apply(ConfigWriteRequest input, Config value)
    {
        value.CompanyName = input.CompanyName;
        value.Address1 = input.Address1;
        value.Address2 = input.Address2;
        value.PhoneNo = input.PhoneNo;
        value.FaxNo = input.FaxNo;
        value.Email = input.Email;
        value.Website = input.Website;
        value.Tin = input.Tin;
        value.IndustryId = input.IndustryId;
        value.CompanyAbout = input.CompanyAbout;
        value.CountryId = input.CountryId;
        value.ARTradeAccountId = input.ARTradeAccountId;
        value.ARAgingBaseDate = input.ARAgingBaseDate;
        value.ARAgingShowCurrent = input.ARAgingShowCurrent;
        value.ARAgingPeriod1 = input.ARAgingPeriod1;
        value.ARAgingPeriod2 = input.ARAgingPeriod2;
        value.ARAgingPeriod3 = input.ARAgingPeriod3;
        value.ARAgingPeriod4 = input.ARAgingPeriod4;
        value.APTradeAccountId = input.APTradeAccountId;
        value.APAgingBaseDate = input.APAgingBaseDate;
        value.APAgingShowCurrent = input.APAgingShowCurrent;
        value.APAgingPeriod1 = input.APAgingPeriod1;
        value.APAgingPeriod2 = input.APAgingPeriod2;
        value.APAgingPeriod3 = input.APAgingPeriod3;
        value.APAgingPeriod4 = input.APAgingPeriod4;
        value.DiscountAccountId = input.DiscountAccountId;
        value.PurchaseDiscountAccountId = input.PurchaseDiscountAccountId;
        value.DateFormat = input.DateFormat;
        value.CompanyLogoURL = input.CompanyLogoURL;
        value.InvoiceShowShippingAddress = input.InvoiceShowShippingAddress;
        value.InvoiceMargin = input.InvoiceMargin;
        value.InvoiceLogoURL = input.InvoiceLogoURL;
        value.InvoiceLogoPosition = input.InvoiceLogoPosition;
        value.InvoiceLogoStyleClass = input.InvoiceLogoStyleClass;
        value.InvoiceLogoWidth = input.InvoiceLogoWidth;
        value.InvoiceLogoHeight = input.InvoiceLogoHeight;
        value.InvoiceTemplate = input.InvoiceTemplate;
        value.SalesReceiptTemplate = input.SalesReceiptTemplate;
        value.PaymentAdjustmentTypes = input.PaymentAdjustmentTypes;
        value.PaymentVerifier = input.PaymentVerifier;
        value.PaymentVerifierPosition = input.PaymentVerifierPosition;
        value.PaymentApprover = input.PaymentApprover;
        value.PaymentApproverPosition = input.PaymentApproverPosition;
        value.PaymentVoucherTemplate = input.PaymentVoucherTemplate;
        value.JournalVoucherVerifier = input.JournalVoucherVerifier;
        value.JournalVoucherVerifierPosition = input.JournalVoucherVerifierPosition;
        value.JournalVoucherApprover = input.JournalVoucherApprover;
        value.JournalVoucherApproverPosition = input.JournalVoucherApproverPosition;
        value.JournalVoucherTemplate = input.JournalVoucherTemplate;
        value.IncomeStatementConfig = input.IncomeStatementConfig;
        value.ShowAccountCodeInList = input.ShowAccountCodeInList;
        value.RequireAccountCode = input.RequireAccountCode;
        value.LandedCostItemsJson = input.LandedCostItemsJson;
        value.PaymentModes = input.PaymentModes;
        value.AutoReferenceNoConfig = input.AutoReferenceNoConfig;
    }
    public static void Apply(UserRoleWriteRequest input, UserRole value)
    {
        value.Name = input.Name;
        value.Notes = input.Notes;
        value.IsAdmin = input.IsAdmin;
        value.Permission = input.Permission;
        value.AdvancePermission = input.AdvancePermission;
        value.ColumnRestriction = input.ColumnRestriction;
        value.MobileAppPermission = input.MobileAppPermission;
        value.EnableAIChatBot = input.EnableAIChatBot;
    }
}
