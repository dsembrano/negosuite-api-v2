using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class SalesInvoiceWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string InvoiceNo { get; set; }
    public DateTime InvoiceDate { get; set; }
    public int CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string BillingAddress { get; set; }
    public string BillingContactName { get; set; }
    public string BillingContactEmail { get; set; }
    public string ShippingAddress { get; set; }
    public string ShippingContactName { get; set; }
    public string ShippingContactEmail { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime DueDate { get; set; }
    public string Notes { get; set; }
    public string TermsConditions { get; set; }
    public short Status { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string Taxes { get; set; }
    public bool? IsTaxExclusive { get; set; }
    public bool? DiscountIsBeforeTax { get; set; }
    public bool? HasItemLevelDiscount { get; set; }
    public string PurchaseOrderNo { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public int? InventoryLocationId { get; set; }
    public bool? AutoReferenceNo { get; set; }
    public List<SalesInvoiceLineRequest> SalesInvoiceDetails { get; set; } = new();
    public List<SalesInvoiceJournalRequest> JournalEntries { get; set; } = new();
}
public sealed class SalesInvoiceCreateRequest : SalesInvoiceWriteRequest { }
public sealed class SalesInvoiceUpdateRequest : SalesInvoiceWriteRequest { }
public sealed class SalesInvoiceJournalRequest : TransactionJournalRequest
{
    public int? SalesInvoiceId { get; set; }
}
public sealed class SalesInvoiceLineRequest
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRateId { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TaxExemptAmount { get; set; }
    public string Notes { get; set; }
    public bool? IsInventoryTransaction { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
    public int? InventoryLocationId { get; set; }
}

public sealed class SalesInvoiceDetailDto
{
    public bool IsDeliveryBased { get; set; }
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string InvoiceNo { get; set; }
    public DateTime InvoiceDate { get; set; }
    public int CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string BillingAddress { get; set; }
    public string BillingContactName { get; set; }
    public string BillingContactEmail { get; set; }
    public string ShippingAddress { get; set; }
    public string ShippingContactName { get; set; }
    public string ShippingContactEmail { get; set; }
    public int? PaymentTermId { get; set; }
    public DateTime DueDate { get; set; }
    public string Notes { get; set; }
    public string TermsConditions { get; set; }
    public short Status { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string Taxes { get; set; }
    public bool? IsTaxExclusive { get; set; }
    public bool? DiscountIsBeforeTax { get; set; }
    public bool? HasItemLevelDiscount { get; set; }
    public string PurchaseOrderNo { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public CustomerDetailDto Customer { get; set; }
    public SupplierDetailDto Supplier { get; set; }
    public PaymentTermDto PaymentTerm { get; set; }
    public ICollection<SalesInvoiceLineDto> SalesInvoiceDetails { get; set; }
    public ICollection<TransactionJournalDto> JournalEntries { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public int? InventoryLocationId { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
    public bool? AutoReferenceNo { get; set; }
}

public sealed class SalesInvoiceLineDto
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Rate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRateId { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TaxExemptAmount { get; set; }
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
    public bool? Deleted { get; set; }
    public int? InventoryLocationId { get; set; }
    public TransactionInventoryLocationDto InventoryLocation { get; set; }
}
