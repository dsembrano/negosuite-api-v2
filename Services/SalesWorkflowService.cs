using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;

public sealed partial class SalesWorkflowService(negosuiteContext db)
{
    public static string Module(string kind) => kind switch { "QT"=>"4101", "SO"=>"4102", "DR"=>"4103", "SI"=>"4110", "SR"=>"4120", "SRT"=>"4130", "PR"=>"4125", _=>throw new AdministrationException("Unknown sales document type.") };
    public static void Demand(User actor,string kind,params string[] actions)
    {
        Company(actor); var module=Module(kind);
        if(CompanyAccessService.IsAdmin(actor))return;
        try { Require(JArray.Parse(actor.UserRole?.Permission??"[]").Any(p=>(string)p["moduleId"]==module&&actions.Any(a=>(bool?)p[a]==true)),"Permission for this sales document is required.",403); }
        catch(JsonException){throw new AdministrationException("Invalid role permissions.",403);}
    }
    static string Hash(object input)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input))));
    static decimal Cents(decimal n)=>Math.Round(n,2,MidpointRounding.AwayFromZero);
    static void Quantity(decimal n)=>Require(n>0&&n<=999999999&&decimal.Round(n,4)==n,"Enter a positive quantity with at most four decimal places.");
    static void Date(DateTime d)=>Require(d.Year>=1900&&d==d.Date,"Enter a valid date without a time.");
    static bool CostVisible(User actor)
    {
        if(CompanyAccessService.IsAdmin(actor))return true;
        try{return !(JObject.Parse(actor.UserRole?.ColumnRestriction??"{}")["view"] as JArray??new()).Any(x=>(string)x=="cost");}catch{return false;}
    }
    public static async Task<bool> Installed(negosuiteContext context,CancellationToken ct)=>await context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='salesconfiguration'").SingleAsync(ct)>0;
    public async Task<SalesConfiguration> Configuration(User actor,CancellationToken ct)
    {
        var company=Company(actor);
        return await db.SalesConfigurations.AsNoTracking().SingleOrDefaultAsync(c=>c.UserConfigId==company,ct)??new(){UserConfigId=company,Version=0};
    }
    public async Task<SalesConfiguration> Configure(User actor,SalesConfiguration input,CancellationToken ct)
    {
        Admin(actor); var company=Company(actor);
        Require(input.SalesProcessingMode is "DIRECT" or "ORDER_TO_SALES" or "BOTH","Invalid sales processing mode.");
        Require(input.CogsRecognitionPoint is "DELIVERY" or "INVOICE","Invalid COGS recognition point.");
        await using var tx=await LockCompanyAsync(db,company,ct);
        var row=await db.SalesConfigurations.SingleOrDefaultAsync(c=>c.UserConfigId==company,ct);
        Require((row?.Version??0)==input.Version,"Sales configuration changed. Reload it.",409);
        if(row==null){row=new(){UserConfigId=company,Version=0,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id};db.SalesConfigurations.Add(row);}
        row.SalesProcessingMode=input.SalesProcessingMode;row.EnableSalesQuotation=input.EnableSalesQuotation;row.CogsRecognitionPoint=input.CogsRecognitionPoint;row.EnableInventoryCommitment=input.EnableInventoryCommitment;row.AllowNegativeInventory=input.AllowNegativeInventory;row.Version++;row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return row;
    }
    async Task Available(User actor,string kind,CancellationToken ct)
    {
        Require(kind is "QT" or "SO" or "DR","Unknown operational document type.");var cfg=await Configuration(actor,ct);
        Require(cfg.SalesProcessingMode!="DIRECT","Order-to-sales processing is disabled.");
        Require(kind!="QT"||cfg.EnableSalesQuotation,"Sales quotations are disabled.");
    }
    async Task<SalesWorkflowDocument> Find(User actor,string kind,int id,CancellationToken ct)
    {
        var company=Company(actor);var row=await db.SalesWorkflowDocuments.Include(x=>x.Lines).Include(x=>x.Customer).SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company&&x.Kind==kind,ct);
        Require(row!=null,"Sales document not found.",404);PostedJournalValidator.DemandScope(actor.UserRole,row.ResponsibilityCenterEntry);return row;
    }
    async Task<Dictionary<int,decimal>> Consumed(int company,string kind,int id,CancellationToken ct)
        =>await (from h in db.DocumentRelationships where h.UserConfigId==company&&h.SourceDocumentType==kind&&h.SourceDocumentId==id&&h.Status==1
                 from l in h.Lines select l).GroupBy(l=>l.SourceLineId).Select(g=>new{Id=g.Key,Quantity=g.Sum(l=>l.Quantity)}).ToDictionaryAsync(x=>x.Id,x=>x.Quantity,ct);
    public async Task<object> Get(User actor,string kind,int id,CancellationToken ct)
    {
        Demand(actor,kind,"canView","canCreate","canEdit");var r=await Find(actor,kind,id,ct);var used=await Consumed(r.UserConfigId,kind,id,ct);
        var incoming=await db.DocumentRelationships.AsNoTracking().Include(x=>x.Lines).Where(x=>x.UserConfigId==r.UserConfigId&&x.TargetDocumentType==kind&&x.TargetDocumentId==id).ToListAsync(ct);
        var status=r.Status==-1?"CANCELLED":r.Status==0?"DRAFT":kind=="QT"?(r.ExpirationDate<DateTime.Today?"EXPIRED":used.Count>0?"CONVERTED":r.Disposition??"OPEN"):kind=="DR"?(used.Count==0?"POSTED":r.Lines.All(l=>used.GetValueOrDefault(l.Id)>=l.Quantity)?"INVOICED":"PARTIALLY_INVOICED"):(used.Count==0?"OPEN":r.Lines.All(l=>used.GetValueOrDefault(l.Id)>=l.Quantity)?"DELIVERED":"PARTIALLY_DELIVERED");
        var invoiced=new Dictionary<int,decimal>();
        if(kind=="SO")
        {
            var deliveries=await db.DocumentRelationships.AsNoTracking().Include(h=>h.Lines).Where(h=>h.UserConfigId==r.UserConfigId&&h.SourceDocumentType=="SO"&&h.SourceDocumentId==id&&h.Status==1).ToListAsync(ct);
            foreach(var delivery in deliveries){var billed=await Consumed(r.UserConfigId,"DR",delivery.TargetDocumentId,ct);foreach(var line in delivery.Lines)invoiced[line.SourceLineId]=invoiced.GetValueOrDefault(line.SourceLineId)+billed.GetValueOrDefault(line.TargetLineId);}
            if(r.Status==1&&invoiced.Values.Sum()>0)status=r.Lines.All(l=>invoiced.GetValueOrDefault(l.Id)>=l.Quantity)?"COMPLETED":"PARTIALLY_INVOICED";
        }
        return new{r.Id,r.Kind,r.ReferenceNo,r.ReferenceDate,r.CustomerId,CustomerName=r.Customer.Name,r.SupplierId,r.InventoryLocationId,r.PaymentTermId,r.RequestedDate,r.ExpirationDate,r.Salesperson,r.PriceList,r.DeliveryAddress,r.ResponsibilityCenterEntry,r.Notes,r.IsTaxExclusive,r.HasItemLevelDiscount,r.DiscountMode,r.DiscountValue,r.Amount,r.DiscountAmount,r.TaxAmount,r.Status,StatusName=status,r.Version,r.RequestKey,r.CogsRecognitionPoint,r.PostedDate,r.VoidedDate,r.VoidReason,
            Lines=r.Lines.OrderBy(l=>l.Id).Select(l=>new{l.Id,l.ItemId,l.Description,l.Unit,l.InventoryLocationId,l.Quantity,l.Rate,DiscountPercent=l.DiscountIsAmount?(decimal?)null:l.DiscountPercent,l.GrossAmount,l.DiscountAmount,l.TaxAmount,l.Amount,l.TaxRateId,l.TrackInventory,Cost=CostVisible(actor)?(decimal?)l.Cost:null,CostAmount=CostVisible(actor)?(decimal?)l.CostAmount:null,ConsumedQuantity=used.GetValueOrDefault(l.Id),RemainingQuantity=l.Quantity-used.GetValueOrDefault(l.Id),InvoicedQuantity=kind=="SO"?invoiced.GetValueOrDefault(l.Id):kind=="DR"?used.GetValueOrDefault(l.Id):0,SourceDocumentId=incoming.FirstOrDefault(h=>h.Lines.Any(d=>d.TargetLineId==l.Id))?.SourceDocumentId,SourceLineId=incoming.SelectMany(h=>h.Lines).FirstOrDefault(d=>d.TargetLineId==l.Id)?.SourceLineId})};
    }
    public async Task<object> List(User actor,string kind,SalesReturnListRequest q,CancellationToken ct)
    {
        Demand(actor,kind,"canView","canCreate","canEdit");Require(kind is "QT" or "SO" or "DR","Unknown sales document type.");Require(CustomerPagination.IsValid(q.PageNumber,q.PageSize),"Invalid paging.");Require(q.Status is -1 or 0 or 1,"Invalid status.");
        var company=Company(actor);var query=db.SalesWorkflowDocuments.AsNoTracking().Include(x=>x.Customer).Where(x=>x.UserConfigId==company&&x.Kind==kind&&x.Status==q.Status);
        if(q.CustomerId.HasValue)query=query.Where(x=>x.CustomerId==q.CustomerId);if(!string.IsNullOrWhiteSpace(q.Search))query=query.Where(x=>x.ReferenceNo.Contains(q.Search)||x.Customer.Name.Contains(q.Search));
        if(q.PeriodStart.HasValue)query=query.Where(x=>x.ReferenceDate>=q.PeriodStart);if(q.PeriodEnd.HasValue)query=query.Where(x=>x.ReferenceDate<=q.PeriodEnd);
        // Filter restricted centers BEFORE pagination/count; the result is never a permission-leaking page.
        Require(q.SortDirection==null||q.SortDirection is "asc" or "desc","Invalid sort direction.");
        Require(q.SortBy==null||q.SortBy is "referenceNo" or "referenceDate" or "customerName" or "amount" or "status","Invalid sort column.");
        Require(!q.PeriodStart.HasValue||!q.PeriodEnd.HasValue||q.PeriodStart<=q.PeriodEnd,"Invalid date range.");
        var asc=q.SortDirection=="asc";
        var sorted=q.SortBy switch { "referenceNo"=>asc?query.OrderBy(x=>x.ReferenceNo):query.OrderByDescending(x=>x.ReferenceNo), "customerName"=>asc?query.OrderBy(x=>x.Customer.Name):query.OrderByDescending(x=>x.Customer.Name), "amount"=>asc?query.OrderBy(x=>x.Amount):query.OrderByDescending(x=>x.Amount), "status"=>asc?query.OrderBy(x=>x.Status):query.OrderByDescending(x=>x.Status), _=>asc?query.OrderBy(x=>x.ReferenceDate):query.OrderByDescending(x=>x.ReferenceDate) };
        var rows=await sorted.ThenByDescending(x=>x.Id).ToListAsync(ct);
        rows=rows.Where(r=>{try{PostedJournalValidator.DemandScope(actor.UserRole,r.ResponsibilityCenterEntry);return true;}catch(AdministrationException){return false;}}).ToList();var count=rows.Count;
        if(q.PageNumber.HasValue)rows=rows.Skip((q.PageNumber.Value-1)*q.PageSize.Value).Take(q.PageSize.Value).ToList();
        var result=rows.Select(r=>new SalesWorkflowListRow(r.Id,r.Kind,r.ReferenceNo,r.ReferenceDate,r.CustomerId,r.Customer.Name,r.Amount,r.Status,r.Version)).ToList();
        return q.PageNumber.HasValue?new PagedResult<SalesWorkflowListRow>(result,q.PageNumber.Value,q.PageSize.Value,count):result;
    }
    public record SalesWorkflowListRow(int Id,string Kind,string ReferenceNo,DateTime ReferenceDate,int CustomerId,string CustomerName,decimal Amount,short Status,long Version);
    async Task<string> Number(int company,string source,DateTime date,CancellationToken ct)
    {
        var seq=await db.TransactionSequences.SingleOrDefaultAsync(x=>x.UserConfigId==company&&x.Source==source,ct);if(seq==null){seq=new(){UserConfigId=company,Source=source};db.TransactionSequences.Add(seq);}seq.LastSequence++;
        var config=await db.Configs.AsNoTracking().SingleAsync(c=>c.Id==company,ct);var settings=JObject.Parse(config.AutoReferenceNoConfig??"{}");var prefix=(string)settings["auto"+source+"ReferenceNoPrefix"]??source+"-";var format=(string)settings["auto"+source+"ReferenceNoFormat"]??"########";
        return prefix+AutoReferenceNoConfig.getFormattedSequenceNo(seq.LastSequence,format,date);
    }
    public async Task<object> Save(User actor,string kind,int? id,SalesWorkflowWrite input,CancellationToken ct)
    {
        Demand(actor,kind,id.HasValue?"canEdit":"canCreate");var company=Company(actor);Require(input!=null&&Guid.TryParse(input.RequestKey,out _),"A request key is required.");Date(input.ReferenceDate);
        Require(input.Lines is {Count:>0}&&input.Lines.Count<=500&&input.Lines.All(l=>l!=null),"Add 1 to 500 complete lines.");
        await using var tx=await LockCompanyAsync(db,company,ct);await Available(actor,kind,ct);
        var hash=Hash(input);
        if(!id.HasValue){var previous=await db.SalesWorkflowDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.UserConfigId==company&&x.RequestKey==input.RequestKey,ct);if(previous!=null){Require(previous.Kind==kind&&previous.RequestHash==hash,"Request key was used for different data.",409);return await Get(actor,kind,previous.Id,ct);}}
        var row=id.HasValue?await Find(actor,kind,id.Value,ct):new SalesWorkflowDocument{UserConfigId=company,Kind=kind,RequestKey=input.RequestKey,RequestHash=hash,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id};
        Require(row.Status==0,"Only drafts can be edited.");Require(!id.HasValue||row.Version==input.Version,"Document changed. Reload it.",409);
        Require(await db.Customers.AnyAsync(x=>x.Id==input.CustomerId&&x.UserConfigId==company,ct),"Invalid customer.");
        if(input.SupplierId.HasValue)Require(await db.Suppliers.AnyAsync(x=>x.Id==input.SupplierId&&x.UserConfigId==company,ct),"Invalid supplier.");
        if(input.PaymentTermId.HasValue)Require(await db.PaymentTerms.AnyAsync(x=>x.Id==input.PaymentTermId,ct),"Invalid payment term.");
        if(input.ExpirationDate.HasValue)Require(input.ExpirationDate>=input.ReferenceDate,"Expiration precedes quotation date.");
        PostedJournalValidator.DemandScope(actor.UserRole,input.ResponsibilityCenterEntry);
        if(id.HasValue){var links=await db.DocumentRelationships.Include(h=>h.Lines).Where(h=>h.UserConfigId==company&&h.TargetDocumentType==kind&&h.TargetDocumentId==id).ToListAsync(ct);db.DocumentLineRelationships.RemoveRange(links.SelectMany(h=>h.Lines));db.DocumentRelationships.RemoveRange(links);db.SalesWorkflowLines.RemoveRange(row.Lines);row.Lines=new();await db.SaveChangesAsync(ct);row.Version++;}
        row.ReferenceNo=string.IsNullOrWhiteSpace(input.ReferenceNo)?row.ReferenceNo??await Number(company,kind,input.ReferenceDate,ct):input.ReferenceNo.Trim();
        Require(row.ReferenceNo.Length<=50&&!await db.SalesWorkflowDocuments.AnyAsync(x=>x.UserConfigId==company&&x.Kind==kind&&x.ReferenceNo==row.ReferenceNo&&x.Id!=row.Id,ct),"Document number already exists or is too long.",409);
        row.ReferenceDate=input.ReferenceDate;row.CustomerId=input.CustomerId;row.SupplierId=input.SupplierId;row.InventoryLocationId=input.InventoryLocationId;row.PaymentTermId=input.PaymentTermId;row.RequestedDate=input.RequestedDate;row.ExpirationDate=input.ExpirationDate;row.Salesperson=input.Salesperson;row.PriceList=input.PriceList;row.DeliveryAddress=input.DeliveryAddress;row.ResponsibilityCenterEntry=input.ResponsibilityCenterEntry??"[]";row.Notes=input.Notes;row.IsTaxExclusive=input.IsTaxExclusive;row.HasItemLevelDiscount=input.HasItemLevelDiscount;row.DiscountMode=input.DiscountMode;row.DiscountValue=input.DiscountValue;
        Require(input.DiscountMode is "percent" or "amount","Invalid discount mode.");Require(input.DiscountValue>=0&&(input.DiscountMode!="percent"||input.DiscountValue<=100),"Invalid document discount.");row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;
        var config=await db.Configs.AsNoTracking().SingleAsync(c=>c.Id==company,ct);var sources=new List<(SalesWorkflowDocument Source,SalesWorkflowLine Line,SalesWorkflowLine Target)>();
        foreach(var value in input.Lines)
        {
            Quantity(value.Quantity);Require(value.Rate>=0&&value.Rate<=999999999&&decimal.Round(value.Rate,4)==value.Rate&&(!value.DiscountPercent.HasValue||(value.DiscountPercent>=0&&value.DiscountPercent<=100))&&value.DiscountAmount>=0,"Invalid rate or discount.");
            var item=await db.Items.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==value.ItemId&&x.UserConfigId==company,ct);Require(item!=null&&item.ToSell,"Choose a sellable item.");
            var line=new SalesWorkflowLine{ItemId=item.Id,Description=value.Description??item.Name,Unit=item.Unit,InventoryLocationId=value.InventoryLocationId??row.InventoryLocationId,Quantity=value.Quantity,Rate=value.Rate,DiscountPercent=value.DiscountPercent??0,DiscountIsAmount=!value.DiscountPercent.HasValue,TaxRateId=value.TaxRateId,TrackInventory=item.TrackInventory==true,SalesAccountId=item.SalesAccountId??0,InventoryAccountId=item.InventoryAccountId,CostAccountId=item.PurchaseAccountId,DiscountAccountId=config.DiscountAccountId};
            Require(line.Description.Length<=250,"Item description is too long.");
            if(line.InventoryLocationId.HasValue)Require(await db.InventoryLocations.AnyAsync(x=>x.Id==line.InventoryLocationId&&x.UserConfigId==company,ct),"Invalid warehouse.");
            Require(!line.TrackInventory||line.InventoryLocationId.HasValue,"Choose a warehouse for inventory items.");
            if(value.SourceDocumentId.HasValue||value.SourceLineId.HasValue)
            {
                Require(kind is "SO" or "DR"&&value.SourceDocumentId.HasValue&&value.SourceLineId.HasValue,"Invalid source relationship.");var sourceKind=kind=="SO"?"QT":"SO";Demand(actor,sourceKind,"canView","canCreate","canEdit");
                if(sourceKind=="QT")Require((await Configuration(actor,ct)).EnableSalesQuotation,"Quotations are disabled.");
                var source=await Find(actor,sourceKind,value.SourceDocumentId.Value,ct);Require(source.Status==1&&source.CustomerId==row.CustomerId,"Select an open source for the same customer.");
                Require(sourceKind!="QT"||source.Disposition is not ("REJECTED" or "EXPIRED")&&(!source.ExpirationDate.HasValue||source.ExpirationDate>=row.ReferenceDate),"Quotation is rejected or expired.");
                var original=source.Lines.SingleOrDefault(l=>l.Id==value.SourceLineId);Require(original!=null&&original.ItemId==item.Id,"Invalid source item.");
                var used=await Consumed(company,sourceKind,source.Id,ct);Require(value.Quantity<=original.Quantity-used.GetValueOrDefault(original.Id),"Quantity exceeds remaining source quantity.",409);
                Require(!sources.Any(s=>s.Line.Id==original.Id),"Source lines cannot be repeated.");
                Require(row.IsTaxExclusive==source.IsTaxExclusive&&row.ResponsibilityCenterEntry==source.ResponsibilityCenterEntry,"Preserve the source tax basis and responsibility centers.");
                if(kind=="DR")
                {
                    Require(line.InventoryLocationId==original.InventoryLocationId,"Delivery warehouse must match the order line.");
                    CopyCommercial(original,line,used.GetValueOrDefault(original.Id),value.Quantity);line.Quantity=value.Quantity;row.HasItemLevelDiscount=source.HasItemLevelDiscount;row.DiscountMode=source.DiscountMode;row.DiscountValue=source.DiscountValue;
                }
                sources.Add((source,original,line));
            }
            else Require(kind!="DR","Delivery lines require a Sales Order source.");
            if(kind!="DR")
            {
                var tax=line.TaxRateId.HasValue?await db.TaxRates.AsNoTracking().SingleOrDefaultAsync(t=>t.Id==line.TaxRateId&&t.UserConfigId==company,ct):null;Require(!line.TaxRateId.HasValue||tax!=null,"Invalid tax code.");
                line.TaxSnapshot=tax==null?null:JsonConvert.SerializeObject(new{id=tax.Id,name=tax.Name,rate=tax.Rate,taxAccountId=tax.TaxAccountId});
                line.GrossAmount=Cents(line.Quantity*line.Rate);var basis=row.IsTaxExclusive||tax==null||tax.Rate<=0?line.GrossAmount:Cents(line.GrossAmount/(1+(decimal)tax.Rate/100));var percent=input.HasItemLevelDiscount?value.DiscountPercent:input.DiscountMode=="percent"?(decimal?)input.DiscountValue:null;
                var gross=input.Lines.Sum(l=>l.Quantity*l.Rate);var amount=input.HasItemLevelDiscount?value.DiscountAmount:gross==0?0:value.Quantity*value.Rate*input.DiscountValue/gross;
                line.DiscountPercent=percent??0;line.DiscountIsAmount=!percent.HasValue;line.DiscountAmount=Cents(percent.HasValue?basis*percent.Value/100:amount);Require(line.DiscountAmount<=basis,"Discount cannot exceed the line amount before tax.");line.TaxAmount=Cents((basis-line.DiscountAmount)*(decimal)(tax?.Rate??0)/100);line.Amount=basis-line.DiscountAmount+line.TaxAmount;
            }
            row.Lines.Add(line);
        }
        row.Amount=row.Lines.Sum(l=>l.Amount);row.DiscountAmount=row.Lines.Sum(l=>l.DiscountAmount);row.TaxAmount=row.Lines.Sum(l=>l.TaxAmount);if(kind=="DR"&&!row.HasItemLevelDiscount&&row.DiscountMode=="amount")row.DiscountValue=row.DiscountAmount;
        if(!id.HasValue)db.SalesWorkflowDocuments.Add(row);await db.SaveChangesAsync(ct);
        foreach(var group in sources.GroupBy(s=>s.Source.Id))
        {
            var source=group.First().Source;db.DocumentRelationships.Add(new(){UserConfigId=company,SourceDocumentType=source.Kind,SourceDocumentId=source.Id,TargetDocumentType=kind,TargetDocumentId=row.Id,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id,Lines=group.Select(s=>new DocumentLineRelationship{SourceLineId=s.Line.Id,TargetLineId=s.Target.Id,Quantity=s.Target.Quantity,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id}).ToList()});
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await Get(actor,kind,row.Id,ct);
    }
    static void CopyCommercial(SalesWorkflowLine source,SalesWorkflowLine target,decimal used,decimal quantity)
    {
        target.Rate=source.Rate;target.DiscountPercent=source.DiscountPercent;target.DiscountIsAmount=source.DiscountIsAmount;target.TaxRateId=source.TaxRateId;target.TaxSnapshot=source.TaxSnapshot;target.SalesAccountId=source.SalesAccountId;target.DiscountAccountId=source.DiscountAccountId;target.InventoryAccountId=source.InventoryAccountId;target.CostAccountId=source.CostAccountId;target.TrackInventory=source.TrackInventory;target.Unit=source.Unit;
        decimal Part(decimal amount)=>SalesReturnSourceService.Portion(amount,used,quantity,source.Quantity);
        target.GrossAmount=Part(source.GrossAmount);target.DiscountAmount=Part(source.DiscountAmount);target.TaxAmount=Part(source.TaxAmount);target.Amount=Part(source.Amount);target.Cost=source.Cost;target.CostAmount=Part(source.CostAmount);
    }
    async Task ValidateSources(User actor,SalesWorkflowDocument row,CancellationToken ct)
    {
        var links=await db.DocumentRelationships.Include(h=>h.Lines).Where(h=>h.UserConfigId==row.UserConfigId&&h.TargetDocumentType==row.Kind&&h.TargetDocumentId==row.Id).ToListAsync(ct);
        foreach(var link in links)
        {
            var source=await Find(actor,link.SourceDocumentType,link.SourceDocumentId,ct);Require(source.Status==1,"Source is no longer open.",409);var used=await Consumed(row.UserConfigId,source.Kind,source.Id,ct);
            Require(source.Kind!="QT"||source.Disposition is not ("REJECTED" or "EXPIRED")&&(!source.ExpirationDate.HasValue||source.ExpirationDate>=DateTime.Today),"Quotation is no longer eligible.",409);
            foreach(var allocation in link.Lines){var original=source.Lines.Single(l=>l.Id==allocation.SourceLineId);Require(allocation.Quantity<=original.Quantity-used.GetValueOrDefault(original.Id),"Source quantity was consumed by another document.",409);if(row.Kind=="DR")CopyCommercial(original,row.Lines.Single(l=>l.Id==allocation.TargetLineId),used.GetValueOrDefault(original.Id),allocation.Quantity);}
            link.Status=1;
        }
    }
    public async Task<object> Post(User actor,string kind,int id,SalesWorkflowAction input,CancellationToken ct)
    {
        var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);var row=await Find(actor,kind,id,ct);Demand(actor,kind,row.CreatedByUserId==actor.Id?new[]{"canCreate","canEdit"}:new[]{"canEdit"});
        if(row.Status==1)return await Get(actor,kind,id,ct);
        Require(row.Status==0&&row.Version==input.Version,"Only the current draft can be posted.",409);await Available(actor,kind,ct);await ValidateSources(actor,row,ct);
        if(kind=="DR")
        {
            await RequireViews(ct);var config=await Configuration(actor,ct);row.CogsRecognitionPoint=config.CogsRecognitionPoint;
            foreach(var group in row.Lines.Where(l=>l.TrackInventory).GroupBy(l=>new{l.ItemId,l.InventoryLocationId}))
            {
                var available=await OnHand(company,group.Key.ItemId,group.Key.InventoryLocationId.Value,ct);Require(config.AllowNegativeInventory||available>=group.Sum(l=>l.Quantity),"Insufficient stock in the delivery warehouse.",409);
            }
            foreach(var line in row.Lines.Where(l=>l.TrackInventory))
            {
                var item=await db.Items.AsNoTracking().SingleAsync(i=>i.Id==line.ItemId&&i.UserConfigId==company,ct);line.Cost=item.AverageCost??item.Cost??0;Require(line.Cost>=0,"Invalid inventory cost.");line.CostAmount=Cents(line.Quantity*line.Cost);
                Require(line.InventoryAccountId.HasValue&&line.CostAccountId.HasValue,"Configure inventory and cost accounts before delivery.");
                if(row.CogsRecognitionPoint=="DELIVERY")await CostEntries(actor,row,line,ct);
            }
        }
        row.Status=1;row.Disposition="OPEN";row.Version++;row.PostedDate=DateTime.UtcNow;row.PostedByUserId=actor.Id;row.Amount=row.Lines.Sum(l=>l.Amount);row.DiscountAmount=row.Lines.Sum(l=>l.DiscountAmount);row.TaxAmount=row.Lines.Sum(l=>l.TaxAmount);
        var journals=await (from p in db.SalesPostingRecords where p.UserConfigId==company&&p.DocumentType==kind&&p.DocumentId==id join j in db.JournalEntries on p.JournalEntryId equals j.Id select j).ToArrayAsync(ct);
        await new PostedJournalValidator(db).ValidateAsync(company,row,journals,actor.UserRole,ct);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await Get(actor,kind,id,ct);
    }
    async Task RequireViews(CancellationToken ct)=>Require(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction' AND VIEW_DEFINITION LIKE '%inventorytransaction_before_salesworkflow%' AND VIEW_DEFINITION LIKE '%salesworkflowline%'").SingleAsync(ct)==1,"Install the sales workflow stock view migration before posting.",409);
    async Task<decimal> OnHand(int company,int item,int location,CancellationToken ct)=>await db.Database.SqlQuery<decimal>($"SELECT COALESCE(SUM(QuantityIn-QuantityOut),0) AS Value FROM inventorytransaction WHERE UserConfigId={company} AND ItemId={item} AND InventoryLocationId={location} AND Status=1").SingleAsync(ct);
    async Task CostEntries(User actor,SalesWorkflowDocument doc,SalesWorkflowLine line,CancellationToken ct)
    {
        if(line.CostAmount==0)return;
        foreach(var part in new[]{(Account:line.CostAccountId.Value,Nature:"D",Effect:"cost"),(Account:line.InventoryAccountId.Value,Nature:"C",Effect:"inventory")})
        {
            var j=new JournalEntry{UserConfigId=doc.UserConfigId,ReferenceNo=doc.ReferenceNo,JournalDate=doc.ReferenceDate,AccountId=part.Account,Nature=part.Nature,Amount=line.CostAmount,Balance=line.CostAmount,CustomerId=doc.CustomerId,SupplierId=doc.SupplierId,ResponsibilityCenterEntry=doc.ResponsibilityCenterEntry,Source="DR",Status=1,PostedDate=DateTime.UtcNow,PostedByUserId=actor.Id,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id};db.JournalEntries.Add(j);await db.SaveChangesAsync(ct);db.SalesPostingRecords.Add(new(){UserConfigId=doc.UserConfigId,DocumentType="DR",DocumentId=doc.Id,DocumentLineId=line.Id,JournalEntryId=j.Id,Effect=part.Effect});
        }
        await db.SaveChangesAsync(ct);
    }
    public async Task<object> Cancel(User actor,string kind,int id,SalesWorkflowAction input,CancellationToken ct)
    {
        Demand(actor,kind,"canDelete");var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);var row=await Find(actor,kind,id,ct);if(row.Status==-1)return await Get(actor,kind,id,ct);
        Require(row.Version==input.Version,"Document changed. Reload it.",409);Require(!string.IsNullOrWhiteSpace(input.Reason)&&input.Reason.Length<=250,"Enter a cancellation reason.");
        Require(!await db.DocumentRelationships.AnyAsync(h=>h.UserConfigId==company&&h.SourceDocumentType==kind&&h.SourceDocumentId==id&&h.Status==1,ct),"Cancel downstream documents first.",409);
        row.Status=-1;row.Version++;row.VoidedDate=DateTime.UtcNow;row.VoidedByUserId=actor.Id;row.VoidReason=input.Reason.Trim();await db.SaveChangesAsync(ct);
        var journals=await (from p in db.SalesPostingRecords where p.UserConfigId==company&&p.DocumentType==kind&&p.DocumentId==id join j in db.JournalEntries on p.JournalEntryId equals j.Id select j).ToListAsync(ct);foreach(var j in journals){j.Status=-1;j.LastUpdatedDate=DateTime.UtcNow;j.LastUpdatedByUserId=actor.Id;}
        var links=await db.DocumentRelationships.Where(h=>h.UserConfigId==company&&h.TargetDocumentType==kind&&h.TargetDocumentId==id).ToListAsync(ct);foreach(var link in links)link.Status=-1;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await Get(actor,kind,id,ct);
    }
    public async Task<object> Disposition(User actor,int id,SalesWorkflowAction input,CancellationToken ct)
    {
        Demand(actor,"QT","canEdit");Require(input.Disposition is "ACCEPTED" or "REJECTED" or "EXPIRED" or "OPEN","Invalid quotation disposition.");await using var tx=await LockCompanyAsync(db,Company(actor),ct);var row=await Find(actor,"QT",id,ct);Require(row.Status==1&&row.Version==input.Version,"Quotation changed.",409);Require((await Consumed(row.UserConfigId,"QT",id,ct)).Count==0,"Converted quotations cannot change disposition.",409);row.Disposition=input.Disposition;row.Version++;row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await Get(actor,"QT",id,ct);
    }
    public async Task<object> Stock(User actor,int itemId,int inventoryLocationId,CancellationToken ct)
    {
        Demand(actor,"SO","canView","canCreate","canEdit");var company=Company(actor);Require(await db.Items.AnyAsync(i=>i.Id==itemId&&i.UserConfigId==company,ct)&&await db.InventoryLocations.AnyAsync(l=>l.Id==inventoryLocationId&&l.UserConfigId==company,ct),"Invalid item or warehouse.");
        var onHand=await OnHand(company,itemId,inventoryLocationId,ct);decimal committed=0;
        if((await Configuration(actor,ct)).EnableInventoryCommitment){var orders=await db.SalesWorkflowDocuments.AsNoTracking().Include(d=>d.Lines).Where(d=>d.UserConfigId==company&&d.Kind=="SO"&&d.Status==1&&d.Lines.Any(l=>l.ItemId==itemId&&l.InventoryLocationId==inventoryLocationId&&l.TrackInventory)).ToListAsync(ct);foreach(var order in orders){var used=await Consumed(company,"SO",order.Id,ct);committed+=order.Lines.Where(l=>l.ItemId==itemId&&l.InventoryLocationId==inventoryLocationId&&l.TrackInventory).Sum(l=>l.Quantity-used.GetValueOrDefault(l.Id));}}
        return new{ItemId=itemId,InventoryLocationId=inventoryLocationId,OnHand=onHand,Committed=committed,Available=onHand-committed};
    }
}
