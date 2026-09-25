using System;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public class StockTransferDetail
    {
        public int Id { get; set; }
        public int StockTransferId { get; set; }
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public string Notes { get; set; }
        public short Status { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public Item Item { get; set; }

        [NotMapped]
        public bool? Deleted { get; set; }

    }
}
