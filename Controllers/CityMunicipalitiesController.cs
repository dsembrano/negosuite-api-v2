using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.ReferenceData;
using negosuite_api.Models;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

[Authorize, ApiController, Route("api/city-municipalities")]
[TypeFilter(typeof(ConfigUuidFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
public class CityMunicipalitiesController : ControllerBase
{
    private readonly CityMunicipalityService service;
    public CityMunicipalitiesController(CityMunicipalityService service) => this.service = service;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetList([FromQuery] AdministrationListOptions options, [FromQuery] ReferenceDataFilter filter, CancellationToken ct) =>
        Ok(await service.ListAsync(options, filter, ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<CityMunicipalityDetailDto>> GetCityMunicipality(int id, CancellationToken ct)
    { var value = await service.GetAsync(id, ct); return value == null ? NotFound() : value; }

    [HttpPost]
    public async Task<ActionResult<CityMunicipalityDetailDto>> PostCityMunicipality(CityMunicipalityCreateRequest input, CancellationToken ct)
    { var value = await service.SaveAsync(Actor, null, input, ct); return CreatedAtAction(nameof(GetCityMunicipality), new { id = value.Id }, value); }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCityMunicipality(int id, CityMunicipalityUpdateRequest input, CancellationToken ct)
    { await service.SaveAsync(Actor, id, input, ct); return NoContent(); }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCityMunicipality(int id, CancellationToken ct)
    { await service.DeleteAsync(id, ct); return NoContent(); }
}
