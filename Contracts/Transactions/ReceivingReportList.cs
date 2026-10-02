using System;
namespace negosuite_api.Contracts.Transactions;

public sealed class ReceivingReportListItemDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string DeliveryReceiptNo { get; set; }
    public string PurchaseOrderNo { get; set; }
    public string InventoryLocationName { get; set; }
    public string Notes { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; }
}
