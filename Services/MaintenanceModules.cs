using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.Maintenance;
using negosuite_api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class TaxRateService : MaintenanceService<TaxRate, TaxRateWriteRequest, TaxRateDetailDto>
{
    public TaxRateService(negosuiteContext db) : base(db) { }
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["name"] = "Name", ["rate"] = "Rate", ["applyToSalesOrPurchase"] = "ApplyToSalesOrPurchase", ["taxAccountName"] = "TaxAccount.Name", ["salesAccountName"] = "SalesAccount.Name" };
    protected override IQueryable<TaxRate> Reads(int company) => base.Reads(company).Include(e => e.TaxAccount).Include(e => e.SalesAccount);
    protected override TaxRateDetailDto Map(TaxRate entity) => MaintenanceMapping.ToDto(entity);
    protected override void Apply(TaxRateWriteRequest input, TaxRate entity) => MaintenanceMapping.Apply(input, entity);
    public async Task<int> ReadCompanyAsync(User actor, int company, CancellationToken ct)
    {
        // V1 company onboarding reads tax definitions from an explicitly marked template.
        Require(actor.ConfigId == company || await Db.Configs.AnyAsync(c => c.Id == company && c.IsTemplate == true, ct), "Company does not match membership.", 403);
        return company;
    }
    protected override IQueryable<TaxRate> Filter(IQueryable<TaxRate> query, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType)
    {
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) ||
            (e.ApplyToSalesOrPurchase != null && e.ApplyToSalesOrPurchase.Contains(term)) ||
            (e.TaxAccount.UserConfigId == e.UserConfigId && e.TaxAccount.Name.Contains(term)) || (e.SalesAccount.UserConfigId == e.UserConfigId && e.SalesAccount.Name.Contains(term))); }
        return query;
    }
    protected override async Task ValidateAsync(int company, TaxRateWriteRequest input, CancellationToken ct)
    {
        Name(input, 150); Require(input.ApplyToSalesOrPurchase == null || input.ApplyToSalesOrPurchase.Length <= 2, "ApplyToSalesOrPurchase must not exceed 2 characters.");
        DecimalValue(input.Rate, "Rate");
        await AccountAsync(company, input.TaxAccountId, ct); await AccountAsync(company, input.SalesAccountId, ct);
    }
    protected override async Task CheckDeleteAsync(int company, int id, CancellationToken ct) => Require(!await new MaintenanceReferences(Db)
        .UsedAsync(id, new[] { "TaxRateId", "SalesTaxRateId", "PurchaseTaxRateId" }, Array.Empty<string>(), ct), "Cannot delete Tax Rate because it is currently in use.", 409);
}

public sealed class DiscountTypeService : MaintenanceService<DiscountType, DiscountTypeWriteRequest, DiscountTypeDetailDto>
{
    public DiscountTypeService(negosuiteContext db) : base(db) { }
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["name"] = "Name", ["rate"] = "Rate", ["discountAmount"] = "DiscountAmount", ["discountIsBeforeTax"] = "DiscountIsBeforeTax", ["lockedRate"] = "LockedRate", ["discountAccountName"] = "DiscountAccount.Name", ["taxRateName"] = "TaxRate.Name" };
    protected override IQueryable<DiscountType> Reads(int company) => base.Reads(company).Include(e => e.DiscountAccount).Include(e => e.TaxRate);
    protected override DiscountTypeDetailDto Map(DiscountType entity) => MaintenanceMapping.ToDto(entity);
    protected override void Apply(DiscountTypeWriteRequest input, DiscountType entity) => MaintenanceMapping.Apply(input, entity);
    protected override IQueryable<DiscountType> Filter(IQueryable<DiscountType> query, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType)
    {
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) ||
            (e.DiscountAccount.UserConfigId == e.UserConfigId && e.DiscountAccount.Name.Contains(term)) || (e.TaxRate.UserConfigId == e.UserConfigId && e.TaxRate.Name.Contains(term))); }
        return query;
    }
    protected override async Task ValidateAsync(int company, DiscountTypeWriteRequest input, CancellationToken ct)
    {
        Name(input, 150); await AccountAsync(company, input.DiscountAccountId, ct);
        DecimalValue(input.Rate, "Rate"); DecimalValue(input.DiscountAmount, "DiscountAmount");
        if (input.TaxRateId.HasValue) Require(await Db.TaxRates.AnyAsync(t => t.Id == input.TaxRateId && t.UserConfigId == company, ct), "Tax rate must belong to this company.");
    }
    protected override async Task CheckDeleteAsync(int company, int id, CancellationToken ct) => Require(!await new MaintenanceReferences(Db)
        .UsedAsync(id, new[] { "DiscountTypeId" }, Array.Empty<string>(), ct), "Cannot delete Discount Type because it is currently in use.", 409);
}

