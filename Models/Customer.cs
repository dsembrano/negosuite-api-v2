using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace negosuite_api.Models
{
    public class Customer
    {
        public Customer()
        {
            CustomerAddresses = new HashSet<CustomerAddress>();
            CustomerContacts = new HashSet<CustomerContact>();
        }
        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal? CreditLimit { get; set; }
        public string Tin { get; set; }
        public int? TaxRateId { get; set; }
        public int? PaymentTermId { get; set; }
        public string Notes { get; set; }
        public string BusinessStyle { get; set; }
        public bool Status { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public virtual TaxRate TaxRate { get; set; }
        public virtual PaymentTerm PaymentTerm { get; set; }
        public virtual ICollection<CustomerAddress> CustomerAddresses { get; set; }
        public virtual ICollection<CustomerContact> CustomerContacts { get; set; }
    }
}


