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

[Authorize, TypeFilter(typeof(AuthenticatedUserFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
[ApiController, Route("api/tax-rates")]
public class TaxRatesController : ControllerBase
{
    public static bool STATUS_ACTIVE = true, STATUS_INACTIVE = false;
    private readonly TaxRateService service;
    public TaxRatesController(TaxRateService service) => this.service = service;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetTaxRate(string criteria, [FromQuery] AdministrationListOptions options, CancellationToken ct)
    {
        var filter = MaintenanceServiceCriteria.Parse(criteria);
        var company = await service.ReadCompanyAsync(Actor, filter.UserConfigId.Value, ct);
        return Ok(await service.ListAsync(company, filter, options, false, ct));
    }
    [TypeFilter(typeof(ConfigUuidFilter))]
    [HttpGet("{id}")]
    public async Task<ActionResult<TaxRateDetailDto>> GetTaxRate(int id, CancellationToken ct)
    { var result = await service.GetAsync(Company(Actor), id, ct); return result == null ? NotFound() : result; }

    [TypeFilter(typeof(ConfigUuidFilter))]
    [HttpPost]
    public async Task<ActionResult<TaxRateDetailDto>> PostTaxRate(TaxRateCreateRequest input, CancellationToken ct)
    {
        Require(input.Id == 0 && input.Deleted != true, "New record ID must be zero or omitted.");
        var result = await service.SaveAsync(Actor, null, input, ct);
        return CreatedAtAction(nameof(GetTaxRate), new { id = result.Id }, result);
    }

    [TypeFilter(typeof(ConfigUuidFilter))]
    [HttpPut("{id}")]
    public async Task<IActionResult> PutTaxRate(int id, TaxRateUpdateRequest input, CancellationToken ct)
    {
        Require(input.Id == id && input.Deleted != true, "Route/body IDs must match; use DELETE to remove a record.");
        await service.SaveAsync(Actor, id, input, ct); return NoContent();
    }

    [TypeFilter(typeof(ConfigUuidFilter))]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTaxRate(int id, CancellationToken ct)
    { await service.DeleteAsync(Actor, id, ct); return NoContent(); }

    [TypeFilter(typeof(ConfigUuidFilter))]
    [HttpPost("many")]
    public async Task<ActionResult> PostManyTaxRate(List<TaxRateWriteRequest> input, CancellationToken ct)
    {
        await service.SaveManyAsync(Actor, input, ct);
        return Ok(await service.ListAsync(Company(Actor), new MaintenanceCriteria(), new AdministrationListOptions(), false, ct));
    }
}