public sealed class ResponsibilityCenterService : MaintenanceService<ResponsibilityCenter, ResponsibilityCenterWriteRequest, ResponsibilityCenterDetailDto>
{
    public ResponsibilityCenterService(negosuiteContext db) : base(db) { }
    protected override bool LegacyOrdered => true;
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["name"] = "Name", ["status"] = "Status", ["notes"] = "Notes", ["responsibilityCenterTypeId"] = "ResponsibilityCenterTypeId" };
    protected override ResponsibilityCenterDetailDto Map(ResponsibilityCenter entity) => MaintenanceMapping.ToDto(entity);
    protected override void Apply(ResponsibilityCenterWriteRequest input, ResponsibilityCenter entity) => MaintenanceMapping.Apply(input, entity);
    protected override IQueryable<ResponsibilityCenter> Filter(IQueryable<ResponsibilityCenter> query, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType)
    {
        if (byType) query = query.Where(e => e.ResponsibilityCenterTypeId == criteria.ResponsibilityCenterTypeId);
        if (options.Status.HasValue) query = query.Where(e => e.Status == options.Status);
        else if (byType && criteria.ShowInactive != true) query = query.Where(e => e.Status);
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || (e.Notes != null && e.Notes.Contains(term))); }
        return query;
    }
    protected override async Task ValidateAsync(int company, ResponsibilityCenterWriteRequest input, CancellationToken ct)
    { Name(input, 50); Require(await Db.ResponsibilityCenterTypes.AnyAsync(t => t.Id == input.ResponsibilityCenterTypeId && t.UserConfigId == company, ct), "Responsibility center type must belong to this company."); }
    protected override async Task CheckDeleteAsync(int company, int id, CancellationToken ct) => Require(!await new MaintenanceReferences(Db)
        .UsedAsync(id, new[] { "ResponsibilityCenterId" }, new[] { "ResponsibilityCenterEntry" }, ct), "Cannot delete this responsibility center as it is already in use.");
}

