using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using negosuite_api.Contracts.Customers;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;
// CenterSql accepts only parsed positive integers; company and all user search values stay parameterized.
#pragma warning disable EF1003
public sealed class SalesReturnService
{
    public const string Module="4130", Source="SRT";
    private readonly negosuiteContext db;
    private readonly SalesReturnSourceService sources;
    private readonly ReceivableApplicationService applications;
    public SalesReturnService(negosuiteContext db){this.db=db;sources=new(db);applications=new(db);}
    public static void Demand(User actor,params string[] actions)
    {
        Require(actor?.ConfigId!=null,"Company membership is required.",403);
        if(CompanyAccessService.IsAdmin(actor))return;
        try{var rows=JArray.Parse(actor.UserRole?.Permission??"[]");Require(rows.Any(r=>(string)r["moduleId"]==Module&&actions.Any(a=>(bool?)r[a]==true)),"Sales Return permission is required.",403);}
        catch(JsonException){throw new AdministrationException("Invalid role permissions.",403);}
    }
    private static bool CostVisible(User actor)
    {
        if(CompanyAccessService.IsAdmin(actor))return true;
        try{return !(JObject.Parse(actor.UserRole?.ColumnRestriction??"{}")["view"] as JArray??new JArray()).Any(v=>(string)v=="cost");}catch{return false;}
    }
    private static string CenterSql(User actor)
    {
        if(CompanyAccessService.IsAdmin(actor))return "";
        try{
            var parts=new List<string>();
            foreach(var row in JArray.Parse(actor.UserRole?.AdvancePermission??"[]"))
            {
                var type=(int)row["responsibilityCenterTypeId"];var ids=row["responsibilityCenterIds"].Values<int>().ToArray();Require(type>0&&ids.All(i=>i>0),"Invalid center access.",403);
                parts.Add($" AND (COALESCE(JSON_CONTAINS(JSON_EXTRACT(ResponsibilityCenterEntry,'$[*].typeId'),'{type}'),0)=0 OR "+(ids.Length==0?"FALSE":$"JSON_OVERLAPS(JSON_EXTRACT(ResponsibilityCenterEntry,'$[*].id'),JSON_ARRAY({string.Join(',',ids)}))")+")");
            }
            return string.Concat(parts);
        }catch(Exception e) when(e is not AdministrationException){throw new AdministrationException("Invalid center access.",403);}
    }
    private void Centers(User actor,string json)
    {
        if(CompanyAccessService.IsAdmin(actor))return;
        try{var values=JArray.Parse(json??"[]");foreach(var row in JArray.Parse(actor.UserRole?.AdvancePermission??"[]")){var ids=row["responsibilityCenterIds"].Values<int>().ToArray();var type=(int)row["responsibilityCenterTypeId"];Require(values.Where(c=>(int?)c["typeId"]==type).All(c=>ids.Contains((int)c["id"])),"This document is outside your responsibility center access.",403);}}
        catch(JsonException){throw new AdministrationException("Invalid center access.",403);}
    }
    public async Task<object> ListAsync(User actor,SalesReturnListRequest filter,CancellationToken ct)
    {
        Demand(actor,"canView","canCreate","canEdit");var company=Company(actor);
        Require(CustomerPagination.IsValid(filter.PageNumber,filter.PageSize),"Supply a valid page number and page size (1 to 200).");
        Require(filter.Status is -1 or 0 or 1,"Invalid status.");Require(filter.SortDirection is null or "asc" or "desc","Invalid sort direction.");
        Require(!filter.PeriodStart.HasValue||!filter.PeriodEnd.HasValue||filter.PeriodStart<=filter.PeriodEnd,"Invalid period.");
        var query=db.SalesReturns.FromSqlRaw("SELECT * FROM salesreturn WHERE UserConfigId={0}"+CenterSql(actor),company).AsNoTracking().Where(r=>r.Status==filter.Status);
        if(filter.CustomerId.HasValue)query=query.Where(r=>r.CustomerId==filter.CustomerId);
        if(filter.PeriodStart.HasValue)query=query.Where(r=>r.ReferenceDate>=filter.PeriodStart.Value.Date);
        if(filter.PeriodEnd.HasValue){var end=filter.PeriodEnd.Value.Date.AddDays(1);query=query.Where(r=>r.ReferenceDate<end);}
        if(!string.IsNullOrWhiteSpace(filter.Search)){var term=filter.Search.Trim();query=query.Where(r=>r.ReferenceNo.Contains(term)||r.Customer.Name.Contains(term)||r.Reason.Contains(term));}
        var count=filter.PageNumber.HasValue?await query.CountAsync(ct):0;var desc=filter.SortDirection!="asc";
        IOrderedQueryable<SalesReturn> sorted=(filter.SortBy??"referenceDate") switch {
            "referenceDate"=>desc?query.OrderByDescending(r=>r.ReferenceDate):query.OrderBy(r=>r.ReferenceDate),
            "referenceNo"=>desc?query.OrderByDescending(r=>r.ReferenceNo):query.OrderBy(r=>r.ReferenceNo),
            "customerName"=>desc?query.OrderByDescending(r=>r.Customer.Name):query.OrderBy(r=>r.Customer.Name),
            "amount"=>desc?query.OrderByDescending(r=>r.Amount):query.OrderBy(r=>r.Amount),
            "balance"=>desc?query.OrderByDescending(r=>r.Status==1?r.JournalEntries.Where(j=>j.AccountId==r.ReceivableAccountId&&j.Nature=="C"&&!j.PaymentToJournalEntryId.HasValue&&j.Status==1).Sum(j=>j.Balance):r.Balance):query.OrderBy(r=>r.Status==1?r.JournalEntries.Where(j=>j.AccountId==r.ReceivableAccountId&&j.Nature=="C"&&!j.PaymentToJournalEntryId.HasValue&&j.Status==1).Sum(j=>j.Balance):r.Balance),
            _=>throw new AdministrationException("Unsupported sort column.")};
        query=sorted.ThenBy(r=>r.Id);if(filter.PageNumber.HasValue)query=query.Skip((filter.PageNumber.Value-1)*filter.PageSize.Value).Take(filter.PageSize.Value);
        var data=await query.Select(r=>new {
            Row=new SalesReturnListRow(r.Id,r.ReferenceNo,r.ReferenceDate,r.Customer.Name,r.SalesInvoiceId.HasValue?"SI":"SR",r.SalesInvoiceId.HasValue?r.SalesInvoice.InvoiceNo:r.SalesReceipt.ReceiptNo,r.Amount,r.Status==1?r.JournalEntries.Where(j=>j.AccountId==r.ReceivableAccountId&&j.Nature=="C"&&!j.PaymentToJournalEntryId.HasValue&&j.Status==1).Sum(j=>j.Balance):r.Balance,r.Reason,r.Status,r.Version),
            Detached=!r.SalesInvoiceId.HasValue&&!r.SalesReceiptId.HasValue,r.SourceSnapshotJson }).ToListAsync(ct);
        var rows=data.Select(r=>{if(!r.Detached)return r.Row;var source=JsonConvert.DeserializeObject<ReturnSource>(r.SourceSnapshotJson);return r.Row with {Source=source.Source,SourceNo=source.ReferenceNo};}).ToList();
        return filter.PageNumber.HasValue?new PagedResult<SalesReturnListRow>(rows,filter.PageNumber.Value,filter.PageSize.Value,count):rows;
    }
    public async Task<object> SourceOptionsAsync(User actor,string kind,string search,int? customer,CancellationToken ct)
    {
        Demand(actor,"canView","canCreate","canEdit");Require(kind is "SI" or "SR","Invalid source.");var company=Company(actor);search=search?.Trim()??"";
        if(kind=="SI")return await db.SalesInvoices.FromSqlRaw("SELECT * FROM salesinvoice WHERE UserConfigId={0}"+CenterSql(actor),company).AsNoTracking().Where(s=>s.Status==1&&(!customer.HasValue||s.CustomerId==customer)&&(s.InvoiceNo.Contains(search)||s.Customer.Name.Contains(search))).OrderByDescending(s=>s.InvoiceDate).ThenByDescending(s=>s.Id).Take(25).Select(s=>new{id=s.Id,name=s.InvoiceNo+" - "+s.Customer.Name,customerId=s.CustomerId,date=s.InvoiceDate}).ToListAsync(ct);
        return await db.SalesReceipts.FromSqlRaw("SELECT * FROM salesreceipt WHERE UserConfigId={0}"+CenterSql(actor),company).AsNoTracking().Where(s=>s.Status==1&&(!customer.HasValue||s.CustomerId==customer)&&(s.ReceiptNo.Contains(search)||s.Customer.Name.Contains(search))).OrderByDescending(s=>s.ReceiptDate).ThenByDescending(s=>s.Id).Take(25).Select(s=>new{id=s.Id,name=s.ReceiptNo+" - "+s.Customer.Name,customerId=s.CustomerId,date=s.ReceiptDate}).ToListAsync(ct);
    }
    private async Task<Dictionary<int,decimal>> ReturnedAsync(ReturnSource source,int? exclude,CancellationToken ct)
    {
        var rows=await db.SalesReturnDetails.AsNoTracking().Where(d=>d.SalesReturn.Status==1&&d.SalesReturnId!=exclude&&(source.Source=="SI"?d.SalesReturn.SalesInvoiceId==source.Id:d.SalesReturn.SalesReceiptId==source.Id)).Select(d=>new{Id=source.Source=="SI"?d.SalesInvoiceDetailId.Value:d.SalesReceiptDetailId.Value,d.Quantity}).ToListAsync(ct);
        return rows.GroupBy(r=>r.Id).ToDictionary(g=>g.Key,g=>g.Sum(r=>r.Quantity));
    }
    public async Task<object> SourceAsync(User actor,string kind,int id,CancellationToken ct)
    {
        Demand(actor,"canView","canCreate","canEdit");var source=await sources.LoadAsync(Company(actor),kind,id,ct);Centers(actor,source.ResponsibilityCenterEntry);return PublicSource(source,await ReturnedAsync(source,null,ct),CostVisible(actor));
    }
    private static object PublicSource(ReturnSource s,Dictionary<int,decimal> returned,bool cost)=>new {
        s.Source,s.Id,s.ReferenceNo,s.ReferenceDate,s.CustomerId,s.CustomerName,s.BillingAddress,s.BillingContactName,s.BillingContactEmail,s.ResponsibilityCenterEntry,s.InventoryLocationId,s.Amount,s.ReceivableJournalId,
        Lines=s.Lines.Select(l=>new{l.Id,l.ItemId,l.Name,l.Unit,l.Quantity,l.Rate,Cost=cost?(decimal?)l.Cost:null,l.DiscountAmount,l.TaxAmount,l.TaxName,l.TrackInventory,ReturnedQuantity=returned.GetValueOrDefault(l.Id),RemainingQuantity=l.Quantity-returned.GetValueOrDefault(l.Id),Amount=l.Components.Sum(c=>c.Nature=="D"?c.Amount:-c.Amount)})};
    public async Task<object> TargetsAsync(User actor,int customer,CancellationToken ct)
    {
        Demand(actor,"canView","canCreate","canEdit");var company=Company(actor);var account=await db.Configs.Where(c=>c.Id==company).Select(c=>c.ARTradeAccountId).SingleAsync(ct);
        return await db.JournalEntries.FromSqlRaw("SELECT * FROM journalentry WHERE UserConfigId={0}"+CenterSql(actor),company).AsNoTracking().Where(j=>j.CustomerId==customer&&j.AccountId==account&&j.Nature=="D"&&j.Status==1&&j.Balance>0).OrderBy(j=>j.JournalDate).ThenBy(j=>j.Id).Select(j=>new{journalEntryId=j.Id,invoiceNo=j.ReferenceNo,invoiceDate=j.JournalDate,j.Amount,j.Balance}).ToListAsync(ct);
    }
    private async Task<SalesReturn> FindAsync(User actor,int id,CancellationToken ct)
    {
        var company=Company(actor);var row=await db.SalesReturns.Include(r=>r.Customer).Include(r=>r.SalesReturnDetails).Include(r=>r.JournalEntries).ThenInclude(j=>j.Account).SingleOrDefaultAsync(r=>r.Id==id&&r.UserConfigId==company,ct);
        Require(row!=null,"Sales Return not found.",404);Centers(actor,row.ResponsibilityCenterEntry);return row;
    }
    public async Task<object> GetAsync(User actor,int id,CancellationToken ct,bool print=false){Demand(actor,print?new[]{"canPrint"}:new[]{"canView","canCreate","canEdit"});return await PublicAsync(actor,await FindAsync(actor,id,ct),ct);}
    private async Task<object> PublicAsync(User actor,SalesReturn row,CancellationToken ct)
    {
        var s=JsonConvert.DeserializeObject<ReturnSource>(row.SourceSnapshotJson);var cost=CostVisible(actor);
        var targetIds=row.JournalEntries.Where(j=>j.PaymentToJournalEntryId.HasValue).Select(j=>j.PaymentToJournalEntryId.Value).ToArray();
        var targetNames=await db.JournalEntries.AsNoTracking().Where(j=>j.UserConfigId==row.UserConfigId&&targetIds.Contains(j.Id)).ToDictionaryAsync(j=>j.Id,j=>j.ReferenceNo,ct);
        return new{row.Id,row.ReferenceNo,row.ReferenceDate,row.CustomerId,CustomerName=row.Customer?.Name??s.CustomerName,row.InventoryLocationId,row.Reason,row.Notes,row.ResponsibilityCenterEntry,row.Amount,Balance=row.Status==1?row.JournalEntries.Where(j=>j.AccountId==row.ReceivableAccountId&&j.Nature=="C"&&!j.PaymentToJournalEntryId.HasValue&&j.Status==1).Sum(j=>j.Balance):row.Balance,row.DiscountAmount,row.TaxAmount,row.Status,row.Version,row.RequestKey,row.PostedDate,row.VoidedDate,row.VoidReason,
            Source=s.Source,SourceId=s.Id,SourceDocument=PublicSource(s,await ReturnedAsync(s,null,ct),cost),
            Lines=row.SalesReturnDetails.Select(d=>new{d.Id,SourceDetailId=d.SalesInvoiceDetailId??d.SalesReceiptDetailId??d.OriginalSourceDetailId,d.ItemId,Name=d.ItemName,d.Unit,d.Quantity,d.Rate,Cost=cost?(decimal?)d.Cost:null,d.Amount,d.DiscountAmount,d.TaxAmount,d.TaxName,TrackInventory=d.IsInventoryTransaction,d.Notes}),
            Applications=row.Status==0||(row.Status==-1&&row.ApplicationsJson!=null)?JsonConvert.DeserializeObject<List<ReceivableApplication>>(row.ApplicationsJson??"[]"):row.JournalEntries.Where(j=>j.PaymentToJournalEntryId.HasValue).Select(j=>new ReceivableApplication{JournalEntryId=j.PaymentToJournalEntryId.Value,Amount=j.Amount,InvoiceNo=targetNames.GetValueOrDefault(j.PaymentToJournalEntryId.Value)}).ToList(),
            JournalEntries=row.JournalEntries.Where(j=>cost||!row.SalesReturnDetails.Any(d=>JsonConvert.DeserializeObject<List<ReturnComponent>>(d.ComponentsJson).Any(c=>(c.Kind=="cost"||c.Kind=="inventory")&&c.AccountId==j.AccountId))).Select(j=>new{j.Id,j.AccountId,AccountName=j.Account?.Name,j.Nature,j.Amount,j.Balance,j.CustomerId,j.SupplierId,j.ResponsibilityCenterEntry,j.PaymentToJournalEntryId,j.Status})};
    }
    private async Task<(ReturnSource Source,List<SalesReturnDetail> Lines)> CalculateAsync(User actor,SalesReturnWriteRequest input,int? exclude,CancellationToken ct)
    {
        Require(input!=null&&Guid.TryParse(input.RequestKey,out _)&&input.ReferenceDate.Year>=1900&&input.ReferenceDate==input.ReferenceDate.Date&&!string.IsNullOrWhiteSpace(input.Reason)&&input.Reason.Length<=250,"Complete the return date and reason.");
        Require(input.Lines!=null&&input.Lines.Count>0&&input.Lines.All(l=>l!=null&&l.Quantity>0&&l.Quantity==Math.Round(l.Quantity,4))&&input.Lines.Select(l=>l.SourceDetailId).Distinct().Count()==input.Lines.Count,"Select distinct source lines with positive quantities (up to four decimals).");
        Require(input.Applications!=null&&input.Applications.All(a=>a!=null&&a.Amount>0&&a.Amount==Math.Round(a.Amount,2))&&input.Applications.Select(a=>a.JournalEntryId).Distinct().Count()==input.Applications.Count,"Invalid or duplicate credit applications.");
        var company=Company(actor);var s=await sources.LoadAsync(company,input.Source,input.SourceId,ct);Centers(actor,s.ResponsibilityCenterEntry);
        Require(input.ReferenceDate>=s.ReferenceDate.Date,"Return date cannot be before the original invoice date.");
        var prior=await ReturnedAsync(s,exclude,ct);
        var priorLines=await db.SalesReturnDetails.AsNoTracking().Where(d=>d.SalesReturn.Status==1&&d.SalesReturnId!=exclude&&(s.Source=="SI"?d.SalesReturn.SalesInvoiceId==s.Id:d.SalesReturn.SalesReceiptId==s.Id)).ToListAsync(ct);
        var result=new List<SalesReturnDetail>();
        foreach(var line in input.Lines)
        {
            var source=s.Lines.SingleOrDefault(l=>l.Id==line.SourceDetailId);Require(source!=null,"Return line does not belong to the source invoice.");
            var already=prior.GetValueOrDefault(source.Id);Require(already+line.Quantity<=source.Quantity,$"Return quantity exceeds the remaining quantity for {source.Name}.",409);
            var used=priorLines.Where(d=>(d.SalesInvoiceDetailId??d.SalesReceiptDetailId)==source.Id).ToList();
            var usedComponents=used.SelectMany(d=>JsonConvert.DeserializeObject<List<ReturnComponent>>(d.ComponentsJson)).ToList();
            decimal Share(decimal original,decimal consumed)=>Math.Round((original-consumed)*line.Quantity/(source.Quantity-already),2);
            var components=source.Components.Select(c=>new ReturnComponent{SourceJournalId=c.SourceJournalId,AccountId=c.AccountId,AccountName=c.AccountName,Nature=c.Nature,Amount=Share(c.Amount,usedComponents.Where(u=>u.SourceJournalId==c.SourceJournalId).Sum(u=>u.Amount)),Kind=c.Kind,SupplierId=c.SupplierId,CustomerId=c.CustomerId,ResponsibilityCenterEntry=c.ResponsibilityCenterEntry}).ToList();
            result.Add(new(){SalesInvoiceDetailId=s.Source=="SI"?source.Id:null,SalesReceiptDetailId=s.Source=="SR"?source.Id:null,ItemId=source.ItemId,ItemName=source.Name,Unit=source.Unit,Quantity=line.Quantity,Rate=source.Rate,Cost=source.Cost,Amount=components.Sum(c=>c.Nature=="D"?c.Amount:-c.Amount),DiscountAmount=Share(source.DiscountAmount,used.Sum(d=>d.DiscountAmount)),TaxAmount=components.Where(c=>c.Kind=="tax").Sum(c=>c.Amount),TaxRateId=source.TaxRateId,TaxName=source.TaxName,IsInventoryTransaction=source.TrackInventory,Notes=line.Notes,ComponentsJson=JsonConvert.SerializeObject(components)});
        }
        if(result.Any(l=>l.IsInventoryTransaction))Require(input.InventoryLocationId.HasValue,"Select a warehouse for returned inventory.");
        if(input.InventoryLocationId.HasValue)Require(await db.InventoryLocations.AnyAsync(l=>l.Id==input.InventoryLocationId&&l.UserConfigId==company,ct),"Invalid warehouse for this company.");
        Require(result.All(l=>l.Amount>=0)&&input.Applications.Sum(a=>a.Amount)<=result.Sum(l=>l.Amount),"Applied credit exceeds the return amount.");
        foreach(var a in input.Applications){var target=await db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(j=>j.Id==a.JournalEntryId&&j.UserConfigId==company,ct);Require(target!=null&&target.CustomerId==s.CustomerId&&target.AccountId==s.ReceivableAccountId&&target.Nature=="D"&&target.Status==1&&target.Balance>=a.Amount,"An invoice allocation is no longer available. Reload its balance.",409);Centers(actor,target.ResponsibilityCenterEntry);}
        return(s,result);
    }
    public async Task<object> PreviewAsync(User actor,SalesReturnWriteRequest input,CancellationToken ct)
    {
        Demand(actor,"canCreate","canEdit");var calculated=await CalculateAsync(actor,input,null,ct);var row=Build(actor,input,calculated.Source,calculated.Lines);row.JournalEntries=BuildJournals(actor,row,calculated.Source,input.Applications);return await PublicAsync(actor,row,ct);
    }
    private static SalesReturn Build(User actor,SalesReturnWriteRequest input,ReturnSource s,List<SalesReturnDetail> lines)=>new(){UserConfigId=actor.ConfigId.Value,ReferenceNo=input.ReferenceNo?.Trim(),ReferenceDate=input.ReferenceDate,CustomerId=s.CustomerId,ReceivableAccountId=s.ReceivableAccountId,SalesInvoiceId=s.Source=="SI"?s.Id:null,SalesReceiptId=s.Source=="SR"?s.Id:null,InventoryLocationId=input.InventoryLocationId,Reason=input.Reason.Trim(),Notes=input.Notes,ResponsibilityCenterEntry=s.ResponsibilityCenterEntry,SourceSnapshotJson=JsonConvert.SerializeObject(s),ApplicationsJson=JsonConvert.SerializeObject(input.Applications),Amount=lines.Sum(l=>l.Amount),Balance=lines.Sum(l=>l.Amount)-input.Applications.Sum(a=>a.Amount),DiscountAmount=lines.Sum(l=>l.DiscountAmount),TaxAmount=lines.Sum(l=>l.TaxAmount),Taxes=JsonConvert.SerializeObject(lines.Where(l=>l.TaxRateId.HasValue).GroupBy(l=>l.TaxRateId).Select(g=>new{taxRate=new{id=g.Key,name=g.First().TaxName},amount=g.Sum(l=>l.TaxAmount)})),Status=0,Version=1,RequestKey=input.RequestKey,RequestHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input)))),CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id,SalesReturnDetails=lines};
    private List<JournalEntry> BuildJournals(User actor,SalesReturn row,ReturnSource source,List<ReceivableApplication> allocations)
    {
        var result=new List<JournalEntry>();
        foreach(var group in row.SalesReturnDetails.SelectMany(l=>JsonConvert.DeserializeObject<List<ReturnComponent>>(l.ComponentsJson)).GroupBy(c=>c.SourceJournalId))
        {
            var c=group.First();var amount=group.Sum(c=>c.Amount);if(amount==0)continue;
            result.Add(new(){UserConfigId=row.UserConfigId,ReferenceNo=row.ReferenceNo??"Preview",JournalDate=row.ReferenceDate,AccountId=c.AccountId,Account=new Account{Id=c.AccountId,Name=c.AccountName},Nature=c.Nature,Amount=amount,Balance=amount,CustomerId=c.CustomerId,SupplierId=c.SupplierId,ResponsibilityCenterEntry=c.ResponsibilityCenterEntry,Source=Source,Status=1,PostedDate=DateTime.UtcNow,PostedByUserId=actor.Id,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id,Notes=row.Reason});
        }
        void Credit(decimal amount,int? target){if(amount==0)return;result.Add(new(){UserConfigId=row.UserConfigId,ReferenceNo=row.ReferenceNo??"Preview",JournalDate=row.ReferenceDate,AccountId=source.ReceivableAccountId,Account=new Account{Id=source.ReceivableAccountId,Name="Accounts Receivable"},Nature="C",Amount=amount,Balance=target.HasValue?0:amount,CustomerId=row.CustomerId,ResponsibilityCenterEntry=row.ResponsibilityCenterEntry,PaymentToJournalEntryId=target,Source=Source,Status=1,PostedDate=DateTime.UtcNow,PostedByUserId=actor.Id,CreatedDate=DateTime.UtcNow,CreatedByUserId=actor.Id,Notes=row.Reason});}
        foreach(var a in allocations)Credit(a.Amount,a.JournalEntryId);Credit(row.Amount-allocations.Sum(a=>a.Amount),null);
        Require(result.Sum(j=>j.Nature=="D"?j.Amount:-j.Amount)==0,"Return journal entries do not balance.");return result;
    }
    public async Task<object> SaveAsync(User actor,int? id,SalesReturnWriteRequest input,CancellationToken ct)
    {
        Demand(actor,id.HasValue?"canEdit":"canCreate");var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);
        if(!id.HasValue){var previous=await db.SalesReturns.AsNoTracking().FirstOrDefaultAsync(r=>r.UserConfigId==company&&r.RequestKey==input.RequestKey,ct);if(previous!=null){Require(previous.RequestHash==Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(input)))),"This creation request was already used for different data. Open the saved return.",409);return await GetAsync(actor,previous.Id,ct);}}
        var old=id.HasValue?await FindAsync(actor,id.Value,ct):null;
        if(old!=null){Require(old.Status==0,"Posted or voided returns cannot be edited.");Require(old.Version==input.Version,"This return changed in another session. Reload before saving.",409);}
        var calculated=await CalculateAsync(actor,input,null,ct);var next=Build(actor,input,calculated.Source,calculated.Lines);
        if(old==null)
        {
            var config=await db.Configs.SingleAsync(c=>c.Id==company,ct);var settings=JObject.Parse(config.AutoReferenceNoConfig??"{}");
            if((bool?)settings["autoSRTReferenceNo"]==true)
            {
                var seq=await db.TransactionSequences.SingleOrDefaultAsync(s=>s.UserConfigId==company&&s.Source==Source,ct);if(seq==null){seq=new(){UserConfigId=company,Source=Source,LastSequence=0};db.TransactionSequences.Add(seq);}seq.LastSequence++;
                next.ReferenceNo=((string)settings["autoSRTReferenceNoPrefix"]??"")+AutoReferenceNoConfig.getFormattedSequenceNo(seq.LastSequence,(string)settings["autoSRTReferenceNoFormat"]);
            }
            Require(!string.IsNullOrWhiteSpace(next.ReferenceNo)&&next.ReferenceNo.Length<=50,"Enter a return number or enable automatic numbering.");
            db.SalesReturns.Add(next);
        }
        else
        {
            Require(next.ReferenceNo==old.ReferenceNo,"The assigned return number cannot be changed.");
            db.SalesReturnDetails.RemoveRange(old.SalesReturnDetails);old.SalesReturnDetails.Clear();
            next.Id=old.Id;next.CreatedDate=old.CreatedDate;next.CreatedByUserId=old.CreatedByUserId;next.RequestKey=old.RequestKey;next.RequestHash=old.RequestHash;next.Version=old.Version+1;next.LastUpdatedDate=DateTime.UtcNow;next.LastUpdatedByUserId=actor.Id;
            db.Entry(old).CurrentValues.SetValues(next);foreach(var line in next.SalesReturnDetails)old.SalesReturnDetails.Add(line);next=old;
        }
        Require(!await db.SalesReturns.AnyAsync(r=>r.UserConfigId==company&&r.ReferenceNo==next.ReferenceNo&&r.Id!=next.Id,ct),"Return number already exists.",409);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await GetAsync(actor,next.Id,ct);
    }
    public async Task<object> PostAsync(User actor,int id,long version,CancellationToken ct)
    {
        var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);
        Require(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('inventorytransaction','salestransaction') AND VIEW_DEFINITION LIKE '%salesreturn%'").SingleAsync(ct)==2,"Sales Return reporting migrations must be installed before posting or voiding.",409);
        var row=await FindAsync(actor,id,ct);
        Demand(actor,row.CreatedByUserId==actor.Id?new[]{"canCreate","canEdit"}:new[]{"canEdit"});
        if(row.Status==1&&row.Version==version+1)return await PublicAsync(actor,row,ct);
        Require(row.Status==0&&row.Version==version,"The return is no longer the draft/version you opened.",409);
        var saved=JsonConvert.DeserializeObject<ReturnSource>(row.SourceSnapshotJson);await sources.LockAsync(company,saved.Source,saved.Id,ct);var current=await sources.LoadAsync(company,saved.Source,saved.Id,ct);
        Require(JToken.DeepEquals(JToken.FromObject(current),JToken.Parse(row.SourceSnapshotJson)),"The source invoice or its accounting configuration changed. Reopen and save the draft before posting.",409);
        var input=new SalesReturnWriteRequest{RequestKey=row.RequestKey,Source=saved.Source,SourceId=saved.Id,ReferenceNo=row.ReferenceNo,ReferenceDate=row.ReferenceDate,InventoryLocationId=row.InventoryLocationId,Reason=row.Reason,Notes=row.Notes,Lines=row.SalesReturnDetails.Select(l=>new SalesReturnLineRequest{SourceDetailId=l.SalesInvoiceDetailId??l.SalesReceiptDetailId.Value,Quantity=l.Quantity,Notes=l.Notes}).ToList(),Applications=JsonConvert.DeserializeObject<List<ReceivableApplication>>(row.ApplicationsJson??"[]")};
        var calculated=await CalculateAsync(actor,input,null,ct);
        Require(calculated.Lines.Sum(l=>l.Amount)==row.Amount&&calculated.Lines.All(l=>row.SalesReturnDetails.Any(d=>(d.SalesInvoiceDetailId??d.SalesReceiptDetailId)==(l.SalesInvoiceDetailId??l.SalesReceiptDetailId)&&JToken.DeepEquals(JToken.Parse(d.ComponentsJson),JToken.Parse(l.ComponentsJson)))),"Another return changed the remaining quantities or rounding. Save the draft again before posting.",409);
        await applications.LockAsync(company,input.Applications.Select(a=>a.JournalEntryId),ct);
        foreach(var a in input.Applications)await applications.ApplyAsync(company,row.CustomerId,current.ReceivableAccountId,a.JournalEntryId,a.Amount,ct);
        var journals=BuildJournals(actor,row,current,input.Applications);foreach(var j in journals){j.Account=null;row.JournalEntries.Add(j);}row.Status=1;row.Version++;row.PostedDate=DateTime.UtcNow;row.PostedByUserId=actor.Id;row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;row.ApplicationsJson=null;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await GetAsync(actor,id,ct);
    }
    public async Task<object> VoidAsync(User actor,int id,SalesReturnActionRequest input,CancellationToken ct)
    {
        Demand(actor,"canDelete");var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);
        Require(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('inventorytransaction','salestransaction') AND VIEW_DEFINITION LIKE '%salesreturn%'").SingleAsync(ct)==2,"Sales Return reporting migrations must be installed before posting or voiding.",409);
        var row=await FindAsync(actor,id,ct);
        Require(row.Version==input.Version&&row.Status==1,"Only the current posted version can be voided.",409);Require(!string.IsNullOrWhiteSpace(input.Reason)&&input.Reason.Length<=250,"Enter a void reason (up to 250 characters).");
        var source=JsonConvert.DeserializeObject<ReturnSource>(row.SourceSnapshotJson);Require(!row.JournalEntries.Any(j=>j.AccountId==source.ReceivableAccountId&&j.Nature=="C"&&!j.PaymentToJournalEntryId.HasValue&&j.Balance!=j.Amount),"Unapplied credit has already been consumed. Reverse its application first.");
        Require(!await db.JournalEntries.AnyAsync(j=>j.Status==1&&j.PaymentToJournalEntryId.HasValue&&row.JournalEntries.Select(x=>x.Id).Contains(j.PaymentToJournalEntryId.Value),ct),"Credit from this return has already been used elsewhere.");
        await applications.LockAsync(company,row.JournalEntries.Where(j=>j.PaymentToJournalEntryId.HasValue).Select(j=>j.PaymentToJournalEntryId.Value),ct);
        foreach(var j in row.JournalEntries){if(j.PaymentToJournalEntryId.HasValue)await applications.ApplyAsync(company,row.CustomerId,source.ReceivableAccountId,j.PaymentToJournalEntryId.Value,-j.Amount,ct);j.Status=-1;j.Balance=0;j.LastUpdatedDate=DateTime.UtcNow;j.LastUpdatedByUserId=actor.Id;}
        row.Status=-1;row.Balance=0;row.Version++;row.VoidedDate=DateTime.UtcNow;row.VoidedByUserId=actor.Id;row.VoidReason=input.Reason.Trim();row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await PublicAsync(actor,row,ct);
    }
    public async Task DeleteDraftAsync(User actor,int id,long version,CancellationToken ct)
    {
        Demand(actor,"canDelete");await using var tx=await LockCompanyAsync(db,Company(actor),ct);var row=await FindAsync(actor,id,ct);Require(row.Status==0&&row.Version==version,"Only the current draft can be deleted.",409);row.Status=-1;row.Balance=0;row.Version++;row.VoidedDate=DateTime.UtcNow;row.VoidedByUserId=actor.Id;row.VoidReason="Draft deleted";await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
