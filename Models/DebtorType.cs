using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class DebtorType
    {
        public DebtorType()
        {
            Debtors = new HashSet<Debtor>();
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string SLType { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual ICollection<Debtor> Debtors { get; set; }
    }
}
