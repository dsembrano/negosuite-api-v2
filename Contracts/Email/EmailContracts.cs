using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace negosuite_api.Contracts.Email;

public sealed class SendEmailRequest
{
    [Required] public List<string> Recipients { get; set; }
    [Required, MaxLength(250)] public string Subject { get; set; }
    [Required] public string Message { get; set; }
}
public class EmailLinkRequest
{
    [Required] public List<string> Recipients { get; set; }
    public string Identifier { get; set; } // Legacy input accepted; the server generates the actual capability.
    public DateTime? ExpiryDate { get; set; } // Expiry is server-owned.
}
public sealed class MemberInviteRequest : EmailLinkRequest
{
    [Required, MaxLength(150)] public string RecipientName { get; set; }
    public int? ConfigId { get; set; }
    public int? UserRoleId { get; set; }
}
public sealed class EmailConfirmationRequest : EmailLinkRequest
{
    [Required, MaxLength(150)] public string RecipientName { get; set; }
    [Required] public string Password { get; set; }
}
public sealed class PasswordResetEmailRequest : EmailLinkRequest { }

// Stored workflow data also reads existing emaillog JSON. Never serialize this type as a public response.
public sealed class EmailWorkflowData
{
    public string Subject { get; set; }
    public List<string> Recipients { get; set; }
    public string Message { get; set; }
    public string CompanyName { get; set; }
    public string SenderName { get; set; }
    public string RecipientName { get; set; }
    public string Password { get; set; }
    public string Identifier { get; set; }
    public int? ConfigId { get; set; }
    public int? UserRoleId { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
public sealed class EmailLinkDataDto
{
    public List<string> Recipients { get; set; }
    public string CompanyName { get; set; }
    public string SenderName { get; set; }
    public string RecipientName { get; set; }
    public string Identifier { get; set; }
    public int? ConfigId { get; set; }
    public int? UserRoleId { get; set; }
    public DateTime? ExpiryDate { get; set; }
}
public sealed class EmailLogDetailDto
{
    public int Id { get; set; }
    public string Uuid { get; set; }
    public string Email { get; set; }
    public string Data { get; set; }
    public int? ConfigId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Status { get; set; }
    public string Action { get; set; }
}
