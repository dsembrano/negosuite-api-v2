using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class ResponsibilityCenterLedger
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public DateTime Period { get; set; }
        public short ResponsibilityCenter1Id { get; set; }
        public short ResponsibilityCenter2Id { get; set; }
        public short ResponsibilityCenter3Id { get; set; }
        public short? ResponsibilityCenter4Id { get; set; }
        public decimal BeginBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal? BalanceEnd { get; set; }
        public DateTime? DateProcessed { get; set; }
    }
}
