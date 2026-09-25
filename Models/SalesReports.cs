namespace negosuite_api.Models
{
    public partial class ItemSale
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal Cost { get; set; }
        public decimal Quantity { get; set; }
        public decimal Sales { get; set; }
        public decimal SalesWithTax { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal AverageCost { get; set; }
        public int Count { get; set; }
    }


    public partial class CustomerSale
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal? Cost { get; set; }
        public decimal? Sales { get; set; }
        public decimal? SalesWithTax { get; set; }
        public decimal? TaxAmount { get; set; }
        public int InvoiceCount { get; set; }
    }

}
