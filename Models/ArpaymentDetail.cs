using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class ArpaymentDetail
    {
        public int Id { get; set; }
        public int PaymentJournalEntryId { get; set; }
        public int ReceivableJournalEntryId { get; set; }
        public decimal Amount { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual JournalEntry PaymentJournalEntry { get; set; }
        public virtual JournalEntry ReceivableJournalEntry { get; set; }
    }
}
