using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Administration;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class ConfigService
{
    private readonly negosuiteContext db;
    public ConfigService(negosuiteContext db) => this.db = db;
    public static readonly IReadOnlyDictionary<string, string> SortFields = new Dictionary<string, string>
    { ["companyName"] = "CompanyName", ["email"] = "Email", ["createdDate"] = "CreatedDate" };

    public async Task<object> ListAsync(User actor, AdministrationListOptions options, CancellationToken ct)
    {
        var query = db.Configs.AsNoTracking().Where(c => c.Id == actor.ConfigId);
        if (!string.IsNullOrWhiteSpace(options.Search))
        { var term = options.Search.Trim(); query = query.Where(c => c.CompanyName.Contains(term) || (c.Email != null && c.Email.Contains(term))); }
        return await PageAsync(query, options, SortFields, "companyName", AdministrationMapping.ToDto, ct);
    }

    public Task<List<ConfigTemplateDto>> TemplatesAsync(CancellationToken ct) => db.Configs.AsNoTracking().Where(c => c.IsTemplate == true)
        .OrderBy(c => c.Id).Select(c => new ConfigTemplateDto { Id = c.Id, Name = c.CompanyName, About = c.CompanyAbout,
            DiscountAccountId = c.DiscountAccountId, PurchaseDiscountAccountId = c.PurchaseDiscountAccountId }).ToListAsync(ct);

    public async Task<ConfigDetailDto> GetAsync(User actor, int id, CancellationToken ct)
    {
        if (actor.ConfigId != id) return null;
        var config = await db.Configs.AsNoTracking().Include(c => c.Industry).Include(c => c.Country)
            .Include(c => c.ARTradeAccount).Include(c => c.APTradeAccount).Include(c => c.DiscountAccount).Include(c => c.PurchaseDiscountAccount)
            .SingleOrDefaultAsync(c => c.Id == id, ct);
        var dto = AdministrationMapping.ToDto(config);
        if (dto == null) return null;
        if (dto.ARTradeAccount?.UserConfigId != id) dto.ARTradeAccount = null;
        if (dto.APTradeAccount?.UserConfigId != id) dto.APTradeAccount = null;
        if (dto.DiscountAccount?.UserConfigId != id) dto.DiscountAccount = null;
        if (dto.PurchaseDiscountAccount?.UserConfigId != id) dto.PurchaseDiscountAccount = null;
        return dto;
    }

    public async Task UpdateAsync(User actor, int id, ConfigUpdateRequest input, CancellationToken ct)
    {
        Admin(actor); Require(actor.ConfigId == id, "Company not found.", 404);
        Require(!string.IsNullOrWhiteSpace(input.CompanyName), "Company name is required."); Lengths<Config>(db, input);
        foreach (var account in new[] { input.ARTradeAccountId, input.APTradeAccountId, input.DiscountAccountId, input.PurchaseDiscountAccountId }.Where(a => a.HasValue).Distinct())
            Require(await db.Accounts.AnyAsync(a => a.Id == account && a.UserConfigId == id, ct), "Configured accounts must belong to this company.");
        await ValidateLookupsAsync(input.CountryId, input.IndustryId, ct);
        var config = await db.Configs.SingleOrDefaultAsync(c => c.Id == id, ct); Require(config != null, "Company not found.", 404);
        AdministrationMapping.Apply(input, config); config.LastUpdatedDate = DateTime.UtcNow; config.LastUpdatedByUserId = actor.Id;
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateLookupsAsync(int? country, int? industry, CancellationToken ct)
    {
        if (country.HasValue) Require(await db.Countries.AnyAsync(c => c.Id == country, ct), "Country not found.");
        if (industry.HasValue) Require(await db.Industries.AnyAsync(c => c.Id == industry, ct), "Industry not found.");
    }

    public async Task SetPaymentAdjustmentsAsync(User actor, int id, string value, CancellationToken ct)
    {
        Admin(actor); Require(actor.ConfigId == id, "Company not found.", 404);
        var config = await db.Configs.SingleOrDefaultAsync(c => c.Id == id, ct); Require(config != null, "Company not found.", 404);
        config.PaymentAdjustmentTypes = value; config.LastUpdatedDate = DateTime.UtcNow; config.LastUpdatedByUserId = actor.Id;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(User actor, int id, CancellationToken ct)
    {
        Admin(actor); Require(actor.ConfigId == id, "Company not found.", 404);
        // Every accessible company has at least its current member. Do not leave orphaned users/data.
        Require(!await db.Users.AnyAsync(u => u.ConfigId == id, ct), "A company with members cannot be deleted.");
        throw new AdministrationException("Company deletion requires a separate data-retention workflow.");
    }

    public async Task<CompanySetupResponse> SetupAsync(User actor, int userId, CompanySetupRequest input, bool fromTemplate, CancellationToken ct)
    {
        Require(actor.Id == userId, "Company setup is only available for your own account.", 403);
        Require(actor.ConfigId == null, "This account already belongs to a company.");
        Require(!string.IsNullOrWhiteSpace(input.CompanyName), "Company name is required."); Lengths<Config>(db, input);
        await ValidateLookupsAsync(input.CountryId, input.IndustryId, ct);
        var template = await db.Configs.AsNoTracking().Where(c => c.IsTemplate == true && (!fromTemplate || c.Id == input.Id)).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        Require(template != null, "Company template not found.");
        var admin = await db.UserRoles.AsNoTracking().Where(r => r.IsAdmin && r.UserConfigId == null).OrderBy(r => r.Id).FirstOrDefaultAsync(ct);
        Require(admin != null, "A shared administrator role is required for company setup.");
        var trial = input.Trial == true;
        SubscriptionPlan plan = null;
        if (!trial)
        {
            plan = await db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == input.SubscriptionPlanId && p.IsActive, ct);
            Require(plan != null && plan.MinimumUsers > 0 && plan.MinimumUsers <= 255, "Select a valid subscription plan.");
            Require(input.BillingMode is "M" or "Y", "Billing mode must be M or Y.");
        }
        var now = DateTime.UtcNow;
        // The legacy client uses a 14-day, one-seat trial. Limits/dates are now server-owned.
        var maxUsers = trial ? 1 : plan.MinimumUsers;
        var planId = plan?.Id; var subscriptionDate = now.ToString("yyyy-MM-dd");
        var trialEnd = trial ? now.AddDays(14).ToString("yyyy-MM-dd") : null;
        var lockName = "negosuite.company-setup." + actor.Id;
        await db.Database.OpenConnectionAsync(ct);
        var acquired = false;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT GET_LOCK(@name, 10)";
                var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = lockName; command.Parameters.Add(parameter);
                acquired = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
            }
            Require(acquired, "Another company setup is in progress. Try again.", 409);
            var current = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor.Id && u.Status, ct);
            Require(current != null && current.ConfigId == null, "This account already belongs to a company or is inactive.");
            // Keep procedure contracts and their own transaction handling. Advisory locking prevents duplicate setup per user.
            FormattableString sql = fromTemplate
                ? (FormattableString)$"CALL CreateUserConfigFromTemplate({actor.Id.ToString()}, {admin.Id.ToString()}, {template.Id.ToString()}, {input.CompanyName}, {input.Address1}, {input.PhoneNo}, {input.Email}, {input.Website}, {input.Tin}, {input.IndustryId?.ToString()}, {input.CompanyAbout}, {input.CountryId?.ToString()}, {input.TaxRatesJson}, {planId}, {subscriptionDate}, {trial}, {trialEnd}, {input.BillingMode}, {maxUsers.ToString()})"
                : $"CALL CreateUserConfig({actor.Id.ToString()}, {admin.Id.ToString()}, {template.Id.ToString()}, {input.CompanyName}, {input.Address1}, {input.PhoneNo}, {input.Email}, {input.Website}, {input.Tin}, {input.IndustryId?.ToString()}, {input.CompanyAbout}, {input.CountryId?.ToString()}, {input.TaxRatesJson}, {planId}, {subscriptionDate}, {trial}, {trialEnd}, {input.BillingMode}, {maxUsers.ToString()})";
            await db.Configs.FromSqlInterpolated(sql).AsNoTracking().ToListAsync(ct);
            var user = await db.Users.AsNoTracking().Include(u => u.Config).Include(u => u.UserRole).Include(u => u.UserType).SingleAsync(u => u.Id == actor.Id, ct);
            Require(user.ConfigId != null, "Company setup did not complete.", 409);
            return new CompanySetupResponse { Id = user.Id, Name = user.Name, Email = user.Email, MobileNo = user.MobileNo,
                UserTypeId = user.UserTypeId, UserTypeName = user.UserType?.Name, Avatar = user.Avatar, Status = user.Status,
                ConfigId = user.ConfigId, Config = AdministrationMapping.ToDto(user.Config), UserRoleId = user.UserRoleId,
                UserRole = AdministrationMapping.ToDto(user.UserRole), BaseCurrency = AdministrationMapping.ToDto(await db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.IsBase, ct)) };
        }
        finally
        {
            try
            {
                if (acquired)
                {
                    await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT RELEASE_LOCK(@name)";
                    var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = lockName; command.Parameters.Add(parameter);
                    await command.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
