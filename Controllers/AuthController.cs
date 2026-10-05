using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Auth;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

[ApiController, Route("api/auth"), TypeFilter(typeof(AdministrationExceptionFilter))]
public class AuthController : ControllerBase
{
    private readonly AuthService service;
    public AuthController(AuthService service) => this.service = service;
    [AllowAnonymous, HttpPost("sign-in")]
    public async Task<ActionResult<SignInResponse>> SignIn(SignInRequest input, CancellationToken ct) => Ok(await service.SignInAsync(input, ct));
    [AllowAnonymous, HttpPost("refresh-access-token")]
    public async Task<ActionResult<AuthSessionResponse>> Refresh(RefreshAccessTokenRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.RefreshToken)) return BadRequest();
        return Ok(await service.RefreshAsync(input, ct));
    }
    [Authorize, HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest input, CancellationToken ct)
    { await service.ChangePasswordAsync(User, input, ct); return Ok(); }
    [AllowAnonymous, HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest input, CancellationToken ct)
    { await service.ResetPasswordAsync(input, ct); return Ok(); }
    [AllowAnonymous, HttpGet("app-version")]
    public async Task<ActionResult<AppVersionDetailDto>> AppVersion(CancellationToken ct) => Ok(await service.GetAppVersionAsync(ct));
    [Authorize, HttpGet("metabase-token")]
    public async Task<ActionResult<MetabaseTokenResponse>> MetabaseToken(int userConfigId, CancellationToken ct) => Ok(await service.MetabaseAsync(User, userConfigId, ct));
}
