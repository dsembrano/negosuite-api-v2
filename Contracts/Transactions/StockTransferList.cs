using System;
namespace negosuite_api.Contracts.Transactions;

public sealed class StockTransferListItemDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public string Notes { get; set; }
    public string FromInventoryLocationName { get; set; }
    public string ToInventoryLocationName { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; }
}
