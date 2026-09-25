using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace negosuite_api.Models
{
    public partial class Item
    {
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Unit { get; set; }
        public int? ItemCategoryId { get; set; }
        public bool ToPurchase { get; set; }
        public decimal? Cost { get; set; }
        public decimal? AverageCost { get; set; }
        public int? PurchaseAccountId { get; set; }
        public int? PurchaseTaxRateId { get; set; }
        public bool ToSell { get; set; }
        public decimal? Rate { get; set; }
        public int? SalesAccountId { get; set; }
        public int? SalesTaxRateId { get; set; }
        public string Notes { get; set; }
        public bool? TrackInventory { get; set; }
        public int? InventoryAccountId { get; set; }
        public decimal? OpeningQuantity { get; set; }
        public decimal? ReorderPoint { get; set; }
        public bool Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public DateTime? LastPurchasedDate { get; set; }
        
        public virtual Account PurchaseAccount { get; set; }
        public virtual Account SalesAccount { get; set; }
        public virtual TaxRate PurchaseTaxRate { get; set; }
        public virtual TaxRate SalesTaxRate { get; set; }
        public virtual Account InventoryAccount { get; set; }
        public virtual ItemCategory ItemCategory { get; set; }

    }
}
