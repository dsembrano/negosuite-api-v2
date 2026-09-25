using System;

namespace negosuite_api.Models
{
    public class SalesTransaction
    {
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal Cost { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string Taxes { get; set; }
        public string Source { get; set; }
        public string SourceName { get; set; }
        public short Status { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
    }

    public class SalesMonthlyTrend
    {
        public string Month { get; set; }
        public decimal TotalAmount { get; set; }
        public int InvoiceCount { get; set; }
    }

}