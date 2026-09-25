using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Bill
    {
        public Bill()
        {
            BillDetails = new HashSet<BillDetail>();
            JournalEntries = new HashSet<JournalEntry>();
        }

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

        public virtual Supplier Supplier { get; set; }
        public virtual PaymentTerm PaymentTerm { get; set; }
        public virtual ICollection<BillDetail> BillDetails { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; }
        public virtual InventoryLocation InventoryLocation { get; set; }

    }

    public partial class SPBill
    {
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

        public string SupplierName { get; set; }
        public string SupplierTIN { get; set; }
        public string PaymentTermName { get; set; }

    }

}
