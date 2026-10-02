using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.ReferenceData;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class PaymentModeService : ReferenceDataService<PaymentMode, int, PaymentModeWriteRequest, PaymentModeDetailDto>
{
    public PaymentModeService(negosuiteContext db) : base(db) { }
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string> { ["id"] = "Id", ["name"] = "Name", ["isActive"] = "IsActive" };
    protected override Expression<Func<PaymentMode, PaymentModeDetailDto>> Projection => ReferenceDataMapping.PaymentModeProjection;
    protected override string[] ReferenceColumns => new[] { "PaymentModeId" };
    protected override void Apply(PaymentModeWriteRequest input, PaymentMode entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<PaymentMode> Filter(IQueryable<PaymentMode> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        if (options.Status.HasValue) query = query.Where(e => e.IsActive == options.Status);
        else if (!filter.ShowInactive) query = query.Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term)); }
        return query;
    }
    protected override Task ValidateAsync(PaymentModeWriteRequest input, CancellationToken ct)
    { Text(input.Name, "Name", 100, true); return Task.CompletedTask; }
}

public sealed class PaymentTermService : ReferenceDataService<PaymentTerm, int, PaymentTermWriteRequest, PaymentTermDetailDto>
{
    public PaymentTermService(negosuiteContext db) : base(db) { }
    protected override bool SuppliedId => true;
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["id"] = "Id", ["name"] = "Name", ["code"] = "Code", ["days"] = "Days", ["isActive"] = "IsActive" };
    protected override Expression<Func<PaymentTerm, PaymentTermDetailDto>> Projection => ReferenceDataMapping.PaymentTermProjection;
    protected override string[] ReferenceColumns => new[] { "PaymentTermId" };
    protected override void Apply(PaymentTermWriteRequest input, PaymentTerm entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<PaymentTerm> Filter(IQueryable<PaymentTerm> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        if (options.Status.HasValue) query = query.Where(e => e.IsActive == options.Status);
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || e.Code.Contains(term)); }
        return query;
    }
    protected override Task ValidateAsync(PaymentTermWriteRequest input, CancellationToken ct)
    { Text(input.Name, "Name", 100, true); Text(input.Code, "Code", 10, true); Require(input.Days == null || input.Days >= 0, "Days cannot be negative."); return Task.CompletedTask; }
}

public sealed class CurrencyService : ReferenceDataService<Currency, short, CurrencyWriteRequest, CurrencyDetailDto>
{
    public CurrencyService(negosuiteContext db) : base(db) { }
    protected override bool SuppliedId => true;
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["id"] = "Id", ["name"] = "Name", ["code"] = "Code", ["altCode"] = "AltCode", ["exchangeRate"] = "ExchangeRate", ["isBase"] = "IsBase" };
    protected override Expression<Func<Currency, CurrencyDetailDto>> Projection => ReferenceDataMapping.CurrencyProjection;
    protected override string[] ReferenceColumns => new[] { "CurrencyId", "BaseCurrencyId" };
    protected override void Apply(CurrencyWriteRequest input, Currency entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<Currency> Filter(IQueryable<Currency> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || e.Code.Contains(term) || (e.AltCode != null && e.AltCode.Contains(term))); }
        return query;
    }
    public Task<CurrencyDetailDto> BaseAsync(CancellationToken ct) => Db.Currencies.AsNoTracking().Where(e => e.IsBase).OrderBy(e => e.Id).Select(Projection).FirstOrDefaultAsync(ct);
    protected override Task ValidateAsync(CurrencyWriteRequest input, CancellationToken ct)
    {
        Text(input.Name, "Name", 40, true); Text(input.Code, "Code", 10, true); Text(input.AltCode, "AltCode", 15);
        Require(input.ExchangeRate >= 0.0001m && input.ExchangeRate <= 999999.9999m, "ExchangeRate must be between 0.0001 and 999999.9999."); return Task.CompletedTask;
    }
}

public sealed class CountryService : ReferenceDataService<Country, int, CountryWriteRequest, CountryDetailDto>
{
    public CountryService(negosuiteContext db) : base(db) { }
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string> { ["id"] = "Id", ["name"] = "Name", ["code"] = "Code" };
    protected override Expression<Func<Country, CountryDetailDto>> Projection => ReferenceDataMapping.CountryProjection;
    protected override string[] ReferenceColumns => new[] { "CountryId" };
    protected override void Apply(CountryWriteRequest input, Country entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<Country> Filter(IQueryable<Country> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || e.Code.Contains(term)); }
        return query;
    }
    protected override Task ValidateAsync(CountryWriteRequest input, CancellationToken ct)
    { Text(input.Name, "Name", 150, true); Text(input.Code, "Code", 10, true); return Task.CompletedTask; }
}

