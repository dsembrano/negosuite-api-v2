using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using negosuite_api.Contracts.Transactions;
namespace negosuite_api.Contracts.Payments;

public class PaymentWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    [Required, StringLength(50)] public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int? SupplierId { get; set; }
    public int? CustomerId { get; set; }
    public bool IsBillPayment { get; set; }
    public string Payee { get; set; }
    public int? PaymentModeId { get; set; }
    public string CheckNo { get; set; }
    public int? PaidThroughAccountId { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public List<PaymentJournalRequest> JournalEntries { get; set; } = new();
}

public sealed class PaymentCreateRequest : PaymentWriteRequest { }
public sealed class PaymentUpdateRequest : PaymentWriteRequest { }

public sealed class PaymentJournalRequest : TransactionJournalRequest
{
    public int? PaymentId { get; set; }
    public int? PaymentToJournalEntryId { get; set; }
}
