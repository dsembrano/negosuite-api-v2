using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace negosuite_api.Models
{
    public class TaxRate
    {
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Name { get; set; }
        public decimal Rate { get; set; }
        public int? TaxAccountId { get; set; }
        public int? SalesAccountId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public Account TaxAccount { get; set; }
        public Account SalesAccount { get; set; }
        public string ApplyToSalesOrPurchase { get; set; }
        
        [NotMapped]
        public bool? Deleted { get; set; }
    }
}
