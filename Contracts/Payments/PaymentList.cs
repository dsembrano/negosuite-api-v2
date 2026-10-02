using System;
namespace negosuite_api.Contracts.Payments;

public class PaymentListCriteria
{
    public int? UserConfigId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public int? SupplierId { get; set; }
    public string ReferenceNo { get; set; }
    public string ArrayString { get; set; }
}

public class PaymentListItemDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; }
    public int? CustomerId { get; set; }
    // Preserve the historical JSON spelling used by existing clients.
    public string CustomerrName { get; set; }
    public string Payee { get; set; }
    public int? PaymentModeId { get; set; }
    public string PaymentModeName { get; set; }
    public int? PaidThroughAccountId { get; set; }
    public string PaidThroughAccountName { get; set; }
    public string CheckNo { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; }
}
