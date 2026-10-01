using System;

namespace negosuite_api.Contracts.SalesInvoices;

public class SalesInvoiceListCriteria
{
    public int? UserConfigId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string ReferenceNo { get; set; }
    public string ArrayString { get; set; }
}

public class SalesInvoiceListItemDto
{
    public int Id { get; set; }
    public string InvoiceNo { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string PurchaseOrderNo { get; set; }
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
    public int? PaymentTermId { get; set; }
    public string PaymentTermName { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
}
