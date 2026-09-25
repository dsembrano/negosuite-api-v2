using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class CreditorType
    {
        public CreditorType()
        {
            Creditors = new HashSet<Creditor>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual ICollection<Creditor> Creditors { get; set; }
    }
}
