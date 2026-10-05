using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Payments;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

public partial class PaymentsController
{
    [HttpPost("disbursement")]
    public async Task<ActionResult<PaymentDetailDto>> PostDisbursement(PaymentCreateRequest request)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        // Shared transaction filter owns the company lock and commit.
        var error = await ValidateDisbursement(request, null, "canCreate");
        if (error != null) return error;
        var result = await PostPayment(request);

        return result;
    }

    [HttpPut("disbursement/{id:int}")]
    public async Task<IActionResult> PutDisbursement(int id, PaymentUpdateRequest request)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        // Shared transaction filter owns the company lock and commit.
        var error = await ValidateDisbursement(request, id, "canEdit");
        if (error != null) return error;
        var result = await PutPayment(id, request);

        return result;
    }

    [HttpDelete("disbursement/{id:int}")]
    public async Task<IActionResult> DeleteDisbursement(int id)
    {
        if (!CompanyId.HasValue) return Unauthorized();
        // Shared transaction filter owns the company lock and commit.
        var error = await ValidateDisbursement(null, id, "canDelete");
        if (error != null) return error;
        var result = await DeletePayment(id);

        return result;
    }

    private async Task<ActionResult> ValidateDisbursement(PaymentWriteRequest request, int? id, string action)
    {
        var ct = HttpContext.RequestAborted;
        var company = CompanyId.Value;
        if (!int.TryParse(User.FindFirst("negosuite_user_id")?.Value, out var userId)) return Unauthorized();
        var user = await _context.Users.AsNoTracking().Include(u => u.UserRole).SingleOrDefaultAsync(u => u.Id == userId && u.Status && u.ConfigId == company, ct);
        if (user == null || !CashDisbursementRules.Can(user.UserRole, "4240", action)) return Forbid();
        var advancedAction = id.HasValue ? "canEdit" : "canCreate";
        var advanced = CashDisbursementRules.Can(user.UserRole, "4310", advancedAction);
        var config = await _context.Configs.AsNoTracking().SingleOrDefaultAsync(c => c.Id == company, ct);
        if (config == null) return BadRequest("Missing company configuration.");
        var inventory = (await _context.Items.Where(i => i.UserConfigId == company && i.TrackInventory == true && i.InventoryAccountId.HasValue).Select(i => i.InventoryAccountId.Value).Distinct().ToListAsync(ct)).ToHashSet();
        Payment existing = null;
        if (id.HasValue)
        {
            existing = await _context.Payments.AsNoTracking().Include(p => p.JournalEntries).SingleOrDefaultAsync(p => p.Id == id && p.UserConfigId == company, ct);
            if (existing == null) return NotFound();
            var blocked = CashDisbursementRules.Existing(existing, config.APTradeAccountId, inventory);
            if (blocked != null) return BadRequest(blocked);
            var ids = existing.JournalEntries.Select(j => j.Id).ToArray();
            if (await _context.JournalEntries.AnyAsync(j => j.PaymentToJournalEntryId.HasValue && ids.Contains(j.PaymentToJournalEntryId.Value), ct)) return Conflict("This payment has linked transactions.");
            if (!advanced && existing.JournalEntries.Any(j => j.IsComputed != true && j.Nature == "C")) return Forbid();
        }
        if (action == "canDelete") return null;
        if (request == null || request.Id != (id ?? 0)) return BadRequest("Invalid payment ID.");
        if (request.UserConfigId != company) return Forbid();
        if (existing != null && request.LastUpdatedDate != existing.LastUpdatedDate) return Conflict("This payment changed in another session. Reload before saving.");
        var payment = TransactionWriteMapping.Map(request);
        var validation = await service.ValidateWriteAsync(company, id, payment, false, ct);
        if (validation != null) return BadRequest(validation);
        if (existing != null && existing.JournalEntries.Any(j => !payment.JournalEntries.Any(e => e.Id == j.Id))) return BadRequest("Existing journal entries must be retained or explicitly deleted.");
        if (payment.JournalEntries.Any(j => j.Id == 0 && j.Deleted == true)) return BadRequest("New entries cannot be marked deleted.");
        var accounts = await _context.Accounts.AsNoTracking().Include(a => a.Category).Where(a => a.UserConfigId == company).ToDictionaryAsync(a => a.Id, ct);
        var taxes = await _context.TaxRates.AsNoTracking().Where(t => t.UserConfigId == company).ToDictionaryAsync(t => t.Id, ct);
        var types = await _context.ResponsibilityCenterTypes.AsNoTracking().Where(t => t.UserConfigId == company).ToListAsync(ct);
        var centers = await _context.ResponsibilityCenters.AsNoTracking().Where(c => c.UserConfigId == company).ToListAsync(ct);
        validation = CashDisbursementRules.Validate(payment, config, accounts, inventory, taxes, types, centers, user.UserRole, advanced);
        if (validation != null) return BadRequest(validation);
        // Identity belongs to the authenticated user; preserve original creation attribution.
        request.CreatedByUserId = existing?.CreatedByUserId ?? userId;
        request.CreatedDate = existing?.CreatedDate;
        request.LastUpdatedByUserId = id.HasValue ? userId : null;
        foreach (var journal in request.JournalEntries)
        {
            var old = existing?.JournalEntries.SingleOrDefault(j => j.Id == journal.Id);
            journal.CreatedByUserId = old?.CreatedByUserId ?? userId;
            journal.CreatedDate = old?.CreatedDate;
            journal.LastUpdatedByUserId = old != null ? userId : null;
        }
        return null;
    }
}
