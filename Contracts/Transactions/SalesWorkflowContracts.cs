using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace negosuite_api.Contracts.Transactions;
public class SalesWorkflowWrite
{
    public long Version { get; set; }
    [Required,StringLength(36)] public string RequestKey { get; set; }
    [StringLength(50)] public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public int? InventoryLocationId { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    [StringLength(250)] public string Salesperson { get; set; }
    [StringLength(250)] public string PriceList { get; set; }
    public string DeliveryAddress { get; set; }
    public string ResponsibilityCenterEntry { get; set; } = "[]";
    public string Notes { get; set; }
    public bool IsTaxExclusive { get; set; } = true;
    public bool HasItemLevelDiscount { get; set; } = true;
    public string DiscountMode { get; set; } = "percent";
    public decimal DiscountValue { get; set; }
    [Required] public List<SalesWorkflowLineWrite> Lines { get; set; } = new();
}
public class SalesWorkflowLineWrite
{
    public int ItemId { get; set; }
    public string Description { get; set; }
    public int? InventoryLocationId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountPercent { get; set; } = 0;
    public decimal DiscountAmount { get; set; }
    public int? TaxRateId { get; set; }
    public int? SourceDocumentId { get; set; }
    public int? SourceLineId { get; set; }
}
public class SalesWorkflowAction
{
    public long Version { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public string Reason { get; set; }
    public string Disposition { get; set; }
}
public class DeliveryInvoiceWrite
{
    [Required,StringLength(36)] public string RequestKey { get; set; }
    public string Kind { get; set; }
    public DateTime ReferenceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int? PaymentModeId { get; set; }
    public int? DepositToAccountId { get; set; }
    public List<DeliveryInvoiceLineWrite> Lines { get; set; } = new();
}
public class DeliveryInvoiceLineWrite { public int DeliveryId { get; set; } public int LineId { get; set; } public decimal Quantity { get; set; } }
