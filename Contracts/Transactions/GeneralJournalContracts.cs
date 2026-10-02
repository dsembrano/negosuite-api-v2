using System;
using System.Collections.Generic;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Suppliers;
using negosuite_api.Contracts.Items;
namespace negosuite_api.Contracts.Transactions;

public class GeneralJournalWriteRequest
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public List<GeneralJournalJournalRequest> JournalEntries { get; set; } = new();
}
public sealed class GeneralJournalCreateRequest : GeneralJournalWriteRequest { }
public sealed class GeneralJournalUpdateRequest : GeneralJournalWriteRequest { }
public sealed class GeneralJournalJournalRequest : TransactionJournalRequest
{
    public int? GeneralJournalId { get; set; }
    public int? PaymentToJournalEntryId { get; set; }
}

public sealed class GeneralJournalDetailDto
{
    public int Id { get; set; }
    public int UserConfigId { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public string Notes { get; set; }
    public short Status { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public int? LastUpdatedByUserId { get; set; }
    public ICollection<TransactionJournalDto> JournalEntries { get; set; }
}
