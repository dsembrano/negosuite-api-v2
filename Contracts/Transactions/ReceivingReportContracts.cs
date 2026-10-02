using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class ReceivingReportWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int SupplierId { get; set; }
    public string LandedCostsJson { get; set; }
    public int CreditAccountId { get; set; }
    public string PurchaseOrderNo { get; set; }
    public string DeliveryReceiptNo { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string Taxes { get; set; }
    public bool? IsTaxExclusive { get; set; }
    public bool? DiscountIsBeforeTax { get; set; }
    public bool? HasItemLevelDiscount { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public int? InventoryLocationId { get; set; }
    public List<ReceivingReportLineRequest> ReceivingReportDetails { get; set; } = new();
    public List<ReceivingReportJournalRequest> JournalEntries { get; set; } = new();
}
public sealed class ReceivingReportCreateRequest : ReceivingReportWriteRequest { }
public sealed class ReceivingReportUpdateRequest : ReceivingReportWriteRequest { }
public sealed class ReceivingReportJournalRequest : TransactionJournalRequest
{
}
public sealed class ReceivingReportLineRequest
{
    public int Id { get; set; }
    public int ReceivingReportId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRateId { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public int? InventoryLocationId { get; set; }
    public string LandedCostJson { get; set; }
    public decimal? LandedCost { get; set; }
    public bool? Deleted { get; set; }
}

public sealed class ReceivingReportDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int SupplierId { get; set; }
    public string LandedCostsJson { get; set; }
    public int CreditAccountId { get; set; }
    public string PurchaseOrderNo { get; set; }
    public string DeliveryReceiptNo { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string Taxes { get; set; }
    public bool? IsTaxExclusive { get; set; }
    public bool? DiscountIsBeforeTax { get; set; }
    public bool? HasItemLevelDiscount { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public int? InventoryLocationId { get; set; }
    public SupplierDetailDto Supplier { get; set; }
    public ItemAccountDto CreditAccount { get; set; }
    public ICollection<ReceivingReportLineDto> ReceivingReportDetails { get; set; }
    public ICollection<TransactionJournalDto> JournalEntries { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
}

public sealed class ReceivingReportLineDto
{
    public int Id { get; set; }
    public int ReceivingReportId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRateId { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ItemDetailDto Item { get; set; }
    public TaxRateDto TaxRate { get; set; }
    public int? InventoryLocationId { get; set; }
    public string LandedCostJson { get; set; }
    public decimal? LandedCost { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
    public bool? Deleted { get; set; }
}
