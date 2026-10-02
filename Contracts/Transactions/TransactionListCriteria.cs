using System;

namespace negosuite_api.Contracts.Transactions;

public sealed class TransactionListCriteria
{
    public int? UserConfigId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string ReferenceNo { get; set; }
    public int? CustomerId { get; set; }
    public int? SupplierId { get; set; }
    public string ArrayString { get; set; }
    public bool? ShowDeleted { get; set; }
}
