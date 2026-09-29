using negosuite_api.Models;
namespace negosuite_api.Contracts.Items;

internal static class ItemMapping
{
    public static ItemDetailDto Map(Item value) => value == null ? null : new ItemDetailDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Code = value.Code,
        Name = value.Name,
        Type = value.Type,
        Unit = value.Unit,
        ItemCategoryId = value.ItemCategoryId,
        ToPurchase = value.ToPurchase,
        Cost = value.Cost,
        AverageCost = value.AverageCost,
        PurchaseAccountId = value.PurchaseAccountId,
        PurchaseTaxRateId = value.PurchaseTaxRateId,
        ToSell = value.ToSell,
        Rate = value.Rate,
        SalesAccountId = value.SalesAccountId,
        SalesTaxRateId = value.SalesTaxRateId,
        Notes = value.Notes,
        TrackInventory = value.TrackInventory,
        InventoryAccountId = value.InventoryAccountId,
        OpeningQuantity = value.OpeningQuantity,
        ReorderPoint = value.ReorderPoint,
        Status = value.Status,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        LastPurchasedDate = value.LastPurchasedDate,
        PurchaseAccount = Map(value.PurchaseAccount),
        SalesAccount = Map(value.SalesAccount),
        PurchaseTaxRate = Map(value.PurchaseTaxRate),
        SalesTaxRate = Map(value.SalesTaxRate),
        InventoryAccount = Map(value.InventoryAccount),
        ItemCategory = Map(value.ItemCategory),
    };
    public static ItemAccountDto Map(Account value) => value == null ? null : new ItemAccountDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Code = value.Code,
        Name = value.Name,
        CategoryId = value.CategoryId,
        IsSubAccount = value.IsSubAccount,
        ParentAccountId = value.ParentAccountId,
        RequireCustomer = value.RequireCustomer,
        RequireSupplier = value.RequireSupplier,
        Notes = value.Notes,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
        Category = Map(value.Category),
        ParentAccount = Map(value.ParentAccount),
    };
    public static ItemAccountCategoryDto Map(AccountCategory value) => value == null ? null : new ItemAccountCategoryDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Type = value.Type,
        OrderNo = value.OrderNo,
        AccountCodePrefix = value.AccountCodePrefix,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
    };
    public static ItemCategoryDto Map(ItemCategory value) => value == null ? null : new ItemCategoryDto
    {
        Id = value.Id,
        UserConfigId = value.UserConfigId,
        Name = value.Name,
        Status = value.Status,
        CreatedDate = value.CreatedDate,
        LastUpdatedDate = value.LastUpdatedDate,
        CreatedByUserId = value.CreatedByUserId,
        LastUpdatedByUserId = value.LastUpdatedByUserId,
    };
    public static ItemTaxRateDto Map(TaxRate value) => value == null ? null : new ItemTaxRateDto
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
        TaxAccount = Map(value.TaxAccount),
        SalesAccount = Map(value.SalesAccount),
        ApplyToSalesOrPurchase = value.ApplyToSalesOrPurchase,
        Deleted = value.Deleted,
    };
}

