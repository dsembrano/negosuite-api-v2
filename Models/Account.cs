using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Account
    {
        public Account()
        {
        }

        public int Id { get; set; }
        public int UserConfigId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public bool IsSubAccount { get; set; }
        public int? ParentAccountId { get; set; }
        public bool RequireCustomer { get; set; }
        public bool RequireSupplier { get; set; }
        public string Notes { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual AccountCategory Category { get; set; }
        public virtual Account ParentAccount { get; set; }

    }

}
