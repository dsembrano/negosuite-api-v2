using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace negosuite_api.Contracts.Transactions;
public class SalesReturnWriteRequest
{
    public long Version { get; set; }
    [Required,StringLength(36)] public string RequestKey { get; set; }
    [Required] public string Source { get; set; }
    public int SourceId { get; set; }
    [StringLength(50)] public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int? InventoryLocationId { get; set; }
    [Required,StringLength(250)] public string Reason { get; set; }
    public string Notes { get; set; }
    [Required] public List<SalesReturnLineRequest> Lines { get; set; } = new();
    [Required] public List<ReceivableApplication> Applications { get; set; } = new();
}
public class SalesReturnLineRequest { public int SourceDetailId { get; set; } public decimal Quantity { get; set; } public string Notes { get; set; } }
public class ReceivableApplication { public string InvoiceNo { get; set; } public int JournalEntryId { get; set; } public decimal Amount { get; set; } }
public class SalesReturnActionRequest { public long Version { get; set; } public string Reason { get; set; } }
public class SalesReturnListRequest
{
    public int? PageNumber { get; set; } public int? PageSize { get; set; }
    public string Search { get; set; } public string SortBy { get; set; } public string SortDirection { get; set; }
    public short Status { get; set; } = 1; public int? CustomerId { get; set; }
    public DateTime? PeriodStart { get; set; } public DateTime? PeriodEnd { get; set; }
}
public record SalesReturnListRow(int Id,string ReferenceNo,DateTime ReferenceDate,string CustomerName,string Source,string SourceNo,decimal Amount,decimal Balance,string Reason,short Status,long Version);
public class ReturnSource
{
    public string Source { get; set; } public int Id { get; set; } public string ReferenceNo { get; set; } public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; } public string CustomerName { get; set; } public string BillingAddress { get; set; } public string BillingContactName { get; set; } public string BillingContactEmail { get; set; }
    public string ResponsibilityCenterEntry { get; set; } public int? InventoryLocationId { get; set; }
    public decimal Amount { get; set; } public int ReceivableAccountId { get; set; } public int? ReceivableJournalId { get; set; }
    public List<ReturnSourceLine> Lines { get; set; } = new();
}
public class ReturnSourceLine
{
    public int Id { get; set; } public int ItemId { get; set; } public string Name { get; set; } public string Unit { get; set; }
    public decimal Quantity { get; set; } public decimal GrossAmount { get; set; }
    public decimal TaxExemptAmount { get; set; }
    public decimal Rate { get; set; } public decimal Cost { get; set; }
    public decimal DiscountAmount { get; set; } public decimal TaxAmount { get; set; } public int? TaxRateId { get; set; } public string TaxName { get; set; }
    public bool TrackInventory { get; set; }
    public List<ReturnComponent> Components { get; set; } = new();
}
public class ReturnComponent
{
    public int SourceJournalId { get; set; } public int AccountId { get; set; } public string AccountName { get; set; }
    public string Nature { get; set; } public decimal Amount { get; set; } public string Kind { get; set; }
    public int? SupplierId { get; set; } public int? CustomerId { get; set; } public string ResponsibilityCenterEntry { get; set; }
}
