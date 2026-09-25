using System;

namespace negosuite_api.Models
{
    public class SalesTransactionDetail
    {
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal Cost { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public int TaxRateId { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public string Source { get; set; }
        public string SourceName { get; set; }
        public short Status { get; set; }
        public bool IsTaxExclusive { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
    }
}

