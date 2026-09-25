using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Creditor
    {
        public Creditor()
        {
        }

        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string PhoneNo { get; set; }
        public string Email { get; set; }
        public string ContactName { get; set; }
        public decimal? CreditLimit { get; set; }
        public string Tin { get; set; }
        public int CreditorTypeId { get; set; }
        public short IsVat { get; set; }
        public short IsGenericName { get; set; }
        public short IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual CreditorType CreditorType { get; set; }

    }
}
