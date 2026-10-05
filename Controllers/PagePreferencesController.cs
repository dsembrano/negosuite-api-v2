using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Models;

namespace negosuite_api.Controllers;

public class PagePreferenceRequest
{
    public long Version { get; set; }
    public Dictionary<string, bool> Columns { get; set; }
}
public record PagePreferenceResponse(long Version, Dictionary<string, bool> Columns);

[ApiController, Authorize, TypeFilter(typeof(ConfigUuidFilter))]
[Route("api/me/page-preferences")]
public class PagePreferencesController : ControllerBase
{
    private readonly negosuiteContext db;
    private static readonly HashSet<string> AccountColumns = new(StringComparer.Ordinal) { "code", "categoryName", "parentAccountCode", "parentAccountName", "requireCustomer", "requireSupplier", "type" };
    private static readonly HashSet<string> AccountCategoryColumns = new(StringComparer.Ordinal) { "type", "accountCodePrefix", "orderNo", "accountCount" };
    private static readonly HashSet<string> GeneralJournalColumns = new(StringComparer.Ordinal) { "referenceDate", "notes", "statusName" };
    private static readonly HashSet<string> ReceivingReportColumns = new(StringComparer.Ordinal) { "referenceDate", "supplierName", "amount", "balance", "deliveryReceiptNo", "purchaseOrderNo", "inventoryLocationName", "notes", "statusName" };
    private static readonly HashSet<string> StockIssuanceColumns = new(StringComparer.Ordinal) { "referenceDate", "customerName", "inventoryLocationName", "notes", "statusName" };
    private static readonly HashSet<string> StockTransferColumns = new(StringComparer.Ordinal) { "referenceDate", "fromInventoryLocationName", "toInventoryLocationName", "notes", "statusName" };
    private static readonly HashSet<string> InventoryAdjustmentColumns = new(StringComparer.Ordinal) { "referenceDate", "customerName", "inventoryLocationName", "notes", "statusName" };
    private static readonly HashSet<string> CustomerColumns = new(StringComparer.Ordinal)
        { "contact", "address", "tin", "taxRateName", "paymentTermName", "creditLimit", "status" };
    private static readonly HashSet<string> SupplierColumns = new(StringComparer.Ordinal)
        { "contact", "address", "tin", "taxRateName", "paymentTermName", "status" };
    private static readonly HashSet<string> ItemColumns = new(StringComparer.Ordinal)
        { "code", "itemCategoryName", "typeName", "unit", "rate", "cost", "toSell", "toPurchase", "trackInventory", "reorderPoint", "status" };
    private static readonly HashSet<string> ItemCategoryColumns = new(StringComparer.Ordinal) { "status" };
    private static readonly HashSet<string> BillColumns = new(StringComparer.Ordinal)
        { "billDate", "dueDate", "supplierName", "supplierTIN", "amount", "balance", "paymentTermName", "notes", "statusName" };
    private static readonly HashSet<string> PaymentColumns = new(StringComparer.Ordinal)
        { "referenceDate", "supplierName", "customerrName", "payee", "paymentModeName", "paidThroughAccountName", "checkNo", "amount", "balance", "notes", "statusName" };
    private static readonly HashSet<string> SalesReceiptColumns = new(StringComparer.Ordinal)
        { "receiptDate", "customerName", "customerTIN", "billingAddress", "billingContactName", "billingContactEmail",
          "shippingAddress", "shippingContactName", "shippingContactEmail", "amount", "balance", "paymentModeName", "notes", "createdDate", "statusName" };
    private static readonly HashSet<string> SalesInvoicePaymentColumns = new(StringComparer.Ordinal)
        { "referenceDate", "customerName", "paymentModeName", "depositToAccountName", "amount", "balance", "notes", "statusName" };
    private static readonly HashSet<string> SalesInvoiceColumns = new(StringComparer.Ordinal)
        { "invoiceDate", "dueDate", "purchaseOrderNo", "customerName", "customerTIN", "billingAddress", "billingContactName", "billingContactEmail",
          "shippingAddress", "shippingContactName", "shippingContactEmail", "amount", "balance", "paymentTermName", "notes", "statusName" };

    public PagePreferencesController(negosuiteContext db) => this.db = db;

    [HttpGet("{pageKey}")]
    public async Task<ActionResult<PagePreferenceResponse>> Get(string pageKey, CancellationToken ct)
    {
        var scope = await Scope(pageKey, ct);
        if (scope.Error != null) return scope.Error;
        var preference = await db.UserPagePreferences.AsNoTracking().SingleOrDefaultAsync(p =>
            p.UserId == scope.UserId && p.CompanyId == scope.CompanyId && p.PageKey == pageKey, ct);
        return preference == null ? new PagePreferenceResponse(0, new()) : new PagePreferenceResponse(preference.Version, Normalize(pageKey, JsonSerializer.Deserialize<Dictionary<string, bool>>(preference.ColumnsJson)));
    }

