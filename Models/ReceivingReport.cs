using System;
using System.Collections.Generic;

namespace negosuite_api.Models
{
    public partial class ReceivingReport
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

        public virtual Supplier Supplier { get; set; }
        public virtual Account CreditAccount { get; set; }
        public virtual ICollection<ReceivingReportDetail> ReceivingReportDetails { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; }
        public virtual InventoryLocation InventoryLocation { get; set; }
    }


    public partial class SPReceivingReport
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

        public string SupplierName { get; set; }
        public string InventoryLocationName { get; set; }
    }

}