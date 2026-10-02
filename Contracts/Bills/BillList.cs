using System;
namespace negosuite_api.Contracts.Bills;

public class BillListCriteria
{
    public int? UserConfigId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public int? SupplierId { get; set; }
    public string ReferenceNo { get; set; }
    public string ArrayString { get; set; }
}
public class BillListItemDto
{
    public int Id { get; set; }
    public string BillNo { get; set; }
    public DateTime BillDate { get; set; }
    public DateTime DueDate { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; }
    public string SupplierTIN { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Balance { get; set; }
    public int? PaymentTermId { get; set; }
    public string PaymentTermName { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public string StatusName { get; set; }
}
