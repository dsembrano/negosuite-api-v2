using System;
namespace negosuite_api.Contracts.Transactions;

// Shared scalar journal fields; owning-document links belong to the module request.
public class TransactionJournalRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
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
    public DateTime? DueDate { get; set; }
    public int? TaxRateId { get; set; }
    public bool? IsComputed { get; set; }
    public string Particular { get; set; }
    public string Payee { get; set; }
    public string Payor { get; set; }
    public string PaymentAdjustmentEntry { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public bool? Deleted { get; set; }
}
