using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Administration;
using negosuite_api.Contracts.ReferenceData;
using negosuite_api.Models;
using negosuite_api.Services;

namespace negosuite_api.Controllers;

[Authorize, ApiController, Route("api/currencies")]
[TypeFilter(typeof(AuthenticatedUserFilter)), TypeFilter(typeof(AdministrationExceptionFilter))]
public class CurrenciesController : ControllerBase
{
    private readonly CurrencyService service;
    public CurrenciesController(CurrencyService service) => this.service = service;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetList([FromQuery] AdministrationListOptions options, [FromQuery] ReferenceDataFilter filter, CancellationToken ct) =>
        Ok(await service.ListAsync(options, filter, ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<CurrencyDetailDto>> GetCurrency(short id, CancellationToken ct)
    { var value = await service.GetAsync(id, ct); return value == null ? NotFound() : value; }

    [HttpGet("base")]
    public async Task<ActionResult<CurrencyDetailDto>> GetBaseCurrency(CancellationToken ct)
    { var value = await service.BaseAsync(ct); return value == null ? NotFound() : value; }

    [HttpPost]
    public async Task<ActionResult<CurrencyDetailDto>> PostCurrency(CurrencyCreateRequest input, CancellationToken ct)
    { var value = await service.SaveAsync(Actor, null, input, ct); return CreatedAtAction(nameof(GetCurrency), new { id = value.Id }, value); }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutCurrency(short id, CurrencyUpdateRequest input, CancellationToken ct)
    { await service.SaveAsync(Actor, id, input, ct); return NoContent(); }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCurrency(short id, CancellationToken ct)
    { await service.DeleteAsync(id, ct); return NoContent(); }
}
