using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public class StockTransfer
    {
        public StockTransfer()
        {
            StockTransferDetails = new HashSet<StockTransferDetail>();
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
        public int FromInventoryLocationId { get; set; }
        public int ToInventoryLocationId { get; set; }
        public string ResponsibilityCenterEntry { get; set; }

        public virtual ICollection<StockTransferDetail> StockTransferDetails { get; set; }
        public InventoryLocation FromInventoryLocation { get; set; }
        public InventoryLocation ToInventoryLocation { get; set; }
    }

    public class SPStockTransfer
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
        public int FromInventoryLocationId { get; set; }
        public int ToInventoryLocationId { get; set; }
        public string ResponsibilityCenterEntry { get; set; }

        public string FromInventoryLocationName { get; set; }
        public string ToInventoryLocationName { get; set; }
    }
}
