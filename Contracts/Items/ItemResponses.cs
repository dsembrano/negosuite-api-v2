using System;
namespace negosuite_api.Contracts.Items;

public class ItemDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Unit { get; set; }
    public int? ItemCategoryId { get; set; }
    public bool ToPurchase { get; set; }
    public decimal? Cost { get; set; }
    public decimal? AverageCost { get; set; }
    public int? PurchaseAccountId { get; set; }
    public int? PurchaseTaxRateId { get; set; }
    public bool ToSell { get; set; }
    public decimal? Rate { get; set; }
    public int? SalesAccountId { get; set; }
    public int? SalesTaxRateId { get; set; }
    public string Notes { get; set; }
    public bool? TrackInventory { get; set; }
    public int? InventoryAccountId { get; set; }
    public decimal? OpeningQuantity { get; set; }
    public decimal? ReorderPoint { get; set; }
    public bool Status { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public DateTime? LastPurchasedDate { get; set; }
    public ItemAccountDto PurchaseAccount { get; set; }
    public ItemAccountDto SalesAccount { get; set; }
    public ItemTaxRateDto PurchaseTaxRate { get; set; }
    public ItemTaxRateDto SalesTaxRate { get; set; }
    public ItemAccountDto InventoryAccount { get; set; }
    public ItemCategoryDto ItemCategory { get; set; }
}
public class ItemAccountDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int CategoryId { get; set; }
    public bool IsSubAccount { get; set; }
    public int? ParentAccountId { get; set; }
    public bool RequireCustomer { get; set; }
    public bool RequireSupplier { get; set; }
    public string Notes { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ItemAccountCategoryDto Category { get; set; }
    public ItemAccountDto ParentAccount { get; set; }
}
public class ItemAccountCategoryDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public int OrderNo { get; set; }
    public string AccountCodePrefix { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}
public class ItemCategoryDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Name { get; set; }
    public bool Status { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
}
public class ItemTaxRateDto
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
    public ItemAccountDto TaxAccount { get; set; }
    public ItemAccountDto SalesAccount { get; set; }
    public string ApplyToSalesOrPurchase { get; set; }
    public bool? Deleted { get; set; }
}

