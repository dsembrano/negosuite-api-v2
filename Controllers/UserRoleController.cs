using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Administration;
using negosuite_api.Models;
using negosuite_api.Services;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
[ApiController, Route("api/user-roles")]
public class UserRoleController : ControllerBase
{
    private readonly UserRoleService roles;
    public UserRoleController(UserRoleService roles) => this.roles = roles;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetUserRoles(string criteria, [FromQuery] AdministrationListOptions options, CancellationToken ct)
    {
        Criteria(criteria, Company(Actor));
        return Ok(await roles.ListAsync(Company(Actor), options, false, ct));
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<UserRoleDetailDto>> GetUserRole(int id, CancellationToken ct)
    {
        var role = await roles.GetAsync(Company(Actor), id, ct);
        return role == null ? NotFound() : role;
    }
    [HttpPost]
    public async Task<ActionResult<UserRoleDetailDto>> PostUser(UserRoleCreateRequest input, CancellationToken ct)
    {
        Require(input.Id == 0, "New role ID must be zero or omitted.");
        var role = await roles.SaveAsync(Actor, null, input, ct);
        return CreatedAtAction(nameof(GetUserRole), new { id = role.Id }, role);
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> PutUserRole(int id, UserRoleUpdateRequest input, CancellationToken ct)
    {
        Require(id == input.Id, "Route and body IDs must match.");
        await roles.SaveAsync(Actor, id, input, ct); return NoContent();
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUserRole(int id, CancellationToken ct)
    { await roles.DeleteAsync(Actor, id, ct); return NoContent(); }
}
