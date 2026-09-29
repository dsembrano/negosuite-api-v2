using System.ComponentModel.DataAnnotations;
namespace negosuite_api.Contracts.Items;

public class ItemWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string Code { get; set; }
    [Required, StringLength(150)] public string Name { get; set; }
    public string Type { get; set; }
    public string Unit { get; set; }
    public int? ItemCategoryId { get; set; }
    public bool ToPurchase { get; set; }
    public decimal? Cost { get; set; }
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
}
public sealed class ItemCreateRequest : ItemWriteRequest { }
public sealed class ItemUpdateRequest : ItemWriteRequest { }

