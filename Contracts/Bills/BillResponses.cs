using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;

namespace negosuite_api.Contracts.Bills;

public sealed class BillDetailDto
{
    public int? PurchaseWorkflowId { get; set; }
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string BillNo { get; set; }
    public DateTime BillDate { get; set; }
    public int SupplierId { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime DueDate { get; set; }
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
    public PaymentTermDto PaymentTerm { get; set; }
    public ICollection<BillLineDto> BillDetails { get; set; } = new List<BillLineDto>();
    public ICollection<TransactionJournalDto> JournalEntries { get; set; } = new List<TransactionJournalDto>();
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
}

public sealed class BillLineDto
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRateId { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Notes { get; set; }
    public bool? IsInventoryTransaction { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ItemDetailDto Item { get; set; }
    public TaxRateDto TaxRate { get; set; }
    public int? InventoryLocationId { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
    public string LandedCostJson { get; set; }
    public decimal? LandedCost { get; set; }
    public bool? Deleted { get; set; }
    public bool? Touched { get; set; }
}
