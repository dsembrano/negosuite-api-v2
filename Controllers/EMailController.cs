using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Email;
using negosuite_api.Models;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

[Authorize, ApiController, Route("api/email")]
[TypeFilter(typeof(AuthenticatedUserFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
public class EmailController : ControllerBase
{
    private readonly EmailWorkflowService service;
    public EmailController(EmailWorkflowService service) => this.service = service;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];
    [AllowAnonymous, HttpGet("log/{uuid}")]
    public async Task<ActionResult<EmailLogDetailDto>> GetLog(string uuid, CancellationToken ct) => Ok(await service.GetLogAsync(uuid, ct));
    [HttpPost]
    public async Task<IActionResult> Send(SendEmailRequest input, CancellationToken ct)
    { await service.SendAsync(input, ct); return Ok(); }
    [HttpPost("member-invite")]
    public async Task<IActionResult> Invite(MemberInviteRequest input, CancellationToken ct)
    { await service.InviteAsync(Actor, input, ct); return Ok(); }
    [AllowAnonymous, HttpPost("email-confirmation")]
    public async Task<IActionResult> Confirm(EmailConfirmationRequest input, CancellationToken ct)
    { await service.ConfirmAsync(input, ct); return Ok(); }
    [AllowAnonymous, HttpPost("password-reset")]
    public async Task<IActionResult> PasswordReset(PasswordResetEmailRequest input, CancellationToken ct)
    { await service.PasswordResetAsync(input, ct); return Ok(); }
}
