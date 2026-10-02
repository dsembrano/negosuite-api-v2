using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Administration;
using negosuite_api.Models;
using negosuite_api.Services;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(AuthenticatedUserFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
[ApiController, Route("api/users")]
public class UsersController : ControllerBase
{
    public static bool STATUS_INACTIVE = false, STATUS_ACTIVE = true;
    public static byte STATUS_OPEN = 1, STATUS_CLOSE = 0, APPLICATION_USER = 1, CONFIG_ADMIN = 2;
    private readonly UserService users;
    private readonly UserRoleService roles;
    private readonly ConfigService configs;
    public UsersController(UserService users, UserRoleService roles, ConfigService configs)
    { this.users = users; this.roles = roles; this.configs = configs; }
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetUsers([FromQuery] AdministrationListOptions options, CancellationToken ct) =>
        Ok(await users.ListAsync(Actor, false, options, ct));

    [HttpGet("config")]
    public async Task<ActionResult> GetConfigUsers(string criteria, [FromQuery] AdministrationListOptions options, CancellationToken ct)
    { Criteria(criteria, Company(Actor)); return Ok(await users.ListAsync(Actor, true, options, ct)); }

    [HttpGet("config-can-add")]
    public async Task<ActionResult> GetConfigCanAdd(int configId, CancellationToken ct) => Ok(await users.CanAddAsync(Actor, configId, ct));

    [HttpGet("roles")]
    public async Task<ActionResult> GetUserRoles(string criteria, [FromQuery] AdministrationListOptions options, CancellationToken ct)
    { Criteria(criteria, Company(Actor)); return Ok(await roles.ListAsync(Company(Actor), options, true, ct)); }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDetailDto>> GetUser(int id, CancellationToken ct)
    { var user = await users.GetAsync(Actor, id, ct); return user == null ? NotFound() : user; }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutUser(int id, UserUpdateRequest input, CancellationToken ct)
    { Require(input.Id == id, "Route and body IDs must match."); await users.UpdateAsync(Actor, id, input, ct); return NoContent(); }

    [HttpPut("update-role")]
    public async Task<IActionResult> PutUserRole(int id, int userRoleId, CancellationToken ct)
    { await users.SetRoleAsync(Actor, id, userRoleId, ct); return NoContent(); }

    [HttpPut("update-ui-config")]
    public async Task<IActionResult> PutUserUIConfig(int id, UserUIConfigRequest input, CancellationToken ct)
    { await users.SetUIAsync(Actor, id, input.UserUIConfigString, ct); return NoContent(); }

    [HttpPut("update-config")]
    public async Task<ActionResult<CompanySetupResponse>> PutUserConfig(int id, CompanySetupRequest input, CancellationToken ct) =>
        await configs.SetupAsync(Actor, id, input, false, ct);

    [HttpPut("update-config-template")]
    public async Task<ActionResult<CompanySetupResponse>> PutUserConfigFromTemplate(int id, CompanySetupRequest input, CancellationToken ct) =>
        await configs.SetupAsync(Actor, id, input, true, ct);

    [AllowAnonymous, HttpPost]
    public async Task<ActionResult<UserDetailDto>> PostUser(UserCreateRequest input, CancellationToken ct)
    {
        var user = await users.RegisterAsync(input, false, ct);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [AllowAnonymous, HttpPost("new-account")]
    public async Task<ActionResult<UserDetailDto>> PostNewUserAccount(UserCreateRequest input, CancellationToken ct)
    {
        var user = await users.RegisterAsync(input, true, ct);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    { await users.DeleteAsync(Actor, id, ct); return NoContent(); }
}
