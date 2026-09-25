using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class ResponsibilityCenterJournalEntry
    {
        public ResponsibilityCenterJournalEntry()
        {
        }

        public int Id { get; set; }
        public string ReferenceNo { get; set; }
        public DateTime JournalDate { get; set; }
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public string Nature { get; set; }
        public string ResponsibilityCenterEntry { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public int? DebtorId { get; set; }
        public int? CreditorId { get; set; }
        public string Notes { get; set; }
        public string Source { get; set; }
        public decimal? CurrencyXrate { get; set; }
        public short Status { get; set; }
        public DateTime? PostedDate { get; set; }
        public int? PostedByUserId { get; set; }
        public int? SalesInvoiceId { get; set; }
        public int? SalesInvoicePaymentId { get; set; }
        public int? SalesReceiptId { get; set; }
        public int? BillId { get; set; }
        public int? GeneralJournalId { get; set; }
        public int? PaymentId { get; set; }
        public int? PaymentToJournalEntryId { get; set; }
        public DateTime? DueDate { get; set; }
        public int? TaxRateId { get; set; }
        public bool? IsComputed { get; set; }
        public string Particular { get; set; }
        public string Payee { get; set; }
        public string Payor { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }
        public string CustomerName { get; set; }
        public string SupplierName { get; set; }
        public string AccountName { get; set; }
        
    }

}
