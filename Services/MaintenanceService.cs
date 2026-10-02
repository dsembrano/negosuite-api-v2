using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Maintenance;
using negosuite_api.Models;
using Newtonsoft.Json;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public static class MaintenanceServiceCriteria
{
    public static MaintenanceCriteria Parse(string json)
    {
        MaintenanceCriteria criteria;
        try { criteria = string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<MaintenanceCriteria>(json); }
        catch (JsonException) { throw new AdministrationException("Invalid criteria JSON."); }
        Require(criteria?.UserConfigId != null, "criteria.userConfigId is required."); return criteria;
    }
}

public abstract class MaintenanceService<TEntity, TWrite, TDto> where TEntity : class, new() where TWrite : MaintenanceWriteRequest
{
    protected readonly negosuiteContext Db;
    protected MaintenanceService(negosuiteContext db) => Db = db;
    protected IQueryable<TEntity> Scoped(int company) => Db.Set<TEntity>().Where(e => EF.Property<int>(e, "UserConfigId") == company);
    protected virtual IQueryable<TEntity> Reads(int company) => Scoped(company).AsNoTracking();
    public abstract IReadOnlyDictionary<string, string> SortFields { get; }
    protected virtual string DefaultSort => "name";
    protected virtual bool LegacyOrdered => false;
    protected abstract TDto Map(TEntity entity);
    protected abstract void Apply(TWrite input, TEntity entity);
    protected abstract IQueryable<TEntity> Filter(IQueryable<TEntity> query, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType);
    protected abstract Task ValidateAsync(int company, TWrite input, CancellationToken ct);
    protected abstract Task CheckDeleteAsync(int company, int id, CancellationToken ct);
    protected virtual async Task<List<object>> ListItemsAsync(IQueryable<TEntity> query, CancellationToken ct) =>
        (await query.ToListAsync(ct)).Select(e => (object)Map(e)).ToList();

    public async Task<object> ListAsync(int company, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType, CancellationToken ct)
    {
        Validate(options, SortFields);
        var query = Filter(Reads(company), criteria, options, byType);
        var count = options.PageNumber.HasValue ? await query.CountAsync(ct) : 0;
        if (LegacyOrdered || options.PageNumber.HasValue || options.SortBy != null || options.SortDirection != null)
            query = Sort(query, options, SortFields, DefaultSort);
        if (options.PageNumber.HasValue) query = query.Skip((options.PageNumber.Value - 1) * options.PageSize.Value).Take(options.PageSize.Value);
        var items = await ListItemsAsync(query, ct);
        return options.PageNumber.HasValue ? new PagedResult<object>(items, options.PageNumber.Value, options.PageSize.Value, count) : items;
    }

    public async Task<TDto> GetAsync(int company, int id, CancellationToken ct) => Map(await Reads(company).SingleOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct));

    private void Stamp(TEntity entity, User actor, bool create)
    {
        var entry = Db.Entry(entity);
        if (create) { entry.Property("UserConfigId").CurrentValue = actor.ConfigId.Value; entry.Property("CreatedDate").CurrentValue = DateTime.UtcNow; entry.Property("CreatedByUserId").CurrentValue = actor.Id; }
        else { entry.Property("LastUpdatedDate").CurrentValue = DateTime.UtcNow; entry.Property("LastUpdatedByUserId").CurrentValue = actor.Id; }
    }

    public async Task<TDto> SaveAsync(User actor, int? id, TWrite input, CancellationToken ct)
    {
        var company = Company(actor); Require(input.UserConfigId == company, "Company does not match membership.", 403);
        var entity = id.HasValue ? await Scoped(company).SingleOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct) : new TEntity();
        Require(entity != null, "Record not found.", 404);
        await ValidateAsync(company, input, ct); Apply(input, entity); Stamp(entity, actor, !id.HasValue);
        if (!id.HasValue) Db.Add(entity);
        try { await Db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new AdministrationException("Record changed or no longer exists.", 409); }
        return await GetAsync(company, (int)Db.Entry(entity).Property("Id").CurrentValue, ct);
    }

    public async Task SaveManyAsync(User actor, List<TWrite> input, CancellationToken ct)
    {
        var company = Company(actor);
        Require(input != null && input.All(e => e != null), "A non-null array of records is required.");
        Require(input.All(e => e.UserConfigId == company), "Every record must belong to the selected company.", 403);
        Require(input.All(e => e.Id >= 0 && !(e.Id == 0 && e.Deleted == true)), "Invalid ID or deletion of an unsaved record.");
        var ids = input.Where(e => e.Id != 0).Select(e => e.Id).ToArray();
        Require(ids.Distinct().Count() == ids.Length, "Duplicate IDs in bulk request.");
        await using var tx = await Db.Database.BeginTransactionAsync(ct);
        var existing = await Scoped(company).Where(e => ids.Contains(EF.Property<int>(e, "Id"))).ToDictionaryAsync(e => (int)Db.Entry(e).Property("Id").CurrentValue, ct);
        Require(existing.Count == ids.Length, "One or more records were not found in this company.", 404);
        // Validate the whole request before modifying tracked records.
        foreach (var row in input)
        {
            if (row.Deleted == true) await CheckDeleteAsync(company, row.Id, ct);
            else await ValidateAsync(company, row, ct);
        }
        foreach (var row in input)
        {
            if (row.Deleted == true) { Db.Remove(existing[row.Id]); continue; }
            var entity = row.Id == 0 ? new TEntity() : existing[row.Id];
            Apply(row, entity); Stamp(entity, actor, row.Id == 0);
            if (row.Id == 0) Db.Add(entity);
        }
        try { await Db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1451 })
        { throw new AdministrationException("Cannot delete a record that is currently in use.", 409); }
    }

    public async Task DeleteAsync(User actor, int id, CancellationToken ct)
    {
        var company = Company(actor);
        var entity = await Scoped(company).SingleOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);
        Require(entity != null, "Record not found.", 404);
        await CheckDeleteAsync(company, id, ct); Db.Remove(entity); await Db.SaveChangesAsync(ct);
    }

    protected void Name(TWrite input, int max)
    { Require(!string.IsNullOrWhiteSpace(input.Name) && input.Name.Length <= max, $"Name is required and must not exceed {max} characters."); }
    protected void DecimalValue(decimal? value, string field) => Require(value == null ||
        (value >= -9999999999999999.9999m && value <= 9999999999999999.9999m), $"{field} exceeds the supported decimal range.");
    protected async Task AccountAsync(int company, int? id, CancellationToken ct)
    { if (id.HasValue) Require(await Db.Accounts.AnyAsync(a => a.Id == id && a.UserConfigId == company, ct), "Account must belong to this company."); }
}
