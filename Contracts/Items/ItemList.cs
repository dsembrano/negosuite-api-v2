namespace negosuite_api.Contracts.Items;

public class ItemListCriteria
{
    public int? UserConfigId { get; set; }
    public string ItemType { get; set; }
    public int? ItemCategoryId { get; set; }
    public bool? ShowInactive { get; set; }
}

public class ItemListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public int? ItemCategoryId { get; set; }
    public string ItemCategoryName { get; set; }
    public string TypeName { get; set; }
    public string Type { get; set; }
    public string Unit { get; set; }
    public decimal? Rate { get; set; }
    public decimal? Cost { get; set; }
    public bool Status { get; set; }
    public bool ToSell { get; set; }
    public bool ToPurchase { get; set; }
    public bool? TrackInventory { get; set; }
    public decimal? ReorderPoint { get; set; }
}

public record ItemUnitDto(string Name);
