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
[ApiController, Route("api/inventory-locations")]
public class InventoryLocationsController : ControllerBase
{
    public static bool STATUS_ACTIVE = true, STATUS_INACTIVE = false;
    private readonly InventoryLocationService service;
    public InventoryLocationsController(InventoryLocationService service) => this.service = service;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetInventoryLocations(string criteria, [FromQuery] AdministrationListOptions options, CancellationToken ct)
    {
        var filter = MaintenanceServiceCriteria.Parse(criteria);
        var company = Company(Actor); Require(filter.UserConfigId == company, "Company does not match membership.", 403);
        return Ok(await service.ListAsync(company, filter, options, false, ct));
    }
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryLocationDetailDto>> GetInventoryLocation(int id, CancellationToken ct)
    { var result = await service.GetAsync(Company(Actor), id, ct); return result == null ? NotFound() : result; }

    [HttpPost]
    public async Task<ActionResult<InventoryLocationDetailDto>> PostInventoryLocation(InventoryLocationCreateRequest input, CancellationToken ct)
    {
        Require(input.Id == 0 && input.Deleted != true, "New record ID must be zero or omitted.");
        var result = await service.SaveAsync(Actor, null, input, ct);
        return CreatedAtAction(nameof(GetInventoryLocation), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutInventoryLocation(int id, InventoryLocationUpdateRequest input, CancellationToken ct)
    {
        Require(input.Id == id && input.Deleted != true, "Route/body IDs must match; use DELETE to remove a record.");
        await service.SaveAsync(Actor, id, input, ct); return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteInventoryLocation(int id, CancellationToken ct)
    { await service.DeleteAsync(Actor, id, ct); return NoContent(); }
}
