using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class AccountRecap
    {
        public AccountRecap()
        {
        }

        public int AccountId { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int? CategoryOrderNo { get; set; }
        public string CategoryAccountPrefix { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

    }

}
