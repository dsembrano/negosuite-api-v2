using System;

namespace negosuite_api.Models
{
    public class InventoryTransaction
    {
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal? AverageCost { get; set; }
        public decimal? ItemCost { get; set; }
        //public decimal? LastInCost { get; set; }
        public decimal Quantity { get; set; }
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Amount { get; set; }
        public short Status { get; set; }
        public string Source { get; set; }
        public string SourceName { get; set; }
        public string TransactionType { get; set; }
        public int? InventoryLocationId { get; set; }
        public string InventoryLocationName { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; }
        public decimal? ItemReorderPoint { get; set; }
        public string ResponsibilityCenterEntry { get; set; }

    }

    public class SPInventoryTransaction
    {
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal Quantity { get; set; }
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Amount { get; set; }
        public short Status { get; set; }
        public string Source { get; set; }
        public string SourceName { get; set; }
        public string TransactionType { get; set; }
        public int? InventoryLocationId { get; set; }
        public string InventoryLocationName { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; }
        public decimal? ItemReorderPoint { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
    }


    public class InventoryStockSummary
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public decimal? ItemReorderPoint { get; set; }
        public decimal? OpeningStock { get; set; }
        public decimal? QuantityIn { get; set; }
        public decimal? QuantityOut { get; set; }
        public decimal? AverageCost { get; set; }
        public decimal? LastInCost { get; set; }
        public DateTime? LastPurchasedDate { get; set; }
    }

    public class PivotedInventoryWithJsonArray
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string LocationQuantities { get; set; }

    }


}
