using System;
namespace negosuite_api.Contracts.Transactions;

public sealed class InventoryAdjustmentListItemDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string Notes { get; set; }
    public string InventoryLocationName { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; }
}
