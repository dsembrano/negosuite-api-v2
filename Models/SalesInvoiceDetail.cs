using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace negosuite_api.Models
{
    public partial class SalesInvoiceDetail
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

        public Item Item { get; set; }
        public TaxRate TaxRate { get; set; }

        [NotMapped]
        public bool? Deleted { get; set; }
        public int? InventoryLocationId { get; set; }
        public InventoryLocation InventoryLocation { get; set; }
    }
}

