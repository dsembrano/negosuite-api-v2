using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.ReferenceData;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

// These catalogues are shared across companies. Do not apply a UserConfigId predicate.
public abstract class ReferenceDataService<TEntity, TKey, TWrite, TDto>
    where TEntity : class, new() where TKey : struct where TWrite : ReferenceWriteRequest<TKey>
{
    protected readonly negosuiteContext Db;
    protected ReferenceDataService(negosuiteContext db) => Db = db;
    public abstract IReadOnlyDictionary<string, string> SortFields { get; }
    protected abstract Expression<Func<TEntity, TDto>> Projection { get; }
    protected abstract void Apply(TWrite input, TEntity entity);
    protected abstract IQueryable<TEntity> Filter(IQueryable<TEntity> query, AdministrationListOptions options, ReferenceDataFilter filter);
    protected abstract Task ValidateAsync(TWrite input, CancellationToken ct);
    protected virtual bool SuppliedId => false;
    protected virtual bool LegacyOrdered => false;
    protected virtual string DefaultSort => "name";
    protected virtual string[] ReferenceColumns => Array.Empty<string>();
    protected virtual Task LockAsync(CancellationToken ct) => Task.CompletedTask;
    protected virtual Task<List<object>> ListItemsAsync(IQueryable<TEntity> query, CancellationToken ct) => ProjectAsync(query, Projection, ct);
    protected static async Task<List<object>> ProjectAsync<TList>(IQueryable<TEntity> query, Expression<Func<TEntity, TList>> projection, CancellationToken ct) =>
        (await query.Select(projection).ToListAsync(ct)).Cast<object>().ToList();

    public async Task<object> ListAsync(AdministrationListOptions options, ReferenceDataFilter filter, CancellationToken ct)
    {
        Validate(options, SortFields);
        var query = Filter(Db.Set<TEntity>().AsNoTracking(), options, filter);
        var count = options.PageNumber.HasValue ? await query.CountAsync(ct) : 0;
        if (LegacyOrdered || options.PageNumber.HasValue || options.SortBy != null || options.SortDirection != null)
            query = Sort(query, options, SortFields, DefaultSort);
        if (options.PageNumber.HasValue) query = query.Skip((options.PageNumber.Value - 1) * options.PageSize.Value).Take(options.PageSize.Value);
        var items = await ListItemsAsync(query, ct);
        return options.PageNumber.HasValue ? new PagedResult<object>(items, options.PageNumber.Value, options.PageSize.Value, count) : items;
    }

    public Task<TDto> GetAsync(TKey id, CancellationToken ct) => Db.Set<TEntity>().AsNoTracking()
        .Where(e => EF.Property<TKey>(e, "Id").Equals(id)).Select(Projection).SingleOrDefaultAsync(ct);

    public async Task<TDto> SaveAsync(User actor, TKey? id, TWrite input, CancellationToken ct)
    {
        Require(id.HasValue ? id.Value.Equals(input.Id) : SuppliedId ? Convert.ToInt64(input.Id) > 0 : Convert.ToInt64(input.Id) >= 0,
            id.HasValue ? "Route/body IDs must match." : "Supply a valid record ID.");
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var entity = id.HasValue ? await Db.Set<TEntity>().FindAsync(new object[] { id.Value }, ct) : new TEntity();
        Require(entity != null, "Record not found.", 404);
        await ValidateAsync(input, ct);
        Apply(input, entity);
        var entry = Db.Entry(entity);
        if (!id.HasValue) entry.Property("Id").CurrentValue = input.Id;
        // Not all shared catalogues have audit columns.
        foreach (var (property, value) in id.HasValue
            ? new (string, object)[] { ("LastUpdatedDate", DateTime.UtcNow), ("LastUpdatedByUserId", actor.Id) }
            : new (string, object)[] { ("CreatedDate", DateTime.UtcNow), ("CreatedByUserId", actor.Id) })
            if (entry.Metadata.FindProperty(property) != null) entry.Property(property).CurrentValue = value;
        if (!id.HasValue) Db.Add(entity);
        try { await Db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AdministrationException("Record changed or no longer exists.", 409); }
        return await GetAsync((TKey)entry.Property("Id").CurrentValue, ct);
    }

    protected virtual async Task CheckDeleteAsync(TEntity entity, int id, CancellationToken ct) => Require(
        !await new MaintenanceReferences(Db).UsedAsync(id, ReferenceColumns, Array.Empty<string>(), ct), "Cannot delete a record that is currently in use.", 409);

    public async Task DeleteAsync(TKey id, CancellationToken ct)
    {
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        await LockAsync(ct);
        var entity = await Db.Set<TEntity>().FindAsync(new object[] { id }, ct);
        Require(entity != null, "Record not found.", 404);
        await CheckDeleteAsync(entity, Convert.ToInt32(id), ct);
        Db.Remove(entity); await Db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    protected static void Text(string value, string field, int max, bool required = false) => Require(
        (!required || !string.IsNullOrWhiteSpace(value)) && (value == null || value.Length <= max), $"{field} must {(required ? "be supplied and " : "")}not exceed {max} characters.");
}
