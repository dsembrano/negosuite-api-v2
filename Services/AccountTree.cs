using System;
using System.Collections.Generic;
using System.Linq;
using negosuite_api.Contracts.Accounts;

namespace negosuite_api.Services;

public sealed record AccountTreeCategory(int Id, string Name, int OrderNo, int AccountCount, int MatchCount, int RootCount);
public sealed record AccountTreeItem(AccountListDto Account, int ChildCount, bool IsMatch);
public sealed record AccountTreeBranch(IReadOnlyList<AccountTreeItem> Items, int TotalCount);

// A request-scoped forest. Never caches another user's/company's chart or changes legacy write semantics.
public sealed class AccountTree
{
    private readonly Dictionary<int, AccountListDto> accounts;
    private readonly Dictionary<int, int?> parents;
    private readonly Dictionary<(int Category, int Parent), List<AccountListDto>> branches;
    private readonly HashSet<int> matches;
    public IReadOnlyList<AccountTreeCategory> Categories { get; }

    public AccountTree(IEnumerable<AccountListDto> rows, IEnumerable<AccountCategoryDetailDto> categories, bool useCodes, string search)
    {
        var orderedCategories = categories.OrderBy(c => c.OrderNo).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id).ToList();
        var categoryIds = orderedCategories.Select(c => c.Id).ToHashSet();
        accounts = rows.Where(a => categoryIds.Contains(a.CategoryId)).ToDictionary(a => a.Id);
        parents = accounts.Values.ToDictionary(a => a.Id, a => a.ParentAccountId is int p && accounts.TryGetValue(p, out var parent) && parent.CategoryId == a.CategoryId ? (int?)p : null);
        // Lift a deterministic node from each legacy cycle to a category root so no account vanishes.
        var complete = new HashSet<int>();
        foreach (var id in accounts.Keys.OrderBy(id => id))
        {
            var path = new List<int>(); var positions = new Dictionary<int, int>(); int? current = id;
            while (current.HasValue && !complete.Contains(current.Value))
            {
                if (positions.TryGetValue(current.Value, out var start)) { parents[path.Skip(start).Min()] = null; break; }
                positions[current.Value] = path.Count; path.Add(current.Value); current = parents[current.Value];
            }
            complete.UnionWith(path);
        }
        var term = search?.Trim() ?? "";
        bool Contains(string value) => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
        matches = accounts.Values.Where(a => term.Length == 0 || Contains(a.Code) || Contains(a.Name) || Contains(a.CategoryName) || Contains(a.Type)).Select(a => a.Id).ToHashSet();
        var included = new HashSet<int>(matches);
        foreach (var id in matches)
        {
            var parent = parents[id];
            while (parent.HasValue && included.Add(parent.Value)) parent = parents[parent.Value];
        }
        branches = accounts.Values.Where(a => included.Contains(a.Id))
            .GroupBy(a => (a.CategoryId, parents[a.Id] ?? 0))
            .ToDictionary(g => g.Key, g => g.OrderBy(a => useCodes ? a.Code : a.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Id).ToList());
        Categories = orderedCategories.Select(c => new AccountTreeCategory(c.Id, c.Name, c.OrderNo,
            accounts.Values.Count(a => a.CategoryId == c.Id), accounts.Values.Count(a => a.CategoryId == c.Id && matches.Contains(a.Id)), Children(c.Id, null).Count))
            .Where(c => term.Length == 0 || c.MatchCount > 0).ToList();
    }

    private List<AccountListDto> Children(int category, int? parent) => branches.GetValueOrDefault((category, parent ?? 0)) ?? new();
    public bool HasCategory(int id) => Categories.Any(c => c.Id == id);
    public bool HasParent(int category, int? parent) => !parent.HasValue || (accounts.TryGetValue(parent.Value, out var a) && a.CategoryId == category);
    public AccountTreeBranch Branch(int category, int? parent, int offset, int limit)
    {
        var children = Children(category, parent);
        return new(children.Skip(offset).Take(limit).Select(a => new AccountTreeItem(a, Children(category, a.Id).Count, matches.Contains(a.Id))).ToList(), children.Count);
    }
    public IReadOnlyList<AccountListDto> Export(int? category)
    {
        var result = new List<AccountListDto>();
        foreach (var c in Categories.Where(c => !category.HasValue || c.Id == category))
        {
            var pending = new Stack<AccountListDto>(Children(c.Id, null).AsEnumerable().Reverse());
            while (pending.TryPop(out var account))
            {
                result.Add(account);
                foreach (var child in Children(c.Id, account.Id).AsEnumerable().Reverse()) pending.Push(child);
            }
        }
        return result;
    }
}
