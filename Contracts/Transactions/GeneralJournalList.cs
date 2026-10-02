using System;
namespace negosuite_api.Contracts.Transactions;

public sealed class GeneralJournalListItemDto
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; }
    public DateTime ReferenceDate { get; set; }
    public string Notes { get; set; }
    public string ResponsibilityCenterEntry { get; set; }
    public short Status { get; set; }
    public string StatusName { get; set; }
}
