using System;
using System.Linq;
using System.Linq.Expressions;
using negosuite_api.Contracts.Transactions;
namespace negosuite_api.Services;

public static class GeneralJournalQuery
{
    public static bool IsValidSort(string field, string direction) => (field is null or "referenceNo" or "referenceDate" or "notes" or "status" or "statusName") && (direction is null or "asc" or "desc");
    public static IQueryable<GeneralJournalListItemDto> Search(IQueryable<GeneralJournalListItemDto> query, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim();
        return query.Where(i => (i.ReferenceNo != null && i.ReferenceNo.Contains(term)) || (i.Notes != null && i.Notes.Contains(term)) || (i.StatusName != null && i.StatusName.Contains(term)));
    }
    public static IOrderedQueryable<GeneralJournalListItemDto> Sort(IQueryable<GeneralJournalListItemDto> query, string field, string direction)
    {
        if (!IsValidSort(field, direction)) throw new ArgumentException("Unsupported transaction sort.");
        var desc = direction == "desc";
        var ordered = field switch
        {
            "referenceNo" => Order(query, i => i.ReferenceNo, desc),
            "notes" => Order(query, i => i.Notes, desc),
            "status" => Order(query, i => i.Status, desc),
            "statusName" => Order(query, i => i.StatusName, desc),
            _ => Order(query, i => i.ReferenceDate, desc).ThenBy(i => i.ReferenceNo)
        };
        return ordered.ThenBy(i => i.Id);
    }
    private static IOrderedQueryable<GeneralJournalListItemDto> Order<T>(IQueryable<GeneralJournalListItemDto> query, Expression<Func<GeneralJournalListItemDto, T>> key, bool desc) => desc ? query.OrderByDescending(key) : query.OrderBy(key);
}