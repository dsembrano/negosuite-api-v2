using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using negosuite_api.Services;
namespace negosuite_api.Controllers;
[ApiController,Authorize,TypeFilter(typeof(ConfigUuidFilter)),TypeFilter(typeof(AuthenticatedUserFilter)),TypeFilter(typeof(AdministrationExceptionFilter))]
[Route("api/sales-workflow")]
public class SalesWorkflowController(SalesWorkflowService service):ControllerBase
{
    User Actor=>(User)HttpContext.Items[CompanyAccessService.ActorKey];
    [HttpGet("configuration")] public async Task<ActionResult> Configuration(CancellationToken ct)=>Ok(await service.Configuration(Actor,ct));
    [HttpPut("configuration")] public async Task<ActionResult> Configure(SalesConfiguration value,CancellationToken ct)=>Ok(await service.Configure(Actor,value,ct));
    [HttpGet("stock")] public async Task<ActionResult> Stock(int itemId,int inventoryLocationId,CancellationToken ct)=>Ok(await service.Stock(Actor,itemId,inventoryLocationId,ct));
    [HttpGet("{kind}")] public async Task<ActionResult> List(string kind,[FromQuery]SalesReturnListRequest query,CancellationToken ct)=>Ok(await service.List(Actor,kind,query,ct));
    [HttpGet("{kind}/{id:int}")] public async Task<ActionResult> Get(string kind,int id,CancellationToken ct)=>Ok(await service.Get(Actor,kind,id,ct));
    [HttpGet("{kind}/{id:int}/related")] public async Task<ActionResult> Related(string kind,int id,CancellationToken ct)=>Ok(await service.Related(Actor,kind,id,ct));
    [HttpPost("{kind}")] public async Task<ActionResult> Create(string kind,SalesWorkflowWrite value,CancellationToken ct)=>Ok(await service.Save(Actor,kind,null,value,ct));
    [HttpPut("{kind}/{id:int}")] public async Task<ActionResult> Update(string kind,int id,SalesWorkflowWrite value,CancellationToken ct)=>Ok(await service.Save(Actor,kind,id,value,ct));
    [HttpPost("{kind}/{id:int}/post")] public async Task<ActionResult> Post(string kind,int id,SalesWorkflowAction value,CancellationToken ct)=>Ok(await service.Post(Actor,kind,id,value,ct));
    [HttpPost("{kind}/{id:int}/cancel")] public async Task<ActionResult> Cancel(string kind,int id,SalesWorkflowAction value,CancellationToken ct)=>Ok(kind is "SI" or "SR"?await service.CancelInvoice(Actor,kind,id,value,ct):await service.Cancel(Actor,kind,id,value,ct));
    [HttpPost("QT/{id:int}/disposition")] public async Task<ActionResult> Disposition(int id,SalesWorkflowAction value,CancellationToken ct)=>Ok(await service.Disposition(Actor,id,value,ct));
    [HttpPost("invoices")] public async Task<ActionResult> Invoice(DeliveryInvoiceWrite value,CancellationToken ct)=>Ok(await service.Invoice(Actor,value,ct));
}
