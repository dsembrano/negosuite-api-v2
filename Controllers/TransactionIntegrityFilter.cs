using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using negosuite_api.Services;
using static negosuite_api.Services.AdministrationSupport;
using static negosuite_api.Services.TransactionDocument;

namespace negosuite_api.Controllers;

// Runs after ConfigUuidFilter. All legacy aliases share the same lock and invariants.
// Sales Returns retain their existing versioned posting service and use the same company lock.
public sealed class TransactionIntegrityFilter(negosuiteContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var name = context.Controller.GetType().Name;
        var module = name switch
        {
            "SalesInvoicesController" => "4110", "SalesReceiptsController" => "4120",
            "SalesInvoicePaymentsController" => "4125", "BillsController" => "4210",
            "PaymentsController" or "BillPaymentsController" or "ExpensePaymentsController" => "4240",
            "GeneralJournalsController" => "4310", "ReceivingReportsController" => "4405",
            "InventoryAdjustmentsController" => "4410", "StockTransfersController" => "4420",
            "StockIssuancesController" => "4430", _ => null
        };
        var method = context.HttpContext.Request.Method;
        if (module == null || method is not ("POST" or "PUT" or "DELETE")) { await next(); return; }
        var ct = context.HttpContext.RequestAborted;
        ActionExecutedContext completed = null;
        try
        {
            var actor = context.HttpContext.Items[CompanyAccessService.ActorKey] as User;
            var company = Company(actor);
            Require(actor.UserRole == null || actor.UserRole.UserConfigId == null || actor.UserRole.UserConfigId == company, "Role does not belong to this company.", 403);
            var action = method == "POST" ? "canCreate" : method == "PUT" ? "canEdit" : "canDelete";
            Require(CashDisbursementRules.Can(actor.UserRole, module, action), "Permission to change this transaction is required.", 403);
            await using var transaction = await LockCompanyAsync(db, company, ct);
            var id = context.ActionArguments.TryGetValue("id", out var idValue) ? Convert.ToInt32(idValue) : 0;
            var input = context.ActionArguments.Values.FirstOrDefault(x => x != null && x.GetType().GetProperty("JournalEntries") != null)
                ?? context.ActionArguments.Values.FirstOrDefault(x => x != null && x.GetType().GetProperty("UserConfigId") != null);
            var before = id == 0 ? null : await LoadAsync(name, company, id, ct);
            if (name is "SalesInvoicesController" or "SalesReceiptsController")
                await SalesWorkflowService.GuardLegacy(db, company, name == "SalesInvoicesController" ? "SI" : "SR", id, (short?)Get(input, "Status"), ct, method == "DELETE");
            if (name is "BillsController" or "ReceivingReportsController" or "GeneralJournalsController")
                await PurchaseWorkflowService.GuardLegacy(db, company, name == "BillsController" ? "PB" : name == "ReceivingReportsController" ? "GR" : "LC", id, method == "POST", ct, input);
            if (id != 0) Require(before != null, "Transaction not found.", 404);
            if (input != null)
            {
                if (Get(input, "UserConfigId") is int inputCompany) Require(inputCompany == company, "Company does not match authenticated membership.", 403);
                Require((int)Get(input, "Id") == id, "Transaction ID does not match the route.");
                if (before != null)
                    Require(Stamp(Get(input, "LastUpdatedDate")) == Stamp(Get(before, "LastUpdatedDate")), "This transaction changed in another session. Reload before saving.", 409);
                Set(input, "CreatedByUserId", Get(before, "CreatedByUserId") ?? actor.Id);
                Set(input, "CreatedDate", Get(before, "CreatedDate"));
                Set(input, "LastUpdatedByUserId", before == null ? null : actor.Id);
            }
            var oldJournals = Journals(before).ToArray();
            PostedJournalValidator.DemandScope(actor.UserRole, (string)Get(before, "ResponsibilityCenterEntry"));
            foreach (var journal in oldJournals) PostedJournalValidator.DemandScope(actor.UserRole, journal.ResponsibilityCenterEntry);
            await ValidateReturnSourceDeletionAsync(name, method, company, id, input, ct);
            // These two old routes bind entities; validate and strip navigation graphs before EF sees them.
            if (input is ExpensePayment or BillPayment)
                await ValidateLegacyAsync(input, before, company, id, ct);
            if (input != null && Get(input, "JournalEntries") is IEnumerable submitted)
            {
                foreach (var entry in submitted.Cast<object>())
                {
                    Require(entry != null, "Journal entries cannot contain null entries.");
                    Require((int)Get(entry, "Id") != 0 || Get(entry, "Deleted") is not true, "New entries cannot be marked deleted.");
                    var old = oldJournals.SingleOrDefault(j => j.Id == (int)Get(entry, "Id"));
                    Set(entry, "CreatedByUserId", old?.CreatedByUserId ?? actor.Id);
                    Set(entry, "CreatedDate", old?.CreatedDate);
                    Set(entry, "LastUpdatedByUserId", old == null ? null : actor.Id);
                }
            }
            // Prevent FK SET NULL/cascade behavior from concealing an applied balance on deletion.
            var oldIds = oldJournals.Select(j => j.Id).ToArray();
            var receivingIds = await db.JournalEntries.AsNoTracking().Where(j => j.PaymentToJournalEntryId.HasValue && oldIds.Contains(j.PaymentToJournalEntryId.Value) && j.Status == 1).Select(j => j.PaymentToJournalEntryId.Value).Distinct().ToListAsync(ct);
            if (method == "DELETE")
                Require(receivingIds.Count == 0, "Reverse linked applications before deleting this transaction.", 409);
            if (method == "PUT" && receivingIds.Count > 0)
            {
                Require((short?)Get(input, "Status") == 1, "Reverse linked applications before changing posting status.", 409);
                foreach (var entry in ((IEnumerable)Get(input, "JournalEntries")).Cast<object>().Where(j => receivingIds.Contains((int)Get(j, "Id"))))
                    Require(Get(entry, "Deleted") is not true, "Reverse linked applications before removing this journal.", 409);
            }
            var executed = await next();
            completed = executed;
            if (executed.Exception is AdministrationException failure)
            {
                executed.ExceptionHandled = true;
                executed.Result = new ObjectResult(failure.Message) { StatusCode = failure.Status };
                return;
            }
            if (executed.Exception != null || executed.Canceled || (executed.Result as IStatusCodeActionResult)?.StatusCode >= 400) return;
            if (id == 0 && executed.Result is ObjectResult result) id = (int?)Get(result.Value, "Id") ?? 0;
            var after = method == "DELETE" ? null : await LoadAsync(name, company, id, ct);
            Require(method == "DELETE" || after != null, "Saved transaction could not be validated.", 409);
            var finalJournals = Journals(after).Where(j => j.Status != -1).ToArray();
            if (after != null)
            {
                Require(Get(after, "Status") is short status && status is 0 or 1, "Unsupported transaction status.");
                PostedJournalValidator.DemandScope(actor.UserRole, (string)Get(after, "ResponsibilityCenterEntry"));
                foreach (var journal in finalJournals) PostedJournalValidator.DemandScope(actor.UserRole, journal.ResponsibilityCenterEntry);
                foreach (var navigation in db.Model.FindEntityType(after.GetType()).GetNavigations().Where(n => n.IsCollection && n.Name != "JournalEntries"))
                    foreach (var line in ((IEnumerable)Get(after, navigation.Name)).Cast<object>().Where(l => (short?)Get(l, "Status") != -1))
                        Require((short?)Get(line, "Status") == (short?)Get(after, "Status"), "Detail status must match its document.");
                await new PostedJournalValidator(db).ValidateAsync(company, after, finalJournals, actor.UserRole, ct);
            }
            await new TransactionApplicationService(db).ReconcileAsync(company, before, after, oldJournals, finalJournals, actor.UserRole, ct);
            await db.SaveChangesAsync(ct);
            if (after != null && executed.Result is ObjectResult response)
            {
                var saved = await db.FindAsync(after.GetType(), new object[] { id }, ct);
                Set(response.Value, "LastUpdatedDate", Get(saved, "LastUpdatedDate"));
                if (response.Value?.GetType().GetProperty("Balance")?.CanWrite == true) Set(response.Value, "Balance", Get(saved, "Balance"));
            }
            await transaction.CommitAsync(ct);
        }
        catch (AdministrationException ex)
        {
            var result = new ObjectResult(ex.Message) { StatusCode = ex.Status };
            if (completed == null) context.Result = result;
            else { completed.Exception = null; completed.ExceptionHandled = true; completed.Result = result; }
        }
    }

    private async Task ValidateReturnSourceDeletionAsync(string controller, string method, int company, int id, object input, CancellationToken ct)
    {
        if (id == 0 || controller is not ("SalesInvoicesController" or "SalesReceiptsController")) return;
        var charge = controller == "SalesInvoicesController";
        var label = charge ? "Charge Invoice" : "Cash Invoice";
        if (method == "DELETE")
            await new SalesReturnSourceLinks(db).ReleaseDeletedAsync(company, id, charge, null, ct);
        if (method == "PUT" && Get(input, charge ? "SalesInvoiceDetails" : "SalesReceiptDetails") is IEnumerable lines)
        {
            var removed = lines.Cast<object>().Where(l => Get(l, "Deleted") is true)
                .Select(l => (int)Get(l, "Id")).Where(lineId => lineId > 0).ToArray();
            if (removed.Length > 0)
                await new SalesReturnSourceLinks(db).ReleaseDeletedAsync(company, id, charge, removed, ct);
        }
    }

    // Legacy JSON dates have millisecond precision. All new revisions are stamped at that precision.
    private static long? Stamp(object value) => value is DateTime date ? date.Ticks / TimeSpan.TicksPerMillisecond : null;

    private async Task<object> LoadAsync(string controller, int company, int id, CancellationToken ct) => controller switch
    {
        "SalesInvoicesController" => await Load<SalesInvoice>(company, id, ct),
        "SalesReceiptsController" => await Load<SalesReceipt>(company, id, ct),
        "SalesInvoicePaymentsController" => await Load<SalesInvoicePayment>(company, id, ct),
        "BillsController" => await Load<Bill>(company, id, ct),
        "PaymentsController" => await Load<Payment>(company, id, ct),
        "GeneralJournalsController" => await Load<GeneralJournal>(company, id, ct),
        "InventoryAdjustmentsController" => await Load<InventoryAdjustment>(company, id, ct),
        "ReceivingReportsController" => await Load<ReceivingReport>(company, id, ct),
        "StockIssuancesController" => await Load<StockIssuance>(company, id, ct),
        "StockTransfersController" => await Load<StockTransfer>(company, id, ct),
        "BillPaymentsController" => await Load<BillPayment>(company, id, ct),
        "ExpensePaymentsController" => await LegacyPaymentScope.Expenses(db, company).AsNoTracking().Include(e => e.JournalEntries).SingleOrDefaultAsync(e => e.Id == id, ct),
        _ => null
    };
    private async Task<T> Load<T>(int company, int id, CancellationToken ct) where T : class
    {
        IQueryable<T> query = db.Set<T>().AsNoTracking().AsSplitQuery();
        foreach (var navigation in db.Model.FindEntityType(typeof(T)).GetNavigations().Where(n => n.IsCollection)) query = query.Include(navigation.Name);
        return await query.SingleOrDefaultAsync(e => EF.Property<int>(e, "Id") == id && EF.Property<int>(e, "UserConfigId") == company, ct);
    }
    private async Task ValidateLegacyAsync(object input, object before, int company, int id, CancellationToken ct)
    {
        var journals = Journals(input).ToArray();
        Require(journals.Length > 0, "At least one journal is required to establish payment ownership.");
        var validator = new TransactionWriteValidator(db);
        var error = await validator.HeaderAsync(company, (int?)Get(input, "SupplierId"), (int?)Get(input, "CustomerId"), Array.Empty<int?>(), new[] { (int?)Get(input, "PaidThroughAccountId") }, ct);
        Require(error == null, error);
        var parent = input is BillPayment ? "BillPaymentId" : "ExpensePaymentId";
        error = await validator.JournalsAsync(company, id == 0 ? null : id, parent, journals, _ => null, ct);
        Require(error == null, error);
        foreach (var entity in journals.Cast<object>().Prepend(input))
            foreach (var navigation in db.Model.FindEntityType(entity.GetType()).GetNavigations().Where(n => n.Name != "JournalEntries"))
                entity.GetType().GetProperty(navigation.Name)?.SetValue(entity, null);
        foreach (var journal in journals)
        {
            journal.SalesInvoiceId = null; journal.SalesReturnId = null; journal.SalesReceiptId = null;
            journal.SalesInvoicePaymentId = null; journal.BillId = null; journal.PaymentId = null;
            journal.GeneralJournalId = null; journal.InventoryAdjustmentId = null;
        }
    }
}
