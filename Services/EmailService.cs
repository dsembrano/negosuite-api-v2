using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed record OutgoingEmail(IReadOnlyList<string> Recipients, string Subject, string HtmlBody);
public interface IEmailService
{
    Task SendAsync(OutgoingEmail email, CancellationToken ct);
}
public sealed class EmailService : IEmailService
{
    private readonly IConfiguration config;
    public EmailService(IConfiguration config) => this.config = config;
    public async Task SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config["Smtp:Host"]) || !int.TryParse(config["Smtp:Port"], out var port) || port < 1 || port > 65535 ||
            !MailAddress.TryCreate(config["Smtp:FromEmail"], out var from)) throw new InvalidOperationException("SMTP is not configured.");
        using var message = new MailMessage { From = from, Subject = email.Subject, Body = email.HtmlBody, IsBodyHtml = true };
        foreach (var recipient in email.Recipients) message.To.Add(recipient);
        using var client = new SmtpClient(config["Smtp:Host"], port)
        {
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]),
            EnableSsl = true
        };
        await client.SendMailAsync(message, ct);
    }
}
public static class EmailValidation
{
    public static List<string> Recipients(List<string> recipients, bool single = false)
    {
        Require(recipients != null && recipients.Count > 0 && recipients.Count <= (single ? 1 : 100), single ? "Supply exactly one recipient." : "Supply 1 to 100 recipients.");
        var result = new List<string>();
        foreach (var raw in recipients)
        {
            var value = raw?.Trim();
            Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 150 && MailAddress.TryCreate(value, out var address) &&
                string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase) && !value.Contains('\r') && !value.Contains('\n'), "Invalid recipient email address.");
            result.Add(value);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
