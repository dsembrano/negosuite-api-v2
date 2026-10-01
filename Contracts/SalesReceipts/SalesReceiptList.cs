using System;
namespace negosuite_api.Contracts.SalesReceipts;

public class SalesReceiptListCriteria
{
    public int? UserConfigId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public int? CustomerId { get; set; }
    public string ReferenceNo { get; set; }
    public string ArrayString { get; set; }
    public bool? IsPOS { get; set; }
}

public class SalesReceiptListItemDto
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; }
    public DateTime ReceiptDate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string CustomerTIN { get; set; }
    public string BillingAddress { get; set; }
    public string BillingContactName { get; set; }
    public string BillingContactEmail { get; set; }
    public string ShippingAddress { get; set; }
    public string ShippingContactName { get; set; }
    public string ShippingContactEmail { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public int? PaymentModeId { get; set; }
    public string PaymentModeName { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string StatusName { get; set; }
}
