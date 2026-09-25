using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class GeneralLedger
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public DateTime Period { get; set; }
        public decimal BeginBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal BalanceEnd { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateEdited { get; set; }
        public string CreatedBy { get; set; }
        public string EditedBy { get; set; }
        public DateTime? DateProcessed { get; set; }
    }
}
