using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace negosuite_api.Models
{
    public class DiscountType
    {
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Name { get; set; }
        public decimal Rate { get; set; }
        public decimal? DiscountAmount { get; set; }
        public int? DiscountAccountId { get; set; }
        public bool? DiscountIsBeforeTax { get; set; }
        public bool? LockedRate { get; set; }
        public int? TaxRateId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public Account DiscountAccount { get; set; }
        public TaxRate TaxRate { get; set; }

        [NotMapped]
        public bool? Deleted { get; set; }
    }
}
