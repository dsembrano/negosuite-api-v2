using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.Maintenance;
using negosuite_api.Models;
using negosuite_api.Services;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Controllers;

[Authorize, TypeFilter(typeof(ConfigUuidFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
[ApiController, Route("api/responsibility-center-types")]
public class ResponsibilityCenterTypesController : ControllerBase
{
    public static bool STATUS_ACTIVE = true, STATUS_INACTIVE = false;
    private readonly ResponsibilityCenterTypeService service;
    public ResponsibilityCenterTypesController(ResponsibilityCenterTypeService service) => this.service = service;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetResponsibilityCenterTypes(string criteria, [FromQuery] AdministrationListOptions options, CancellationToken ct)
    {
        var filter = MaintenanceServiceCriteria.Parse(criteria);
        var company = Company(Actor); Require(filter.UserConfigId == company, "Company does not match membership.", 403);
        return Ok(await service.ListAsync(company, filter, options, false, ct));
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<ResponsibilityCenterTypeDetailDto>> GetResponsibilityCenterType(int id, CancellationToken ct)
    { var result = await service.GetAsync(Company(Actor), id, ct); return result == null ? NotFound() : result; }

    [HttpPost]
    public async Task<ActionResult<ResponsibilityCenterTypeDetailDto>> PostResponsibilityCenterType(ResponsibilityCenterTypeCreateRequest input, CancellationToken ct)
    {
        Require(input.Id == 0 && input.Deleted != true, "New record ID must be zero or omitted.");
        var result = await service.SaveAsync(Actor, null, input, ct);
        return CreatedAtAction(nameof(GetResponsibilityCenterType), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutResponsibilityCenterType(int id, ResponsibilityCenterTypeUpdateRequest input, CancellationToken ct)
    {
        Require(input.Id == id && input.Deleted != true, "Route/body IDs must match; use DELETE to remove a record.");
        await service.SaveAsync(Actor, id, input, ct); return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteResponsibilityCenterType(int id, CancellationToken ct)
    { await service.DeleteAsync(Actor, id, ct); return NoContent(); }

    [HttpPost("many")]
    public async Task<ActionResult> PostManyResponsibilityCenterType(List<ResponsibilityCenterTypeWriteRequest> input, CancellationToken ct)
    {
        await service.SaveManyAsync(Actor, input, ct);
        return Ok(await service.ListAsync(Company(Actor), new MaintenanceCriteria(), new AdministrationListOptions(), false, ct));
    }
}