public sealed class ResponsibilityCenterTypeService : MaintenanceService<ResponsibilityCenterType, ResponsibilityCenterTypeWriteRequest, ResponsibilityCenterTypeDetailDto>
{
    public ResponsibilityCenterTypeService(negosuiteContext db) : base(db) { }
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["name"] = "Name", ["isActive"] = "IsActive", ["requiredBy"] = "RequiredBy" };
    protected override ResponsibilityCenterTypeDetailDto Map(ResponsibilityCenterType entity) => MaintenanceMapping.ToDto(entity);
    protected override void Apply(ResponsibilityCenterTypeWriteRequest input, ResponsibilityCenterType entity)
    { MaintenanceMapping.Apply(input, entity); if (string.IsNullOrWhiteSpace(entity.RequiredByTags)) entity.RequiredByTags = null; }
    protected override IQueryable<ResponsibilityCenterType> Filter(IQueryable<ResponsibilityCenterType> query, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType)
    {
        if (options.Status.HasValue) query = query.Where(e => e.IsActive == options.Status);
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || (e.RequiredBy != null && e.RequiredBy.Contains(term))); }
        return query;
    }
    protected override async Task ValidateAsync(int company, ResponsibilityCenterTypeWriteRequest input, CancellationToken ct)
    {
        Name(input, 150); Require(!string.IsNullOrWhiteSpace(input.RequiredBy) && input.RequiredBy.Length <= 45, "RequiredBy is required and must not exceed 45 characters.");
        if (string.IsNullOrWhiteSpace(input.RequiredByTags)) return;
        JArray tags;
        try { tags = JArray.Parse(input.RequiredByTags); }
        catch (JsonException) { throw new AdministrationException("RequiredByTags must be a JSON array of positive IDs."); }
        Require(tags.All(t => t.Type == JTokenType.Integer && int.TryParse(t.ToString(), out var id) && id > 0), "RequiredByTags must contain positive integer IDs.");
        var ids = tags.Select(t => t.Value<int>()).Distinct().ToArray();
        if (input.RequiredBy == "ACCTCAT") Require(await Db.AccountCategories.CountAsync(a => a.UserConfigId == company && ids.Contains(a.Id), ct) == ids.Length, "Account categories must belong to this company.");
        else if (input.RequiredBy == "SPECIFIC") Require(await Db.Accounts.CountAsync(a => a.UserConfigId == company && ids.Contains(a.Id), ct) == ids.Length, "Accounts must belong to this company.");
    }
    protected override async Task CheckDeleteAsync(int company, int id, CancellationToken ct) => Require(!await new MaintenanceReferences(Db)
        .UsedAsync(id, new[] { "ResponsibilityCenterTypeId" }, Array.Empty<string>(), ct), "Cannot delete a responsibility center type used by another record.");
}

public sealed class InventoryLocationService : MaintenanceService<InventoryLocation, InventoryLocationWriteRequest, InventoryLocationDetailDto>
{
    public InventoryLocationService(negosuiteContext db) : base(db) { }
    protected override bool LegacyOrdered => true;
    public override IReadOnlyDictionary<string, string> SortFields { get; } = new Dictionary<string, string>
    { ["code"] = "Code", ["name"] = "Name", ["status"] = "Status" };
    protected override InventoryLocationDetailDto Map(InventoryLocation entity) => MaintenanceMapping.ToDto(entity);
    protected override async Task<List<object>> ListItemsAsync(IQueryable<InventoryLocation> query, CancellationToken ct) =>
        (await query.Select(e => new InventoryLocationListDto { Id = e.Id, Code = e.Code, Name = e.Name, Status = e.Status }).ToListAsync(ct)).Cast<object>().ToList();
    protected override void Apply(InventoryLocationWriteRequest input, InventoryLocation entity) => MaintenanceMapping.Apply(input, entity);
    protected override IQueryable<InventoryLocation> Filter(IQueryable<InventoryLocation> query, MaintenanceCriteria criteria, AdministrationListOptions options, bool byType)
    {
        if (options.Status.HasValue) query = query.Where(e => e.Status == options.Status);
        else if (criteria.ShowInactive != true) query = query.Where(e => e.Status);
        if (!string.IsNullOrWhiteSpace(options.Search)) { var term = options.Search.Trim(); query = query.Where(e => e.Name.Contains(term) || (e.Code != null && e.Code.Contains(term))); }
        return query;
    }
    protected override Task ValidateAsync(int company, InventoryLocationWriteRequest input, CancellationToken ct)
    { Name(input, 50); Require(input.Code == null || input.Code.Length <= 10, "Code must not exceed 10 characters."); return Task.CompletedTask; }
    protected override async Task CheckDeleteAsync(int company, int id, CancellationToken ct) => Require(!await new MaintenanceReferences(Db)
        .UsedAsync(id, new[] { "InventoryLocationId", "FromInventoryLocationId", "ToInventoryLocationId" }, Array.Empty<string>(), ct), "Cannot delete an inventory location used by another record.");
}
