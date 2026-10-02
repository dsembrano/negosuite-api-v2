using System.Collections.Generic;
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
[ApiController, Route("api/configs")]
public class ConfigsController : ControllerBase
{
    private readonly ConfigService configs;
    public ConfigsController(ConfigService configs) => this.configs = configs;
    private User Actor => (User)HttpContext.Items[CompanyAccessService.ActorKey];

    [HttpGet]
    public async Task<ActionResult> GetConfigs([FromQuery] AdministrationListOptions options, CancellationToken ct) => Ok(await configs.ListAsync(Actor, options, ct));

    [HttpGet("template")]
    public async Task<ActionResult<List<ConfigTemplateDto>>> GetConfigTeamplates(CancellationToken ct) => await configs.TemplatesAsync(ct);

    [HttpGet("{id}")]
    public async Task<ActionResult<ConfigDetailDto>> GetConfig(int id, CancellationToken ct)
    { var config = await configs.GetAsync(Actor, id, ct); return config == null ? NotFound() : config; }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutConfig(int id, ConfigUpdateRequest input, CancellationToken ct)
    { Require(input.Id == id, "Route and body IDs must match."); await configs.UpdateAsync(Actor, id, input, ct); return NoContent(); }

    [HttpPost]
    public async Task<ActionResult<ConfigDetailDto>> PostConfig(ConfigCreateRequest input, CancellationToken ct)
    {
        Require(input.Id == 0, "Use the company-template setup route to select a template.");
        var result = await configs.SetupAsync(Actor, Actor.Id, input, false, ct);
        return CreatedAtAction(nameof(GetConfig), new { id = result.ConfigId }, result.Config);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteConfig(int id, CancellationToken ct)
    { await configs.DeleteAsync(Actor, id, ct); return NoContent(); }

    [HttpPut("payment-adjustment-type/{id}")]
    public async Task<IActionResult> PutConfigPaymentAdjusmentTypes(int id, [FromBody] string paymentAdjustmentTypes, CancellationToken ct)
    { await configs.SetPaymentAdjustmentsAsync(Actor, id, paymentAdjustmentTypes, ct); return NoContent(); }
}

public class RCRequiredBy
{
    public int RCNumber { get; set; }
    public string RequiredBy { get; set; }
    public List<int> RequiredByIds { get; set; }
}