    [HttpPut("{pageKey}")]
    public async Task<ActionResult<PagePreferenceResponse>> Put(string pageKey, PagePreferenceRequest request, CancellationToken ct)
    {
        var scope = await Scope(pageKey, ct);
        if (scope.Error != null) return scope.Error;
        if (request?.Columns == null || request.Columns.Count > 128 || request.Columns.Keys.Any(k => k.Length > 64) || request.Version < 0 || request.Version == long.MaxValue)
            return BadRequest("Supply a valid preference version and column visibility map.");
        var columns = Normalize(pageKey, request.Columns);
        var json = JsonSerializer.Serialize(columns);
        if (request.Version == 0)
        {
            db.UserPagePreferences.Add(new()
            {
                UserId = scope.UserId,
                CompanyId = scope.CompanyId,
                PageKey = pageKey,
                ColumnsJson = json,
                Version = 1,
                UpdatedAtUtc = DateTime.UtcNow
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 }) { return Changed(); }
        }
        else
        {
            var updated = await db.UserPagePreferences.Where(p => p.UserId == scope.UserId && p.CompanyId == scope.CompanyId &&
                    p.PageKey == pageKey && p.Version == request.Version)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.ColumnsJson, json)
                    .SetProperty(p => p.Version, request.Version + 1).SetProperty(p => p.UpdatedAtUtc, DateTime.UtcNow), ct);
            if (updated == 0) return Changed();
        }
        // Empty overrides restore defaults while retaining the version to prevent stale writes.
        return new PagePreferenceResponse(request.Version + 1, columns);
    }

    private ConflictObjectResult Changed() => Conflict(new ProblemDetails
    {
        Status = 409,
        Title = "Column preferences changed",
        Detail = "Column choices changed in another session. Reload the preference version before saving again."
    });

    private static Dictionary<string, bool> Normalize(string pageKey, Dictionary<string, bool> columns) =>
        (columns ?? new()).Where(pair => (pageKey switch { "accounts" => AccountColumns, "account-categories" => AccountCategoryColumns, "general-journals" => GeneralJournalColumns, "receiving-reports" => ReceivingReportColumns, "stock-issuances" => StockIssuanceColumns, "stock-transfers" => StockTransferColumns, "inventory-adjustments" => InventoryAdjustmentColumns, "bills" => BillColumns, "cash-disbursements" => PaymentColumns, "payments" => PaymentColumns, "suppliers" => SupplierColumns, "items" => ItemColumns, "item-categories" => ItemCategoryColumns, "sales-returns" => new HashSet<string> { "referenceDate", "customerName", "sourceNo", "amount", "balance", "reason", "status" }, "sales-invoices" => SalesInvoiceColumns, "sales-receipts" => SalesReceiptColumns, "sales-invoice-payments" => SalesInvoicePaymentColumns, _ => CustomerColumns }).Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

    private async Task<(int UserId, int CompanyId, ActionResult Error)> Scope(string pageKey, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("negosuite_user_id")?.Value, out var userId) || userId <= 0)
            return (0, 0, Unauthorized()); // Existing claimless tokens refresh through the normal client flow.
        if (HttpContext.Items[ConfigUuidFilter.CompanyIdKey] is not int companyId) return (0, 0, Unauthorized());
        var moduleId = pageKey switch { "accounts" => "3210", "account-categories" => "3220", "general-journals" => "4310", "receiving-reports" => "4405", "stock-issuances" => "4430", "stock-transfers" => "4420", "inventory-adjustments" => "4410", "bills" => "4210", "cash-disbursements" => "4240", "payments" => "4240", "customers" => "3110", "suppliers" => "3120", "items" => "3130", "item-categories" => "3135", "sales-returns" => "4130", "sales-invoices" => "4110", "sales-receipts" => "4120", "sales-invoice-payments" => "4125", _ => null };
        if (moduleId == null) return (0, 0, NotFound());
        var user = await db.Users.AsNoTracking().Include(u => u.UserRole).SingleOrDefaultAsync(u => u.Id == userId && u.Status && u.ConfigId == companyId, ct);
        if (user == null) return (0, 0, Forbid());
        if (user.UserRole?.IsAdmin != true)
        {
            try
            {
                using var permissions = JsonDocument.Parse(user.UserRole?.Permission ?? "[]");
                bool Can(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
                var allowed = permissions.RootElement.ValueKind == JsonValueKind.Array && permissions.RootElement.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.Object && item.TryGetProperty("moduleId", out var module) && module.ValueKind == JsonValueKind.String && module.GetString() == moduleId &&
                    (Can(item, "canView") || Can(item, "canCreate") || Can(item, "canEdit")));
                if (!allowed) return (0, 0, Forbid());
            }
            catch (JsonException) { return (0, 0, Forbid()); }
        }
        return (userId, companyId, null);
    }
}
