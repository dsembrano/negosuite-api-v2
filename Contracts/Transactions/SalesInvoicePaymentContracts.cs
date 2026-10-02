using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class SalesInvoicePaymentWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; }
    public int? PaymentModeId { get; set; }
    public int? DepositToAccountId { get; set; }
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
    public List<SalesInvoicePaymentJournalRequest> JournalEntries { get; set; } = new();
}
public sealed class SalesInvoicePaymentCreateRequest : SalesInvoicePaymentWriteRequest { }
public sealed class SalesInvoicePaymentUpdateRequest : SalesInvoicePaymentWriteRequest { }
public sealed class SalesInvoicePaymentJournalRequest : TransactionJournalRequest
{
    public int? SalesInvoicePaymentId { get; set; }
    public int? PaymentToJournalEntryId { get; set; }
}

public sealed class SalesInvoicePaymentDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; }
    public int? PaymentModeId { get; set; }
    public int? DepositToAccountId { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public CustomerDetailDto Customer { get; set; }
    public TransactionPaymentModeDto PaymentMode { get; set; }
    public ItemAccountDto DepositToAccount { get; set; }
    public ICollection<TransactionJournalDto> JournalEntries { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
}
