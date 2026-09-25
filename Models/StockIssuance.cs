using System;
using System.Collections.Generic;

namespace negosuite_api.Models
{
    public class StockIssuance
    {
        public StockIssuance()
        {
            StockIssuanceDetails = new HashSet<StockIssuanceDetail>();
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public string Notes { get; set; }
        public short Status { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public int InventoryLocationId { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
        public virtual ICollection<StockIssuanceDetail> StockIssuanceDetails { get; set; }
        public InventoryLocation InventoryLocation { get; set; }
        public Customer Customer { get; set; }
        public Supplier Supplier { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; }
    }


    public class SPStockIssuance
    {
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public string Notes { get; set; }
        public short Status { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public int InventoryLocationId { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public string ResponsibilityCenterEntry { get; set; }

        public string InventoryLocationName { get; set; }
        public string CustomerName { get; set; }
        public string SupplierName { get; set; }

    }
}
