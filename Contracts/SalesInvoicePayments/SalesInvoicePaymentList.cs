using System;
namespace negosuite_api.Contracts.SalesInvoicePayments;

public class SalesInvoicePaymentListCriteria
{
    public int? UserConfigId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public int? CustomerId { get; set; }
    public string ReferenceNo { get; set; }
    public string ArrayString { get; set; }

}

public class SalesInvoicePaymentListItemDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public int? PaymentModeId { get; set; }
    public string PaymentModeName { get; set; }
    public int? DepositToAccountId { get; set; }
    public string DepositToAccountName { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public string StatusName { get; set; }
}
