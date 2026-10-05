using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using negosuite_api.Services;

namespace negosuite_api.Controllers;
[ApiController,Authorize,TypeFilter(typeof(ConfigUuidFilter)),TypeFilter(typeof(AuthenticatedUserFilter)),TypeFilter(typeof(AdministrationExceptionFilter))]
[Route("api/sales-returns")]
public class SalesReturnsController:ControllerBase
{
    private readonly SalesReturnService service;
    public SalesReturnsController(SalesReturnService service)=>this.service=service;
    private User Actor=>(User)HttpContext.Items[CompanyAccessService.ActorKey];
    [HttpGet] public async Task<ActionResult> List([FromQuery]SalesReturnListRequest query,CancellationToken ct)=>Ok(await service.ListAsync(Actor,query,ct));
    [HttpGet("sources")] public async Task<ActionResult> Sources(string source,string search,int? customerId,CancellationToken ct)=>Ok(await service.SourceOptionsAsync(Actor,source,search,customerId,ct));
    [HttpGet("sources/{source}/{id:int}")] public async Task<ActionResult> Source(string source,int id,CancellationToken ct)=>Ok(await service.SourceAsync(Actor,source,id,ct));
    [HttpGet("targets")] public async Task<ActionResult> Targets(int customerId,CancellationToken ct)=>Ok(await service.TargetsAsync(Actor,customerId,ct));
    [HttpGet("{id:int}")] public async Task<ActionResult> Get(int id,CancellationToken ct,bool print=false)=>Ok(await service.GetAsync(Actor,id,ct,print));
    [HttpPost("preview")] public async Task<ActionResult> Preview(SalesReturnWriteRequest value,CancellationToken ct)=>Ok(await service.PreviewAsync(Actor,value,ct));
    [HttpPost] public async Task<ActionResult> Create(SalesReturnWriteRequest value,CancellationToken ct)=>Ok(await service.SaveAsync(Actor,null,value,ct));
    [HttpPut("{id:int}")] public async Task<ActionResult> Update(int id,SalesReturnWriteRequest value,CancellationToken ct)=>Ok(await service.SaveAsync(Actor,id,value,ct));
    [HttpPost("{id:int}/post")] public async Task<ActionResult> Post(int id,SalesReturnActionRequest value,CancellationToken ct)=>Ok(await service.PostAsync(Actor,id,value.Version,ct));
    [HttpPost("{id:int}/void")] public async Task<ActionResult> Void(int id,SalesReturnActionRequest value,CancellationToken ct)=>Ok(await service.VoidAsync(Actor,id,value,ct));
    [HttpDelete("{id:int}")] public async Task<ActionResult> Delete(int id,long version,CancellationToken ct){await service.DeleteDraftAsync(Actor,id,version,ct);return NoContent();}
}