public sealed class CityMunicipalityService : ReferenceDataService<CityMunicipality, int, CityMunicipalityWriteRequest, CityMunicipalityDetailDto>
{
    public CityMunicipalityService(negosuiteContext db) : base(db) { }
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["id"] = "Id", ["name"] = "Name", ["stateProvinceId"] = "StateProvinceId", ["stateProvinceName"] = "StateProvince.Name", ["postalCode"] = "PostalCode" };
    protected override Expression<Func<CityMunicipality, CityMunicipalityDetailDto>> Projection => ReferenceDataMapping.CityMunicipalityProjection;
    protected override string[] ReferenceColumns => new[] { "CityMunicipalityId" };
    protected override void Apply(CityMunicipalityWriteRequest input, CityMunicipality entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<CityMunicipality> Filter(IQueryable<CityMunicipality> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        Require(filter.StateProvinceId == null || filter.StateProvinceId > 0, "StateProvinceId must be positive.");
        Require(filter.CountryId == null || filter.CountryId > 0, "CountryId must be positive.");
        if (filter.StateProvinceId.HasValue) query = query.Where(e => e.StateProvinceId == filter.StateProvinceId);
        if (filter.CountryId.HasValue) query = query.Where(e => e.StateProvince.CountryId == filter.CountryId);
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || e.StateProvince.Name.Contains(term) || (e.PostalCode != null && e.PostalCode.Contains(term))); }
        return query;
    }
    protected override Task<List<object>> ListItemsAsync(IQueryable<CityMunicipality> query, CancellationToken ct) => ProjectAsync(query, e => new CityMunicipalityListDto
    { Id = e.Id, Name = e.Name, StateProvinceId = e.StateProvinceId, StateProvinceName = e.StateProvince.Name, SelectOptionName = e.Name + ", " + e.StateProvince.Name, PostalCode = e.PostalCode }, ct);
    protected override async Task ValidateAsync(CityMunicipalityWriteRequest input, CancellationToken ct)
    {
        Text(input.Name, "Name", 150, true); Text(input.PostalCode, "PostalCode", 20);
        Require(await Db.StateProvinces.AnyAsync(e => e.Id == input.StateProvinceId, ct), "State province does not exist.");
    }
}

public sealed class IndustryService : ReferenceDataService<Industry, int, IndustryWriteRequest, IndustryDetailDto>
{
    public IndustryService(negosuiteContext db) : base(db) { }
    protected override bool LegacyOrdered => true;
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string> { ["id"] = "Id", ["name"] = "Name" };
    protected override Expression<Func<Industry, IndustryDetailDto>> Projection => ReferenceDataMapping.IndustryProjection;
    protected override string[] ReferenceColumns => new[] { "IndustryId" };
    protected override void Apply(IndustryWriteRequest input, Industry entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<Industry> Filter(IQueryable<Industry> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term)); }
        return query;
    }
    protected override Task<List<object>> ListItemsAsync(IQueryable<Industry> query, CancellationToken ct) => ProjectAsync(query, e => new IndustryListDto { Id = e.Id, Name = e.Name }, ct);
    protected override Task ValidateAsync(IndustryWriteRequest input, CancellationToken ct)
    { Text(input.Name, "Name", 150, true); return Task.CompletedTask; }
}

public sealed class NavigationItemService : ReferenceDataService<NavigationItem, int, NavigationItemWriteRequest, NavigationItemDetailDto>
{
    public NavigationItemService(negosuiteContext db) : base(db) { }
    protected override bool SuppliedId => true;
    protected override string DefaultSort => "title";
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["id"] = "Id", ["title"] = "Title", ["subtitle"] = "Subtitle", ["type"] = "Type", ["link"] = "Link", ["icon"] = "Icon" };
    protected override Expression<Func<NavigationItem, NavigationItemDetailDto>> Projection => ReferenceDataMapping.NavigationItemProjection;
    protected override void Apply(NavigationItemWriteRequest input, NavigationItem entity) => ReferenceDataMapping.Apply(input, entity);
    protected override IQueryable<NavigationItem> Filter(IQueryable<NavigationItem> query, AdministrationListOptions options, ReferenceDataFilter filter)
    {
        query = query.Where(e => e.ParentId == null);
        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var term = options.Search.Trim();
            query = query.Where(e => e.Title.Contains(term) || (e.Subtitle != null && e.Subtitle.Contains(term)) || (e.Link != null && e.Link.Contains(term)) ||
                e.Children.Any(c => c.Title.Contains(term) || (c.Subtitle != null && c.Subtitle.Contains(term)) || (c.Link != null && c.Link.Contains(term))));
        }
        return query;
    }
    protected override Task<List<object>> ListItemsAsync(IQueryable<NavigationItem> query, CancellationToken ct) => ProjectAsync(query, e => new NavigationRootDto
    {
        Id = e.Id.ToString(), Title = e.Title, Subtitle = e.Subtitle, Type = e.Type, Link = e.Link, Icon = e.Icon,
        Children = e.Children.OrderBy(c => c.Id).Select(c => new NavigationChildDto
        { Id = c.Id.ToString(), Title = c.Title, Subtitle = c.Subtitle, Type = c.Type, Link = c.Link, Icon = c.Icon }).ToList()
    }, ct);
    // Serialize hierarchy changes so two concurrent reparentings cannot introduce a cycle.
    protected override async Task LockAsync(CancellationToken ct) => await Db.NavigationItems
        .FromSqlRaw("SELECT * FROM navigationitem ORDER BY Id FOR UPDATE").AsNoTracking().ToListAsync(ct);
    protected override async Task ValidateAsync(NavigationItemWriteRequest input, CancellationToken ct)
    {
        Text(input.Title, "Title", 100, true); Text(input.Subtitle, "Subtitle", 100); Text(input.Type, "Type", 100);
        Text(input.Link, "Link", 100); Text(input.Icon, "Icon", 100);
        var parents = await Db.NavigationItems.AsNoTracking().Select(e => new { e.Id, e.ParentId }).ToDictionaryAsync(e => e.Id, e => e.ParentId, ct);
        var visited = new HashSet<int> { input.Id }; var parent = input.ParentId;
        while (parent.HasValue)
        {
            Require(visited.Add(parent.Value), "Navigation hierarchy cannot contain a cycle.");
            Require(parents.ContainsKey(parent.Value), "Parent navigation item does not exist."); parent = parents[parent.Value];
        }
    }
    protected override async Task CheckDeleteAsync(NavigationItem entity, int id, CancellationToken ct) =>
        Require(!await Db.NavigationItems.AnyAsync(e => e.ParentId == id, ct), "Cannot delete navigation with child items.", 409);
}
