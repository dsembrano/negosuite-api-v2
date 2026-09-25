using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{

    public partial class GeneralLedgerRecap
    {
        public GeneralLedgerRecap()
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
        public decimal? OpeningDebit { get; set; }
        public decimal? OpeningCredit { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

    }


    public partial class GeneralLedgerDetails
    {
        public GeneralLedgerDetails() { }

        public string ReferenceNo { get; set; }
        public DateTime? JournalDate { get; set; }

        public int AccountId { get; set; }
        public string AccountName { get; set; }

        public string CategoryName { get; set; }
        public string AccountType { get; set; }

        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        public int? CustomerId { get; set; }
        public string CustomerName { get; set; }

        public int? SupplierId { get; set; }
        public string SupplierName { get; set; }

        public string Payee { get; set; }
        public string Source { get; set; }
        [NotMapped]
        public string SourceName { get; set; }
        public string Status { get; set; }

        public DateTime? DueDate { get; set; }
        public string Notes { get; set; }
    }


}
