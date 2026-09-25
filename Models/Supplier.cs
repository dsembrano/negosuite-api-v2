using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Supplier
    {
        public Supplier()
        {
            SupplierAddresses = new HashSet<SupplierAddress>();
            SupplierContacts = new HashSet<SupplierContact>();
        }
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Tin { get; set; }
        public int? TaxRateId { get; set; }
        public int? PaymentTermId { get; set; }
        public string Notes { get; set; }
        public bool Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public virtual TaxRate TaxRate { get; set; }
        public virtual PaymentTerm PaymentTerm { get; set; }
        public virtual ICollection<SupplierAddress> SupplierAddresses { get; set; }
        public virtual ICollection<SupplierContact> SupplierContacts { get; set; }
    }
}
