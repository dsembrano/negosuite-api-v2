using System;
using System.Collections.Generic;

namespace negosuite_api.Models
{
    public class AccountTotal
    {
        public AccountTotal()
        {
        }

        public int Id { get; set; }
        public string Period { get; set; }
        public int AccountId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }

        public virtual Account Account { get; set; }
    }
}
