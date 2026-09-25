using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class SalesInvoice
    {
        public SalesInvoice()
        {
            SalesInvoiceDetails = new HashSet<SalesInvoiceDetail>();
            JournalEntries = new HashSet<JournalEntry>();
        }

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

        public virtual Customer Customer { get; set; }
        public virtual Supplier Supplier { get; set; }
        public virtual PaymentTerm PaymentTerm { get; set; }
        public virtual ICollection<SalesInvoiceDetail> SalesInvoiceDetails { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
        public int? InventoryLocationId { get; set; }
        public InventoryLocation InventoryLocation { get; set; }

        [NotMapped]
        public bool? AutoReferenceNo { get; set; }

    }

    public class SPSalesInvoice
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
        public string CustomerName { get; set; }
        public string CustomerTIN { get; set; }
        public string PaymentTermName { get; set; }

    }

}