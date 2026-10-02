using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.Customers;
using negosuite_api.Models;
using Newtonsoft.Json;

namespace negosuite_api.Services;

public sealed class AdministrationException : Exception
{
    public int Status { get; }
    public AdministrationException(string message, int status = 400) : base(message) => Status = status;
}

public static class AdministrationSupport
{
    public static void Require(bool condition, string message, int status = 400)
    { if (!condition) throw new AdministrationException(message, status); }
    public static void Admin(User actor) => Require(CompanyAccessService.IsAdmin(actor), "Company administrator permission is required.", 403);
    public static int Company(User actor)
    { Require(actor?.ConfigId != null, "A company must be configured.", 403); return actor.ConfigId.Value; }

    public static void Criteria(string criteria, int company)
    {
        AdministrationCriteria value;
        try { value = string.IsNullOrWhiteSpace(criteria) ? null : JsonConvert.DeserializeObject<AdministrationCriteria>(criteria); }
        catch (JsonException) { throw new AdministrationException("Invalid criteria JSON."); }
        Require(value?.UserConfigId != null, "criteria.userConfigId is required.");
        Require(value.UserConfigId == company, "Company does not match authenticated membership.", 403);
    }

    public static void Validate(AdministrationListOptions options, IReadOnlyDictionary<string, string> fields)
    {
        Require(CustomerPagination.IsValid(options.PageNumber, options.PageSize), "Supply both pageNumber (1 or greater) and pageSize (1 to 200), within the supported offset range.");
        Require(options.SortBy == null || fields.ContainsKey(options.SortBy), "Unsupported sortBy.");
        Require(options.SortDirection is null or "asc" or "desc", "Unsupported sortDirection.");
    }

    public static IQueryable<T> Sort<T>(IQueryable<T> query, AdministrationListOptions options, IReadOnlyDictionary<string, string> fields, string defaultField)
    {
        Validate(options, fields);
        var parameter = Expression.Parameter(typeof(T), "row");
        Expression property = parameter;
        foreach (var part in fields[options.SortBy ?? defaultField].Split('.')) property = Expression.Property(property, part);
        var key = Expression.Lambda(property, parameter);
        var ordered = Expression.Call(typeof(Queryable), options.SortDirection == "desc" ? "OrderByDescending" : "OrderBy",
            new[] { typeof(T), property.Type }, query.Expression, Expression.Quote(key));
        var id = Expression.Property(parameter, "Id");
        var stable = Expression.Call(typeof(Queryable), "ThenBy", new[] { typeof(T), id.Type }, ordered, Expression.Quote(Expression.Lambda(id, parameter)));
        return query.Provider.CreateQuery<T>(stable);
    }

    public static async Task<object> PageAsync<T, TDto>(IQueryable<T> query, AdministrationListOptions options,
        IReadOnlyDictionary<string, string> fields, string defaultField, Func<T, TDto> map, CancellationToken ct)
    {
        Validate(options, fields);
        var count = options.PageNumber.HasValue ? await query.CountAsync(ct) : 0;
        query = Sort(query, options, fields, defaultField);
        if (options.PageNumber.HasValue) query = query.Skip((options.PageNumber.Value - 1) * options.PageSize.Value).Take(options.PageSize.Value);
        var items = (await query.ToListAsync(ct)).Select(map).ToList();
        return options.PageNumber.HasValue ? new PagedResult<TDto>(items, options.PageNumber.Value, options.PageSize.Value, count) : items;
    }

    public static async Task<IDbContextTransaction> LockCompanyAsync(negosuiteContext db, int company, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try { await db.Configs.FromSqlInterpolated($"SELECT * FROM config WHERE Id = {company} FOR UPDATE").AsNoTracking().ToListAsync(ct); return transaction; }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public static async Task PreserveAdminAsync(negosuiteContext db, int company, int? excludedUser, int? excludedRole, CancellationToken ct)
    {
        Require(await db.Users.AnyAsync(u => u.ConfigId == company && u.Status &&
            (!excludedUser.HasValue || u.Id != excludedUser) && (!excludedRole.HasValue || u.UserRoleId != excludedRole) &&
            db.UserRoles.Any(r => r.Id == u.UserRoleId && r.IsAdmin && (r.UserConfigId == null || r.UserConfigId == company)), ct),
            "The company must retain an active administrator.");
    }

    public static void Lengths<T>(negosuiteContext db, object input)
    {
        foreach (var property in db.Model.FindEntityType(typeof(T)).GetProperties())
        {
            var max = property.GetMaxLength();
            if (max.HasValue && input.GetType().GetProperty(property.Name)?.GetValue(input) is string value)
                Require(value.Length <= max, $"{property.Name} must not exceed {max} characters.");
        }
    }
}
