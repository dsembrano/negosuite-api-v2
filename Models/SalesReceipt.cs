using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class SalesReceipt
    {
        public SalesReceipt()
        {
            SalesReceiptDetails = new HashSet<SalesReceiptDetail>();
            JournalEntries = new HashSet<JournalEntry>();
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string ReceiptNo { get; set; }
        public DateTime ReceiptDate { get; set; }
        public int CustomerId { get; set; }
        public string BillingAddress { get; set; }
        public string BillingContactName { get; set; }
        public string BillingContactEmail { get; set; }
        public string ShippingAddress { get; set; }
        public string ShippingContactName { get; set; }
        public string ShippingContactEmail { get; set; }
        public int? PaymentModeId { get; set; }
        public int? DepositToAccountId { get; set; }
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
        public string PurchaseOrderNo { get; set; }
        public DateTime? PostedDate { get; set; }
        public bool? IsPOS { get; set; }
        public string PaymentDetails { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual PaymentMode PaymentMode { get; set; }
        public virtual Account DepositToAccount { get; set; }
        public virtual ICollection<SalesReceiptDetail> SalesReceiptDetails { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
        public int? InventoryLocationId { get; set; }
        public InventoryLocation InventoryLocation { get; set; }
        [NotMapped]
        public bool? AutoReferenceNo { get; set; }

    }
}
