using negosuite_api.Models;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Contracts.Maintenance;

public static class MaintenanceMapping
{
    public static TaxRateDetailDto ToDto(TaxRate value) => value == null ? null : new()
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
        TaxAccount = value.TaxAccount != null && value.TaxAccount.UserConfigId == value.UserConfigId ? AccountMapping.ToDto(value.TaxAccount) : null,
        SalesAccount = value.SalesAccount != null && value.SalesAccount.UserConfigId == value.UserConfigId ? AccountMapping.ToDto(value.SalesAccount) : null
    };
    public static void Apply(TaxRateWriteRequest input, TaxRate value)
    {
        value.Name = input.Name;
        value.Rate = input.Rate;
        value.TaxAccountId = input.TaxAccountId;
        value.SalesAccountId = input.SalesAccountId;
        value.ApplyToSalesOrPurchase = input.ApplyToSalesOrPurchase;
    }
    public static DiscountTypeDetailDto ToDto(DiscountType value) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Rate = value.Rate,
        DiscountAmount = value.DiscountAmount,
        DiscountAccountId = value.DiscountAccountId,
        DiscountIsBeforeTax = value.DiscountIsBeforeTax,
        LockedRate = value.LockedRate,
        TaxRateId = value.TaxRateId,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        Deleted = value.Deleted,
        DiscountAccount = value.DiscountAccount != null && value.DiscountAccount.UserConfigId == value.UserConfigId ? AccountMapping.ToDto(value.DiscountAccount) : null,
        TaxRate = value.TaxRate != null && value.TaxRate.UserConfigId == value.UserConfigId ? ToDto(value.TaxRate) : null
    };
    public static void Apply(DiscountTypeWriteRequest input, DiscountType value)
    {
        value.Name = input.Name;
        value.Rate = input.Rate;
        value.DiscountAmount = input.DiscountAmount;
        value.DiscountAccountId = input.DiscountAccountId;
        value.DiscountIsBeforeTax = input.DiscountIsBeforeTax;
        value.LockedRate = input.LockedRate;
        value.TaxRateId = input.TaxRateId;
    }
    public static ResponsibilityCenterDetailDto ToDto(ResponsibilityCenter value) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Status = value.Status,
        ResponsibilityCenterTypeId = value.ResponsibilityCenterTypeId,
        Notes = value.Notes,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };
    public static void Apply(ResponsibilityCenterWriteRequest input, ResponsibilityCenter value)
    {
        value.Name = input.Name;
        value.Status = input.Status;
        value.ResponsibilityCenterTypeId = input.ResponsibilityCenterTypeId;
        value.Notes = input.Notes;
    }
    public static ResponsibilityCenterTypeDetailDto ToDto(ResponsibilityCenterType value) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        IsActive = value.IsActive,
        RequiredBy = value.RequiredBy,
        RequiredByTags = value.RequiredByTags,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        Deleted = value.Deleted
    };
    public static void Apply(ResponsibilityCenterTypeWriteRequest input, ResponsibilityCenterType value)
    {
        value.Name = input.Name;
        value.IsActive = input.IsActive;
        value.RequiredBy = input.RequiredBy;
        value.RequiredByTags = input.RequiredByTags;
    }
    public static InventoryLocationDetailDto ToDto(InventoryLocation value) => value == null ? null : new()
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Code = value.Code,
        Name = value.Name,
        Status = value.Status,
        Notes = value.Notes,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId
    };
    public static void Apply(InventoryLocationWriteRequest input, InventoryLocation value)
    {
        value.Code = input.Code;
        value.Name = input.Name;
        value.Status = input.Status;
        value.Notes = input.Notes;
    }
}
