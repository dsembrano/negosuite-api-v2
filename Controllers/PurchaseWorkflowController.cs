using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Models;
using negosuite_api.Services;
using negosuite_api.Contracts.Transactions;
namespace negosuite_api.Controllers;
[ApiController,Authorize,TypeFilter(typeof(ConfigUuidFilter)),TypeFilter(typeof(AuthenticatedUserFilter)),TypeFilter(typeof(AdministrationExceptionFilter))]
[Route("api/purchase-workflow")]
public class PurchaseWorkflowController(PurchaseWorkflowService service):ControllerBase
{
 User Actor=>(User)HttpContext.Items[CompanyAccessService.ActorKey];
 [HttpGet("configuration")]public async Task<ActionResult> Configuration(CancellationToken ct)=>Ok(await service.Configuration(Actor,ct));
 [HttpPut("configuration")]public async Task<ActionResult> Configure(PurchaseConfiguration value,CancellationToken ct)=>Ok(await service.Configure(Actor,value,ct));
 [HttpGet("legacy/{kind}/{id:int}")]public async Task<ActionResult> Legacy(string kind,int id,CancellationToken ct)=>Ok(await service.LegacyInfo(Actor,kind,id,ct));
 [HttpGet("{kind}")]public async Task<ActionResult> List(string kind,[FromQuery]PurchaseListQuery query,CancellationToken ct)=>Ok(await service.List(Actor,kind,query,ct));
 [HttpGet("{kind}/{id:int}")]public async Task<ActionResult> Get(string kind,int id,CancellationToken ct)=>Ok(await service.Get(Actor,kind,id,ct));
 [HttpGet("{kind}/{id:int}/related")]public async Task<ActionResult> Related(string kind,int id,CancellationToken ct)=>Ok(await service.Related(Actor,kind,id,ct));
 [HttpPost("{kind}")]public async Task<ActionResult> Create(string kind,PurchaseWorkflowWrite value,CancellationToken ct)=>Ok(await service.Save(Actor,kind,null,value,ct));
 [HttpPut("{kind}/{id:int}")]public async Task<ActionResult> Update(string kind,int id,PurchaseWorkflowWrite value,CancellationToken ct)=>Ok(await service.Save(Actor,kind,id,value,ct));
 [HttpPost("{kind}/{id:int}/post")]public async Task<ActionResult> Post(string kind,int id,PurchaseAction value,CancellationToken ct)=>Ok(await service.Post(Actor,kind,id,value,ct));
 [HttpPost("{kind}/{id:int}/cancel")]public async Task<ActionResult> Cancel(string kind,int id,PurchaseAction value,CancellationToken ct)=>Ok(await service.Cancel(Actor,kind,id,value,ct));
}
