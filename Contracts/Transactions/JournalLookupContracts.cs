using System;
using negosuite_api.Contracts.Customers;

namespace negosuite_api.Contracts.Transactions;

public sealed class JournalLookupCriteria
{
    public int? UserConfigId { get; set; }
    public int? AccountId { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

public sealed class UnpaidInvoiceDto
{
    public int JournalEntryId { get; set; }
    public string InvoiceNo { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
}

public sealed class UnpaidBillDto
{
    public int JournalEntryId { get; set; }
    public string BillNo { get; set; }
    public DateTime BillDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
}

public class UnappliedCreditDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string Source { get; set; }
    public string SourceName { get; set; }
}

public sealed class UnappliedCreditDetailDto : UnappliedCreditDto
{
    public CustomerDetailDto Customer { get; set; }
}
