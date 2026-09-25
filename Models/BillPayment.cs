using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class BillPayment
    {
        public BillPayment()
        {
            JournalEntries = new HashSet<JournalEntry>();
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime ReferenceDate { get; set; }
        public int SupplierId { get; set; }
        public int? PaymentModeId { get; set; }
        public string CheckNo { get; set; }
        public int? PaidThroughAccountId { get; set; }
        public string Notes { get; set; }
        public short Status { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Balance { get; set; }
        public DateTime? PostedDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual Supplier Supplier { get; set; }
        public virtual PaymentMode PaymentMode { get; set; }
        public virtual Account PaidThroughAccount { get; set; }
        public virtual ICollection<JournalEntry> JournalEntries { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
    }
}
