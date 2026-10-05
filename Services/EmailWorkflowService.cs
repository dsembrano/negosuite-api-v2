using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using negosuite_api.Contracts.Email;
using negosuite_api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed class EmailWorkflowService
{
    public const string InviteAction = "member-invite", ResetAction = "password-reset", ActivationAction = "new-account";
    private static readonly JsonSerializerSettings Json = new() { ContractResolver = new CamelCasePropertyNamesContractResolver() };
    private readonly negosuiteContext db;
    private readonly IEmailService sender;
    private readonly EmailTemplateService templates;
    private readonly IConfiguration config;
    private readonly ILogger<EmailWorkflowService> logger;
    public EmailWorkflowService(negosuiteContext db, IEmailService sender, EmailTemplateService templates, IConfiguration config, ILogger<EmailWorkflowService> logger)
    { this.db = db; this.sender = sender; this.templates = templates; this.config = config; this.logger = logger; }

    public async Task<EmailLogDetailDto> GetLogAsync(string uuid, CancellationToken ct)
    {
        var log = await db.EmailLogs.AsNoTracking().SingleOrDefaultAsync(e => e.Uuid == uuid && e.Status == 1 && e.ExpiryDate > DateTime.Now, ct);
        Require(log != null, "This link is no longer valid or has already expired.", 404);
        EmailWorkflowData data;
        try { data = JsonConvert.DeserializeObject<EmailWorkflowData>(log.Data ?? "null"); }
        catch (JsonException) { throw new AdministrationException("This link is no longer valid.", 404); }
        Require(data != null, "This link is no longer valid.", 404);
        var visible = new EmailLinkDataDto { Recipients = new() { log.Email }, Identifier = log.Uuid, ConfigId = log.ConfigId,
            RecipientName = data.RecipientName, SenderName = data.SenderName, CompanyName = data.CompanyName,
            UserRoleId = data.UserRoleId, ExpiryDate = log.ExpiryDate };
        return new EmailLogDetailDto { Id = log.Id, Uuid = log.Uuid, Email = log.Email, ConfigId = log.ConfigId,
            Data = JsonConvert.SerializeObject(visible, Json), CreatedDate = log.CreatedDate, LastUpdatedDate = log.LastUpdatedDate,
            ExpiryDate = log.ExpiryDate, Status = log.Status, Action = log.Action };
    }
    public Task SendAsync(SendEmailRequest input, CancellationToken ct)
    {
        var recipients = EmailValidation.Recipients(input.Recipients);
        Require(!string.IsNullOrWhiteSpace(input.Subject) && input.Subject.Length <= 250 && !input.Subject.Contains('\r') && !input.Subject.Contains('\n'), "Invalid subject.");
        Require(!string.IsNullOrWhiteSpace(input.Message), "Message is required.");
        return DeliverAsync(new OutgoingEmail(recipients, input.Subject, input.Message), ct);
    }
    public async Task InviteAsync(User actor, MemberInviteRequest input, CancellationToken ct)
    {
        Admin(actor); Require(actor.ConfigId == input.ConfigId, "Company does not match membership.", 403);
        var recipients = EmailValidation.Recipients(input.Recipients, true);
        if (input.UserRoleId.HasValue) Require(await db.UserRoles.AnyAsync(r => r.Id == input.UserRoleId && (r.UserConfigId == actor.ConfigId || r.UserConfigId == null), ct), "Role must belong to this company or be a shared system role.");
        var data = new EmailWorkflowData { Recipients = recipients, RecipientName = input.RecipientName, ConfigId = actor.ConfigId,
            UserRoleId = input.UserRoleId, SenderName = actor.Name, CompanyName = actor.Config.CompanyName };
        await IssueAsync(data, InviteAction, TimeSpan.FromDays(7), ct);
    }
    public async Task ConfirmAsync(EmailConfirmationRequest input, CancellationToken ct)
    {
        var recipients = EmailValidation.Recipients(input.Recipients, true); PasswordSecurity.ValidateNew(input.Password);
        Require(!await db.Users.AnyAsync(u => u.Email == recipients[0], ct), "Email address already exists.");
        var data = new EmailWorkflowData { Recipients = recipients, RecipientName = input.RecipientName, Password = PasswordSecurity.Hash(input.Password) };
        await IssueAsync(data, ActivationAction, TimeSpan.FromDays(1), ct);
        // Operational notifications must not contain an activation capability or password.
        if (!string.IsNullOrWhiteSpace(config["NotificationRecipients"]))
        {
            try
            {
                var notifications = EmailValidation.Recipients(config["NotificationRecipients"].Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());
                await DeliverAsync(new OutgoingEmail(notifications, "Negosuite - New registration", templates.RegistrationNotice(data)), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { logger.LogWarning("Registration notification failed after confirmation delivery."); }
        }
    }
    public async Task PasswordResetAsync(PasswordResetEmailRequest input, CancellationToken ct)
    {
        var recipients = EmailValidation.Recipients(input.Recipients, true);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == recipients[0] && u.Status, ct);
        Require(user != null, "Email not found. Are you sure you are already a member?", 404);
        await IssueAsync(new EmailWorkflowData { Recipients = new() { user.Email }, RecipientName = user.Name }, ResetAction, TimeSpan.FromHours(2), ct);
    }
    private async Task IssueAsync(EmailWorkflowData data, string action, TimeSpan lifetime, CancellationToken ct)
    {
        data.Identifier = Guid.NewGuid().ToString();
        // Existing email-log consumers use server-local time. Preserve that convention for old links.
        data.ExpiryDate = DateTime.Now.Add(lifetime);
        data.Subject = action == InviteAction ? $"{data.SenderName} invites you to join their Negosuite team" : action == ResetAction ? "Reset your password" : "Negosuite - Email Confirmation";
        var message = templates.Render(data, action);
        var log = new EmailLog { Uuid = data.Identifier, Email = data.Recipients[0], ConfigId = data.ConfigId,
            Data = JsonConvert.SerializeObject(data, Json), Status = 2, CreatedDate = DateTime.Now, ExpiryDate = data.ExpiryDate, Action = action };
        db.EmailLogs.Add(log); await db.SaveChangesAsync(ct);
        try
        {
            await DeliverAsync(new OutgoingEmail(data.Recipients, data.Subject, message), ct);
            log.Status = 1; log.LastUpdatedDate = DateTime.Now; await db.SaveChangesAsync(ct);
        }
        catch
        {
            log.Status = 0; log.LastUpdatedDate = DateTime.Now;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
    private async Task DeliverAsync(OutgoingEmail message, CancellationToken ct)
    {
        try { await sender.SendAsync(message, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { logger.LogWarning("Email transport failed."); throw new AdministrationException("Email delivery failed. Please try again.", 503); }
    }
}
