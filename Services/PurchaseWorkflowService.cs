using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using negosuite_api.Models;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Contracts.Customers;
using static negosuite_api.Services.AdministrationSupport;
namespace negosuite_api.Services;

public sealed partial class PurchaseWorkflowService(negosuiteContext db)
{
 public static string Module(string kind)=>kind switch {"PO"=>"4211","GR"=>"4405","PB"=>"4210","LC"=>"4310",_=>throw new AdministrationException("Unknown purchase document.")};
 static void Demand(User actor,string kind,params string[] actions)
 {
  Company(actor);var module=Module(kind);if(CompanyAccessService.IsAdmin(actor))return;
  Require(actor.UserRole?.UserConfigId==null||actor.UserRole.UserConfigId==actor.ConfigId,"Invalid company role.",403);
  try{Require(JArray.Parse(actor.UserRole?.Permission??"[]").Any(p=>(string)p["moduleId"]==module&&actions.Any(a=>(bool?)p[a]==true)),"Permission for this purchase document is required.",403);}catch(JsonException){throw new AdministrationException("Invalid permissions.",403);}
 }
 static decimal Money(decimal v)=>Math.Round(v,2,MidpointRounding.AwayFromZero);
 static decimal Four(decimal v)=>Math.Round(v,4,MidpointRounding.AwayFromZero);
 static void Date(DateTime v)=>Require(v.Year>=1900&&v==v.Date,"Enter a valid date without a time.");
 static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value))));
 static PurchaseWorkflowWrite Payload(PurchaseWorkflowDocument d)=>JsonConvert.DeserializeObject<PurchaseWorkflowWrite>(d.PayloadJson);
 public static async Task<bool> Installed(negosuiteContext db,CancellationToken ct)=>await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='purchaseconfiguration'").SingleAsync(ct)>0;
 public async Task<PurchaseConfiguration> Configuration(User actor,CancellationToken ct)=>await db.PurchaseConfigurations.AsNoTracking().SingleOrDefaultAsync(x=>x.UserConfigId==Company(actor),ct)??new(){UserConfigId=Company(actor)};
 public async Task<object> Configure(User actor,PurchaseConfiguration input,CancellationToken ct)
 {
  Admin(actor);var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);
  Require(input.ProcessingMode is "DIRECT" or "ORDER_TO_PURCHASE" or "BOTH","Choose a purchase processing mode.");
  Require(input.ProcessingMode=="DIRECT"||input.GrniAccountId.HasValue,"Select the Goods Received Not Invoiced clearing account.");
  if(input.GrniAccountId.HasValue){await Account(company,input.GrniAccountId.Value,ct);var ap=await db.Configs.Where(c=>c.Id==company).Select(c=>c.APTradeAccountId).SingleAsync(ct);Require(input.GrniAccountId!=ap,"GRNI must be separate from Accounts Payable Trade.");}
  var row=await db.PurchaseConfigurations.SingleOrDefaultAsync(x=>x.UserConfigId==company,ct);Require((row?.Version??0)==input.Version,"Purchase configuration changed. Reload it.",409);
  if(row==null){row=new(){UserConfigId=company};db.PurchaseConfigurations.Add(row);}row.ProcessingMode=input.ProcessingMode;row.GrniAccountId=input.GrniAccountId;row.Version++;
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return row;
 }
 async Task<Account> Account(int company,int id,CancellationToken ct){var a=await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);Require(a!=null,"Choose an account belonging to this company.");return a;}
 async Task<PurchaseWorkflowDocument> Find(User actor,string kind,int id,CancellationToken ct)
 {
  Demand(actor,kind,"canView","canCreate","canEdit");var d=await db.PurchaseWorkflowDocuments.SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==Company(actor)&&x.Kind==kind,ct);Require(d!=null,"Purchase document not found.",404);PostedJournalValidator.DemandScope(actor.UserRole,d.ResponsibilityCenterEntry);return d;
 }
 async Task<Dictionary<int,decimal>> Used(int company,int id,CancellationToken ct)=>await (from l in db.PurchaseWorkflowLinks join d in db.PurchaseWorkflowDocuments on l.TargetId equals d.Id where l.UserConfigId==company&&l.SourceId==id&&d.Status==1&&d.Kind!="LC" group l by l.SourceLine into g select new{Id=g.Key,Quantity=g.Sum(x=>x.Quantity)}).ToDictionaryAsync(x=>x.Id,x=>x.Quantity,ct);
 public async Task<object> Get(User actor,string kind,int id,CancellationToken ct)
 {
  var d=await Find(actor,kind,id,ct);var p=Payload(d);var used=await Used(d.UserConfigId,id,ct);for(var i=0;i<p.Lines.Count;i++){p.Lines[i].RemainingQuantity=p.Lines[i].Quantity-used.GetValueOrDefault(i+1);p.Lines[i].PreviousAverageCost=null;p.Lines[i].PreviousCost=null;p.Lines[i].PreviousPurchasedDate=null;p.Lines[i].PostAverageCost=0;p.Lines[i].PostOnHand=0;p.Lines[i].StockFingerprint=null;}
  var status=d.Status==-1?"CANCELLED":d.Status==0?"DRAFT":kind=="PO"?(used.Count==0?"OPEN":p.Lines.All(l=>l.RemainingQuantity==0)?"RECEIVED":"PARTIALLY_RECEIVED"):kind=="GR"?(used.Count==0?"POSTED":p.Lines.All(l=>l.RemainingQuantity==0)?"BILLED":"PARTIALLY_BILLED"):"POSTED";
  return new{d.Id,d.Kind,d.ReferenceNo,d.ReferenceDate,d.SupplierId,d.SupplierName,d.Status,StatusName=status,d.Version,d.LegacyId,d.Amount,d.VoidReason,d.PostedDate,Document=p};
 }
 public async Task<object> List(User actor,string kind,PurchaseListQuery q,CancellationToken ct)
 {
  Demand(actor,kind,"canView","canCreate","canEdit");Require(CustomerPagination.IsValid(q.PageNumber,q.PageSize),"Invalid pagination.");Require(q.Status is -1 or 0 or 1,"Invalid status.");Require(q.SortBy is "referenceDate" or "referenceNo" or "supplierName" or "amount" or "status","Invalid sort column.");Require(q.SortDirection is "asc" or "desc","Invalid sort direction.");Require(!q.PeriodStart.HasValue||!q.PeriodEnd.HasValue||q.PeriodStart<=q.PeriodEnd,"Invalid date range.");
  var query=db.PurchaseWorkflowDocuments.AsNoTracking().Where(d=>d.UserConfigId==Company(actor)&&d.Kind==kind&&d.Status==q.Status);
  if(q.SupplierId.HasValue)query=query.Where(d=>d.SupplierId==q.SupplierId);if(!string.IsNullOrWhiteSpace(q.Search))query=query.Where(d=>d.ReferenceNo.Contains(q.Search)||d.SupplierName.Contains(q.Search));if(q.PeriodStart.HasValue)query=query.Where(d=>d.ReferenceDate>=q.PeriodStart);if(q.PeriodEnd.HasValue)query=query.Where(d=>d.ReferenceDate<=q.PeriodEnd);
  // Scope before count/pagination; shared legacy responsibility-center rules are authoritative.
  var all=await query.ToListAsync(ct);all=all.Where(d=>{try{PostedJournalValidator.DemandScope(actor.UserRole,d.ResponsibilityCenterEntry);return true;}catch(AdministrationException){return false;}}).ToList();
  Func<PurchaseWorkflowDocument,object> key=q.SortBy switch{"referenceNo"=>d=>d.ReferenceNo,"supplierName"=>d=>d.SupplierName,"amount"=>d=>d.Amount,"status"=>d=>d.Status,_=>d=>d.ReferenceDate};var sorted=q.SortDirection=="asc"?all.OrderBy(key):all.OrderByDescending(key);var rows=sorted.ThenByDescending(d=>d.Id).AsEnumerable();if(q.PageNumber.HasValue)rows=rows.Skip((q.PageNumber.Value-1)*q.PageSize.Value).Take(q.PageSize.Value);
  var items=rows.Select(d=>new{d.Id,d.Kind,d.ReferenceNo,d.ReferenceDate,d.SupplierId,d.SupplierName,d.Amount,d.Status,d.Version,d.LegacyId}).ToArray();return q.PageNumber.HasValue?new{items,totalCount=all.Count}:(object)items;
 }
 public async Task<object> Save(User actor,string kind,int? id,PurchaseWorkflowWrite input,CancellationToken ct)
 {
  Demand(actor,kind,id.HasValue?"canEdit":"canCreate");Require(input!=null&&Guid.TryParse(input.RequestKey,out _),"Supply a request key.");var company=Company(actor);var hash=Hash(input);await using var tx=await LockCompanyAsync(db,company,ct);
  var cfg=await Configuration(actor,ct);Require(kind!="PO"||cfg.ProcessingMode!="DIRECT","New purchase orders are disabled in Direct Purchase mode.");
  if(!id.HasValue){var old=await db.PurchaseWorkflowDocuments.SingleOrDefaultAsync(d=>d.UserConfigId==company&&d.RequestKey==input.RequestKey,ct);if(old!=null){Require(old.Kind==kind&&old.RequestHash==hash,"Request key already used with different contents.",409);return await Get(actor,kind,old.Id,ct);}}
  var d=id.HasValue?await Find(actor,kind,id.Value,ct):new PurchaseWorkflowDocument{UserConfigId=company,Kind=kind,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id,RequestKey=input.RequestKey,RequestHash=hash};Require(d.Status==0&&(!id.HasValue||d.Version==input.Version),"Document changed or is already posted. Reload it.",409);
  var p=await Build(actor,kind,input,cfg,ct);var supplier=await db.Suppliers.AsNoTracking().SingleAsync(s=>s.Id==input.SupplierId&&s.UserConfigId==company,ct);
  d.ReferenceDate=p.ReferenceDate;d.SupplierId=p.SupplierId;d.SupplierName=supplier.Name;d.ResponsibilityCenterEntry=p.ResponsibilityCenterEntry;d.Amount=p.Lines.Sum(l=>l.Amount);d.PayloadJson=JsonConvert.SerializeObject(p);if(id.HasValue)d.Version++;
  if(!id.HasValue){d.ReferenceNo=string.IsNullOrWhiteSpace(input.ReferenceNo)?kind+"-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant():input.ReferenceNo.Trim();Require(!await db.PurchaseWorkflowDocuments.AnyAsync(x=>x.UserConfigId==company&&x.Kind==kind&&x.ReferenceNo==d.ReferenceNo,ct),"Reference number already exists.",409);db.PurchaseWorkflowDocuments.Add(d);}
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await Get(actor,kind,d.Id,ct);
 }
 async Task<PurchaseWorkflowWrite> Build(User actor,string kind,PurchaseWorkflowWrite input,PurchaseConfiguration cfg,CancellationToken ct)
 {
  var company=Company(actor);Date(input.ReferenceDate);Require(input.Lines is {Count:>0}&&input.Lines.Count<=500&&input.Lines.All(x=>x!=null),"Add between one and 500 item lines.");Require(await db.Suppliers.AnyAsync(s=>s.Id==input.SupplierId&&s.UserConfigId==company,ct),"Choose a supplier.");
  if(input.PaymentTermId.HasValue)Require(await db.PaymentTerms.AnyAsync(x=>x.Id==input.PaymentTermId,ct),"Invalid payment terms.");
  if(input.ExpectedDate.HasValue)Date(input.ExpectedDate.Value);if(input.DueDate.HasValue)Date(input.DueDate.Value);Require(kind!="PB"||input.DueDate>=input.ReferenceDate,"Choose a due date on or after the bill date.");
  if(input.InventoryLocationId.HasValue)Require(await db.InventoryLocations.AnyAsync(x=>x.Id==input.InventoryLocationId&&x.UserConfigId==company,ct),"Invalid warehouse.");Require(input.InventoryLocationId.HasValue,"Choose the receiving warehouse.");
  PostedJournalValidator.DemandScope(actor.UserRole,input.ResponsibilityCenterEntry);await new PostedJournalValidator(db).ValidateAsync(company,new GeneralJournal{Status=0,ResponsibilityCenterEntry=input.ResponsibilityCenterEntry},Array.Empty<JournalEntry>(),actor.UserRole,ct);
  Require(input.DiscountMode is "percent" or "amount"&&input.DiscountValue>=0&&(input.DiscountMode!="percent"||input.DiscountValue<=100),"Invalid document discount.");
  var result=JsonConvert.DeserializeObject<PurchaseWorkflowWrite>(JsonConvert.SerializeObject(input));result.Lines=new();result.GrniAccountId=kind=="GR"?cfg.GrniAccountId:null;
  if(kind=="GR")Require(result.GrniAccountId.HasValue,"Configure the GRNI clearing account before receiving goods.");
  Require(input.Lines.All(l=>l.Quantity>0&&l.Quantity<=999999999&&l.Rate>=0&&l.Rate<=999999999),"Quantity or unit price is outside the supported range.");
  var sources=new HashSet<(int,int)>();
  foreach(var value in input.Lines)
  {
   Require(value.Quantity>0&&value.Quantity<=999999999&&Four(value.Quantity)==value.Quantity,"Enter a positive quantity with at most four decimal places.");
   var item=await db.Items.AsNoTracking().SingleOrDefaultAsync(i=>i.Id==value.ItemId&&i.UserConfigId==company,ct);Require(item!=null,"Choose an item belonging to this company.");
   Require(item.TrackInventory==true,"Goods-receipt purchasing requires inventory-tracked items. Use a direct bill for services and expenses.");
   var l=new PurchaseLine{ItemId=item.Id,Description=item.Name,Unit=item.Unit,Quantity=value.Quantity,InventoryAccountId=item.InventoryAccountId??0,CostAccountId=item.PurchaseAccountId};Require(l.InventoryAccountId>0,"Configure the item's inventory account.");
   if(kind=="PO")
   {
    Require(value.Rate>=0&&value.Rate<=999999999&&Four(value.Rate)==value.Rate,"Invalid unit price.");Require(value.DiscountPercent==null||value.DiscountPercent>=0&&value.DiscountPercent<=100,"Invalid item discount.");Require(value.DiscountAmount>=0,"Invalid item discount amount.");
    l.Rate=value.Rate;l.TaxRateId=value.TaxRateId;var tax=value.TaxRateId.HasValue?await db.TaxRates.AsNoTracking().SingleOrDefaultAsync(t=>t.Id==value.TaxRateId&&t.UserConfigId==company,ct):null;Require(!value.TaxRateId.HasValue||tax!=null,"Choose a valid tax rate.");l.TaxPercent=tax?.Rate??0;l.TaxAccountId=tax?.TaxAccountId;
    var gross=Money(value.Quantity*value.Rate);var basis=input.IsTaxExclusive?gross:Money(gross/(1+l.TaxPercent/100));var percent=input.HasItemLevelDiscount?value.DiscountPercent:input.DiscountMode=="percent"?(decimal?)input.DiscountValue:null;var totalGross=input.Lines.Sum(x=>x.Quantity*x.Rate);var discount=input.HasItemLevelDiscount?value.DiscountAmount:totalGross==0?0:value.Quantity*value.Rate*input.DiscountValue/totalGross;
    l.DiscountPercent=percent;l.DiscountAmount=Money(percent.HasValue?basis*percent.Value/100:discount);Require(l.DiscountAmount<=basis,"Discount exceeds the line's base value.");l.NetAmount=basis-l.DiscountAmount;l.TaxAmount=Money(l.NetAmount*l.TaxPercent/100);l.Amount=l.NetAmount+l.TaxAmount;
   }
   else
   {
    Require(value.SourceDocumentId.HasValue&&value.SourceLineId.HasValue&&sources.Add((value.SourceDocumentId.Value,value.SourceLineId.Value)),"Select unique source lines.");var source=await Find(actor,kind=="GR"?"PO":"GR",value.SourceDocumentId.Value,ct);var original=Payload(source);Require(source.Status==1&&source.SupplierId==input.SupplierId&&source.ReferenceDate<=input.ReferenceDate,"Choose a posted source for this supplier dated on or before this document.",409);Require(original.ResponsibilityCenterEntry==input.ResponsibilityCenterEntry&&original.InventoryLocationId==input.InventoryLocationId,"Preserve source warehouse and responsibility centers.");
    Require(value.SourceLineId>0&&value.SourceLineId<=original.Lines.Count,"Invalid source line.");var line=original.Lines[value.SourceLineId.Value-1];Require(line.ItemId==value.ItemId,"Source item does not match.");var used=(await Used(company,source.Id,ct)).GetValueOrDefault(value.SourceLineId.Value);Require(kind=="LC"||value.Quantity<=line.Quantity-used,"Quantity exceeds the remaining source quantity.",409);
    l=JsonConvert.DeserializeObject<PurchaseLine>(JsonConvert.SerializeObject(line));l.Quantity=value.Quantity;l.SourceDocumentId=source.Id;l.SourceLineId=value.SourceLineId;l.LegacyLineId=null;l.LandedCosts=new();l.RemainingQuantity=0;l.InventoryAdjustment=0;l.CogsAdjustment=0;l.AdjustmentAccountId=null;
    decimal Part(decimal amount)=>SalesReturnSourceService.Portion(amount,used,value.Quantity,line.Quantity);
    l.NetAmount=kind=="LC"?0:Part(line.NetAmount);l.TaxAmount=kind=="LC"?0:Part(line.TaxAmount);l.Rate=Four(l.NetAmount/value.Quantity);l.DiscountAmount=0;l.DiscountPercent=0;
    if(kind=="GR")
    {
     Require(value.LandedCosts!=null&&value.LandedCosts.All(c=>c!=null&&c.Amount>=0&&c.Amount<=999999999&&Four(c.Amount)==c.Amount),"Invalid per-unit landed costs.");Require(value.LandedCosts.Select(c=>c.AccountId).Distinct().Count()==value.LandedCosts.Count,"Do not repeat a landed-cost account.");
     foreach(var cost in value.LandedCosts){await Account(company,cost.AccountId,ct);Require(cost.AccountId!=l.InventoryAccountId,"Landed-cost clearing account cannot be the inventory account.");l.LandedCosts.Add(new(){AccountId=cost.AccountId,Amount=cost.Amount});}l.Amount=l.NetAmount+Money(l.Quantity*l.LandedCosts.Sum(c=>c.Amount));
    }
    else if(kind=="PB"){result.GrniAccountId=original.GrniAccountId;Require(original.GrniAccountId.HasValue,"Receipt clearing snapshot is missing.");l.Amount=l.NetAmount+l.TaxAmount;}
    else
    {
     Require(kind=="LC"&&!string.IsNullOrWhiteSpace(input.Notes),"Enter the adjustment reason and allocation basis.");Require(value.Quantity==line.Quantity,"Cost adjustments refer to the complete receipt line, without changing its quantity.");Require(value.AdjustmentAccountId.HasValue,"Select the landed-cost clearing account.");
     await Account(company,value.AdjustmentAccountId.Value,ct);Require(value.AdjustmentAccountId!=l.InventoryAccountId&&value.AdjustmentAccountId!=l.CostAccountId,"Choose a separate landed-cost clearing account.");
     Require(value.InventoryAdjustment is >= -999999999 and <= 999999999&&value.CogsAdjustment is >= -999999999 and <= 999999999,"Adjustment amounts exceed the supported range.");
     Require(Money(value.InventoryAdjustment)==value.InventoryAdjustment&&Money(value.CogsAdjustment)==value.CogsAdjustment,"Adjustment amounts require at most two decimal places.");Require(value.InventoryAdjustment+value.CogsAdjustment!=0,"Enter the cost difference allocated to inventory and/or COGS.");Require(value.InventoryAdjustment==0||value.CogsAdjustment==0||Math.Sign(value.InventoryAdjustment)==Math.Sign(value.CogsAdjustment),"Inventory and COGS adjustments must have the same direction.");
     l.InventoryAdjustment=value.InventoryAdjustment;l.CogsAdjustment=value.CogsAdjustment;l.AdjustmentAccountId=value.AdjustmentAccountId;l.Amount=value.InventoryAdjustment+value.CogsAdjustment;l.TaxAmount=0;l.NetAmount=0;
    }
   }
   Require(Math.Abs(l.Amount)<=999999999999m,"Line amount exceeds the supported range.");result.Lines.Add(l);
  }
  if(kind!="PO")Require(result.Lines.Select(l=>l.SourceDocumentId).Distinct().Count()==1,"Select one source document per transaction.");
  return result;
 }
 public async Task<object> Related(User actor,string kind,int id,CancellationToken ct)
 {
  var root=await Find(actor,kind,id,ct);var links=await db.PurchaseWorkflowLinks.AsNoTracking().Where(l=>l.UserConfigId==root.UserConfigId).ToListAsync(ct);var seen=new HashSet<int>{id};var pending=new Queue<int>();pending.Enqueue(id);
  while(pending.Count>0){var current=pending.Dequeue();foreach(var other in links.Where(l=>l.SourceId==current||l.TargetId==current).Select(l=>l.SourceId==current?l.TargetId:l.SourceId)){if(seen.Add(other)){Require(seen.Count<=1000,"Document history is too large.");pending.Enqueue(other);}}}
  var rows=await db.PurchaseWorkflowDocuments.AsNoTracking().Where(d=>d.UserConfigId==root.UserConfigId&&seen.Contains(d.Id)).ToListAsync(ct);return rows.Where(d=>{try{Demand(actor,d.Kind,"canView","canCreate","canEdit");PostedJournalValidator.DemandScope(actor.UserRole,d.ResponsibilityCenterEntry);return true;}catch(AdministrationException){return false;}}).Select(d=>new{d.Id,d.Kind,d.ReferenceNo,d.Status,d.LegacyId}).ToArray();
 }
 public async Task<object> LegacyInfo(User actor,string kind,int id,CancellationToken ct)
 {
  Demand(actor,kind,"canView","canCreate","canEdit");if(!await Installed(db,ct))return new{linked=false};var d=await db.PurchaseWorkflowDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.UserConfigId==Company(actor)&&x.Kind==kind&&x.LegacyId==id,ct);if(d==null)return new{linked=false};PostedJournalValidator.DemandScope(actor.UserRole,d.ResponsibilityCenterEntry);return new{linked=true,d.Id,d.Kind,d.ReferenceNo};
 }
 public static async Task GuardLegacy(negosuiteContext db,int company,string kind,int id,bool creating,CancellationToken ct,object input=null)
 {
  if(!await Installed(db,ct))return;
  Require(id==0||!await db.PurchaseWorkflowDocuments.AnyAsync(d=>d.UserConfigId==company&&d.Kind==kind&&d.LegacyId==id,ct),"This document belongs to Purchase Processing. Open it there to preserve source quantities and costs.",409);
  if(creating&&kind=="PB"){var lines=(input as negosuite_api.Contracts.Bills.BillWriteRequest)?.BillDetails;var itemIds=lines?.Where(l=>l!=null).Select(l=>l.ItemId).Distinct().ToArray()??Array.Empty<int>();var expenseOnly=lines is {Count:>0}&&lines.All(l=>l!=null)&&itemIds.Length==await db.Items.CountAsync(i=>i.UserConfigId==company&&itemIds.Contains(i.Id)&&i.TrackInventory!=true,ct);Require(expenseOnly||!await db.PurchaseConfigurations.AnyAsync(c=>c.UserConfigId==company&&c.ProcessingMode=="ORDER_TO_PURCHASE",ct),"Inventory purchases require a posted Goods Receipt in this mode. Noninventory bills can be entered directly.",409);}
 }
}
