using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static negosuite_api.Services.AdministrationSupport;
namespace negosuite_api.Services;

public sealed partial class SalesWorkflowService
{
    public async Task<object> Invoice(User actor,DeliveryInvoiceWrite input,CancellationToken ct)
    {
        Require(input?.Kind is "SI" or "SR","Choose Charge Invoice or Cash Invoice.");Demand(actor,input.Kind,"canCreate");Demand(actor,"DR","canView","canCreate","canEdit");
        Require(Guid.TryParse(input.RequestKey,out _)&&input.Lines is {Count:>0}&&input.Lines.Count<=500,"Supply a request key and delivery quantities.");Date(input.ReferenceDate);if(input.Kind=="SI"){Date(input.DueDate);Require(input.DueDate>=input.ReferenceDate,"Due date precedes invoice date.");}
        var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);var hash=Hash(input);
        var previous=await db.SalesWorkflowInvoices.AsNoTracking().SingleOrDefaultAsync(i=>i.UserConfigId==company&&i.RequestKey==input.RequestKey,ct);
        if(previous!=null){Require(previous.RequestHash==hash,"Request key was used for another invoice.",409);return new{previous.Kind,Id=previous.InvoiceId,previous.Status,previous.Version};}
        await RequireViews(ct);await Available(actor,"DR",ct);
        Require(input.Lines.All(l=>l!=null)&&input.Lines.Select(l=>l.LineId).Distinct().Count()==input.Lines.Count,"Delivery lines cannot be repeated.");
        var selections=new List<(SalesWorkflowDocument Delivery,SalesWorkflowLine Source,SalesWorkflowLine Slice)>();
        foreach(var value in input.Lines)
        {
            Quantity(value.Quantity);var delivery=await Find(actor,"DR",value.DeliveryId,ct);Require(delivery.Status==1,"Select a posted delivery.");Require(delivery.ReferenceDate<=input.ReferenceDate,"Invoice date precedes delivery.");
            var source=delivery.Lines.SingleOrDefault(l=>l.Id==value.LineId);Require(source!=null,"Delivery line not found.");var consumed=await Consumed(company,"DR",delivery.Id,ct);var used=consumed.GetValueOrDefault(source.Id);Require(value.Quantity<=source.Quantity-used,"Invoice quantity exceeds the remaining delivered quantity.",409);
            var slice=new SalesWorkflowLine{ItemId=source.ItemId,Description=source.Description,Unit=source.Unit,InventoryLocationId=source.InventoryLocationId,Quantity=value.Quantity};CopyCommercial(source,slice,used,value.Quantity);selections.Add((delivery,source,slice));
        }
        var first=selections[0].Delivery;
        Require(selections.All(s=>s.Delivery.CustomerId==first.CustomerId&&s.Delivery.SupplierId==first.SupplierId&&s.Delivery.IsTaxExclusive==first.IsTaxExclusive&&s.Delivery.ResponsibilityCenterEntry==first.ResponsibilityCenterEntry&&s.Delivery.PaymentTermId==first.PaymentTermId),"Combine only deliveries with the same customer, supplier, terms, tax basis and responsibility centers.");
        var config=await db.Configs.AsNoTracking().SingleAsync(c=>c.Id==company,ct);Require(config.ARTradeAccountId.HasValue,"Configure Accounts Receivable Trade.");
        if(input.Kind=="SR"){Require(input.PaymentModeId.HasValue&&await db.PaymentModes.AnyAsync(m=>m.Id==input.PaymentModeId,ct),"Select a payment method.");Require(input.DepositToAccountId.HasValue&&await db.Accounts.AnyAsync(a=>a.Id==input.DepositToAccountId&&a.UserConfigId==company,ct),"Select a deposit account.");}
        var number=await Number(company,input.Kind,input.ReferenceDate,ct);var amount=selections.Sum(s=>s.Slice.Amount);Require(amount>0,"Invoice amount must be positive.");
        var taxGroups=selections.Where(s=>s.Slice.TaxRateId.HasValue).GroupBy(s=>s.Slice.TaxSnapshot).Select(g=>new{taxRate=JObject.Parse(g.Key),amount=g.Sum(s=>s.Slice.TaxAmount)}).ToArray();var taxes=JsonConvert.SerializeObject(taxGroups);
        var now=DateTime.UtcNow;SalesInvoice si=null;SalesReceipt sr=null;int invoiceId;
        if(input.Kind=="SI")
        {
            si=new(){UserConfigId=company,CustomerId=first.CustomerId,SupplierId=first.SupplierId,InvoiceNo=number,InvoiceDate=input.ReferenceDate,DueDate=input.DueDate,PaymentTermId=first.PaymentTermId,ShippingAddress=first.DeliveryAddress,InventoryLocationId=first.InventoryLocationId,ResponsibilityCenterEntry=first.ResponsibilityCenterEntry,Amount=amount,Balance=amount,DiscountAmount=selections.Sum(s=>s.Slice.DiscountAmount),Taxes=taxes,IsTaxExclusive=first.IsTaxExclusive,HasItemLevelDiscount=true,Status=1,PostedDate=now,CreatedDate=now,CreatedByUserId=actor.Id,LastUpdatedDate=now};
            foreach(var s in selections)si.SalesInvoiceDetails.Add(new(){ItemId=s.Slice.ItemId,Quantity=s.Slice.Quantity,Rate=s.Slice.Rate,Cost=s.Slice.Cost,Amount=s.Slice.GrossAmount,DiscountAmount=s.Slice.DiscountAmount,DiscountPercent=s.Slice.DiscountPercent,TaxRateId=s.Slice.TaxRateId,TaxAmount=s.Slice.TaxAmount,TaxExemptAmount=0,IsInventoryTransaction=s.Slice.TrackInventory,InventoryLocationId=s.Slice.InventoryLocationId,Notes=s.Slice.Description,Status=1,CreatedDate=now,CreatedByUserId=actor.Id,PostedDate=now});
            db.SalesInvoices.Add(si);await db.SaveChangesAsync(ct);invoiceId=si.Id;
        }
        else
        {
            sr=new(){UserConfigId=company,CustomerId=first.CustomerId,ReceiptNo=number,ReceiptDate=input.ReferenceDate,PaymentModeId=input.PaymentModeId,DepositToAccountId=input.DepositToAccountId,ShippingAddress=first.DeliveryAddress,InventoryLocationId=first.InventoryLocationId,ResponsibilityCenterEntry=first.ResponsibilityCenterEntry,Amount=amount,Balance=0,DiscountAmount=selections.Sum(s=>s.Slice.DiscountAmount),Taxes=taxes,IsTaxExclusive=first.IsTaxExclusive,HasItemLevelDiscount=true,IsPOS=false,Status=1,PostedDate=now,CreatedDate=now,CreatedByUserId=actor.Id,LastUpdatedDate=now};
            foreach(var s in selections)sr.SalesReceiptDetails.Add(new(){ItemId=s.Slice.ItemId,Quantity=s.Slice.Quantity,Rate=s.Slice.Rate,Cost=s.Slice.Cost,Amount=s.Slice.GrossAmount,DiscountAmount=s.Slice.DiscountAmount,DiscountPercent=s.Slice.DiscountPercent,TaxRateId=s.Slice.TaxRateId,TaxAmount=s.Slice.TaxAmount,TaxExemptAmount=0,IsInventoryTransaction=s.Slice.TrackInventory,InventoryLocationId=s.Slice.InventoryLocationId,Notes=s.Slice.Description,Status=1,CreatedDate=now,CreatedByUserId=actor.Id,PostedDate=now});
            db.SalesReceipts.Add(sr);await db.SaveChangesAsync(ct);invoiceId=sr.Id;
        }
        var targetIds=input.Kind=="SI"?si.SalesInvoiceDetails.Select(l=>l.Id).ToArray():sr.SalesReceiptDetails.Select(l=>l.Id).ToArray();
        var journals=new List<JournalEntry>();
        async Task<JournalEntry> Add(int account,string nature,decimal value,int? lineId,string effect)
        {
            Require(account>0,"A captured posting account is missing.");var j=new JournalEntry{UserConfigId=company,SalesInvoiceId=si?.Id,SalesReceiptId=sr?.Id,ReferenceNo=number,JournalDate=input.ReferenceDate,AccountId=account,Nature=nature,Amount=value,Balance=value,CustomerId=first.CustomerId,SupplierId=first.SupplierId,ResponsibilityCenterEntry=first.ResponsibilityCenterEntry,Source=input.Kind,Status=1,PostedDate=now,PostedByUserId=actor.Id,CreatedDate=now,CreatedByUserId=actor.Id,DueDate=si?.DueDate};db.JournalEntries.Add(j);await db.SaveChangesAsync(ct);journals.Add(j);db.SalesPostingRecords.Add(new(){UserConfigId=company,DocumentType=input.Kind,DocumentId=invoiceId,DocumentLineId=lineId,JournalEntryId=j.Id,Effect=effect});return j;
        }
        var settlement=await Add(input.Kind=="SI"?config.ARTradeAccountId.Value:input.DepositToAccountId.Value,"D",amount,null,"settlement");
        var customer=await db.Customers.AsNoTracking().SingleAsync(c=>c.Id==first.CustomerId,ct);
        var snapshot=new ReturnSource{Source=input.Kind,Id=invoiceId,ReferenceNo=number,ReferenceDate=input.ReferenceDate,CustomerId=first.CustomerId,CustomerName=customer.Name,ResponsibilityCenterEntry=first.ResponsibilityCenterEntry,InventoryLocationId=first.InventoryLocationId,Amount=amount,ReceivableAccountId=config.ARTradeAccountId.Value,ReceivableJournalId=input.Kind=="SI"?settlement.Id:null};
        for(var n=0;n<selections.Count;n++)
        {
            var selected=selections[n];var line=selected.Slice;var target=targetIds[n];
            var returned=new ReturnSourceLine{Id=target,ItemId=line.ItemId,Name=line.Description,Unit=line.Unit,Quantity=line.Quantity,Rate=line.Rate,Cost=line.Cost,GrossAmount=line.GrossAmount,DiscountAmount=line.DiscountAmount,TaxAmount=line.TaxAmount,TaxRateId=line.TaxRateId,TaxName=line.TaxSnapshot==null?null:(string)JObject.Parse(line.TaxSnapshot)["name"],TrackInventory=line.TrackInventory};
            async Task Component(int account,string nature,decimal value,string effect)
            {
                if(value==0)return;var j=await Add(account,nature,value,target,effect);var name=await db.Accounts.Where(a=>a.Id==account).Select(a=>a.Name).SingleAsync(ct);returned.Components.Add(new(){SourceJournalId=j.Id,AccountId=account,AccountName=name,Nature=nature=="C"?"D":"C",Amount=value,Kind=effect,CustomerId=first.CustomerId,SupplierId=first.SupplierId,ResponsibilityCenterEntry=first.ResponsibilityCenterEntry});
            }
            await Component(line.SalesAccountId,"C",line.Amount-line.TaxAmount+(line.DiscountAccountId.HasValue?line.DiscountAmount:0),"revenue");
            if(line.DiscountAccountId.HasValue)await Component(line.DiscountAccountId.Value,"D",line.DiscountAmount,"discount");
            if(line.TaxAmount>0)await Component((int?)JObject.Parse(line.TaxSnapshot)["taxAccountId"]??0,"C",line.TaxAmount,"tax");
            if(line.TrackInventory&&line.CostAmount>0)
            {
                if(selected.Delivery.CogsRecognitionPoint=="INVOICE"){await Component(line.InventoryAccountId.Value,"C",line.CostAmount,"inventory");await Component(line.CostAccountId.Value,"D",line.CostAmount,"cost");}
                else
                {
                    foreach(var effect in new[]{"inventory","cost"})
                    {
                        var posting=await (from p in db.SalesPostingRecords where p.UserConfigId==company&&p.DocumentType=="DR"&&p.DocumentId==selected.Delivery.Id&&p.DocumentLineId==selected.Source.Id&&p.Effect==effect join j in db.JournalEntries.Include(j=>j.Account) on p.JournalEntryId equals j.Id select j).SingleAsync(ct);
                        returned.Components.Add(new(){SourceJournalId=posting.Id,AccountId=posting.AccountId,AccountName=posting.Account.Name,Nature=posting.Nature=="C"?"D":"C",Amount=line.CostAmount,Kind=effect,CustomerId=first.CustomerId,SupplierId=first.SupplierId,ResponsibilityCenterEntry=posting.ResponsibilityCenterEntry});
                    }
                }
            }
            snapshot.Lines.Add(returned);
        }
        await new PostedJournalValidator(db).ValidateAsync(company,(object)si??sr,journals.ToArray(),actor.UserRole,ct);
        foreach(var group in selections.Select((s,n)=>new{s,n}).GroupBy(x=>x.s.Delivery.Id))db.DocumentRelationships.Add(new(){UserConfigId=company,SourceDocumentType="DR",SourceDocumentId=group.Key,TargetDocumentType=input.Kind,TargetDocumentId=invoiceId,Status=1,CreatedDate=now,CreatedByUserId=actor.Id,Lines=group.Select(x=>new DocumentLineRelationship{SourceLineId=x.s.Source.Id,TargetLineId=targetIds[x.n],Quantity=x.s.Slice.Quantity,CreatedDate=now,CreatedByUserId=actor.Id}).ToList()});
        db.SalesWorkflowInvoices.Add(new(){UserConfigId=company,Kind=input.Kind,InvoiceId=invoiceId,RequestKey=input.RequestKey,RequestHash=hash,ReturnSourceJson=JsonConvert.SerializeObject(snapshot),CreatedDate=now,CreatedByUserId=actor.Id});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new{Kind=input.Kind,Id=invoiceId,ReferenceNo=number,Status=1,Version=1};
    }
    public async Task<object> CancelInvoice(User actor,string kind,int id,SalesWorkflowAction input,CancellationToken ct)
    {
        Demand(actor,kind,"canDelete");Require(kind is "SI" or "SR","Invalid invoice type.");var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);
        var record=await db.SalesWorkflowInvoices.SingleOrDefaultAsync(r=>r.UserConfigId==company&&r.Kind==kind&&r.InvoiceId==id,ct);
        if(record==null){var result=await CancelDirect(actor,kind,id,input,ct);await tx.CommitAsync(ct);return result;}
        var snapshot=JsonConvert.DeserializeObject<ReturnSource>(record.ReturnSourceJson);PostedJournalValidator.DemandScope(actor.UserRole,snapshot.ResponsibilityCenterEntry);
        if(record.Status==-1)return new{Kind=kind,Id=id,record.Status,record.Version};Require(record.Version==input.Version,"Invoice changed.",409);Require(!string.IsNullOrWhiteSpace(input.Reason)&&input.Reason.Length<=250,"Enter a cancellation reason.");
        Require(!await db.SalesReturns.AnyAsync(r=>r.UserConfigId==company&&r.Status==1&&(kind=="SI"?r.SalesInvoiceId==id:r.SalesReceiptId==id),ct),"Void posted returns first.",409);
        var journals=await db.JournalEntries.Where(j=>j.UserConfigId==company&&(kind=="SI"?j.SalesInvoiceId==id:j.SalesReceiptId==id)).ToListAsync(ct);var ids=journals.Select(j=>j.Id).ToArray();Require(!await db.JournalEntries.AnyAsync(j=>j.Status==1&&j.PaymentToJournalEntryId.HasValue&&ids.Contains(j.PaymentToJournalEntryId.Value),ct),"Reverse invoice payments and credit applications first.",409);
        record.Status=-1;record.Version++;record.VoidedDate=DateTime.UtcNow;record.VoidedByUserId=actor.Id;record.VoidReason=input.Reason.Trim();await db.SaveChangesAsync(ct);
        foreach(var j in journals){j.Status=-1;j.LastUpdatedDate=DateTime.UtcNow;j.LastUpdatedByUserId=actor.Id;}
        if(kind=="SI"){var row=await db.SalesInvoices.Include(i=>i.SalesInvoiceDetails).SingleAsync(i=>i.Id==id&&i.UserConfigId==company,ct);row.Status=-1;row.Balance=0;row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;foreach(var l in row.SalesInvoiceDetails)l.Status=-1;}
        else{var row=await db.SalesReceipts.Include(i=>i.SalesReceiptDetails).SingleAsync(i=>i.Id==id&&i.UserConfigId==company,ct);row.Status=-1;row.Balance=0;row.LastUpdatedDate=DateTime.UtcNow;row.LastUpdatedByUserId=actor.Id;foreach(var l in row.SalesReceiptDetails)l.Status=-1;}
        foreach(var link in await db.DocumentRelationships.Where(h=>h.UserConfigId==company&&h.TargetDocumentType==kind&&h.TargetDocumentId==id).ToListAsync(ct))link.Status=-1;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new{Kind=kind,Id=id,record.Status,record.Version};
    }
    public static async Task GuardLegacy(negosuiteContext context,int company,string kind,int id,short? status,CancellationToken ct,bool deleting=false)
    {
        if(!await Installed(context,ct))return;
        Require(id==0||!await context.SalesWorkflowInvoices.AnyAsync(r=>r.UserConfigId==company&&r.Kind==kind&&r.InvoiceId==id,ct),"This delivery-based invoice is read-only here. Cancel it through Sales Workflow.",409);
        var cfg=await context.SalesConfigurations.AsNoTracking().SingleOrDefaultAsync(c=>c.UserConfigId==company,ct);
        if(deleting&&cfg!=null)Require(kind=="SI"?!await context.SalesInvoices.AnyAsync(i=>i.Id==id&&i.UserConfigId==company&&i.Status!=0,ct):!await context.SalesReceipts.AnyAsync(i=>i.Id==id&&i.UserConfigId==company&&i.Status!=0,ct),"Posted invoices must be cancelled from the invoice editor, not deleted.",409);
        Require(id!=0||cfg?.SalesProcessingMode!="ORDER_TO_SALES","Create this invoice from a posted delivery.",409);
        // Existing direct documents retain their historical path after a configuration switch.
    }
    async Task<object> CancelDirect(User actor,string kind,int id,SalesWorkflowAction input,CancellationToken ct)
    {
        var company=Company(actor);Require(!string.IsNullOrWhiteSpace(input.Reason)&&input.Reason.Length<=250,"Enter a cancellation reason.");
        SalesInvoice si=null;SalesReceipt sr=null;
        if(kind=="SI")si=await db.SalesInvoices.Include(i=>i.SalesInvoiceDetails).Include(i=>i.JournalEntries).SingleOrDefaultAsync(i=>i.UserConfigId==company&&i.Id==id,ct);
        else sr=await db.SalesReceipts.Include(i=>i.SalesReceiptDetails).Include(i=>i.JournalEntries).SingleOrDefaultAsync(i=>i.UserConfigId==company&&i.Id==id,ct);
        Require(si!=null||sr!=null,"Invoice not found.",404);var status=si?.Status??sr.Status;PostedJournalValidator.DemandScope(actor.UserRole,si!=null?si.ResponsibilityCenterEntry:sr.ResponsibilityCenterEntry);
        if(status==-1)return new{Kind=kind,Id=id,Status=-1,Version=0};Require(status==1,"Only posted invoices can be cancelled.");
        Require((si!=null?si.LastUpdatedDate:sr.LastUpdatedDate)==input.LastUpdatedDate,"Invoice changed. Reload before cancelling.",409);
        Require(sr==null||sr.IsPOS!=true&&(string.IsNullOrWhiteSpace(sr.PaymentDetails)||sr.PaymentDetails=="[]"),"Cancel POS or split payments in their original workflow.");
        Require(!await db.SalesReturns.AnyAsync(r=>r.UserConfigId==company&&r.Status==1&&(kind=="SI"?r.SalesInvoiceId==id:r.SalesReceiptId==id),ct),"Void posted returns first.",409);
        var journals=(si?.JournalEntries??sr.JournalEntries).ToArray();var ids=journals.Select(j=>j.Id).ToArray();
        Require(!await db.JournalEntries.AnyAsync(j=>j.Status==1&&j.PaymentToJournalEntryId.HasValue&&ids.Contains(j.PaymentToJournalEntryId.Value),ct),"Reverse invoice payments and credit applications first.",409);
        Require(journals.All(j=>!j.PaymentToJournalEntryId.HasValue),"Reverse outgoing journal applications before cancelling this invoice.",409);
        var stamp=DateTime.UtcNow;var audit="Cancelled by user "+actor.Id+" at "+stamp.ToString("O")+": "+input.Reason.Trim();
        foreach(var journal in journals){journal.Status=-1;journal.LastUpdatedDate=stamp;journal.LastUpdatedByUserId=actor.Id;}
        if(si!=null){si.Status=-1;si.Balance=0;si.LastUpdatedDate=stamp;si.LastUpdatedByUserId=actor.Id;si.Notes=string.IsNullOrEmpty(si.Notes)?audit:si.Notes+"\n"+audit;foreach(var line in si.SalesInvoiceDetails)line.Status=-1;}
        else{sr.Status=-1;sr.Balance=0;sr.LastUpdatedDate=stamp;sr.LastUpdatedByUserId=actor.Id;sr.Notes=string.IsNullOrEmpty(sr.Notes)?audit:sr.Notes+"\n"+audit;foreach(var line in sr.SalesReceiptDetails)line.Status=-1;}
        await db.SaveChangesAsync(ct);return new{Kind=kind,Id=id,Status=-1,Version=0};
    }
    public static async Task<ReturnSource> ReturnSnapshot(negosuiteContext context,int company,string kind,int id,CancellationToken ct)
    {
        if(!await Installed(context,ct))return null;var saved=await context.SalesWorkflowInvoices.AsNoTracking().SingleOrDefaultAsync(r=>r.UserConfigId==company&&r.Kind==kind&&r.InvoiceId==id,ct);if(saved==null)return null;Require(saved.Status==1,"Source invoice is no longer posted.",409);
        Require(kind=="SI"?await context.SalesInvoices.AnyAsync(i=>i.Id==id&&i.UserConfigId==company&&i.Status==1,ct):await context.SalesReceipts.AnyAsync(i=>i.Id==id&&i.UserConfigId==company&&i.Status==1,ct),"Source invoice is no longer posted.",409);return JsonConvert.DeserializeObject<ReturnSource>(saved.ReturnSourceJson);
    }
    public async Task<object> Related(User actor,string kind,int id,CancellationToken ct)
    {
        var company=Company(actor);Demand(actor,kind,"canView","canCreate","canEdit");var root=await DocumentInfo(actor,kind,id,ct);Require(root!=null,"Document not found.",404);
        var visited=new HashSet<(string Kind,int Id)>{(kind,id)};var queue=new Queue<(string Kind,int Id)>();queue.Enqueue((kind,id));var nodes=new List<object>{root};var edges=new List<object>();
        while(queue.Count>0)
        {
            var current=queue.Dequeue();Require(visited.Count<=1000,"Document history is too large. Open a nearer related document.");
            var links=await db.DocumentRelationships.AsNoTracking().Where(h=>h.UserConfigId==company&&(h.SourceDocumentType==current.Kind&&h.SourceDocumentId==current.Id||h.TargetDocumentType==current.Kind&&h.TargetDocumentId==current.Id)).ToListAsync(ct);
            if(current.Kind=="SRT")
            {
                var source=await db.SalesReturns.AsNoTracking().SingleAsync(r=>r.Id==current.Id&&r.UserConfigId==company,ct);
                if(source.SalesInvoiceId.HasValue||source.SalesReceiptId.HasValue)links.Add(new(){SourceDocumentType=source.SalesInvoiceId.HasValue?"SI":"SR",SourceDocumentId=source.SalesInvoiceId??source.SalesReceiptId.Value,TargetDocumentType="SRT",TargetDocumentId=source.Id,Status=source.Status});
            }
            if(current.Kind=="PR")
            {
                var targets=await (from payment in db.JournalEntries where payment.UserConfigId==company&&payment.SalesInvoicePaymentId==current.Id join target in db.JournalEntries on payment.PaymentToJournalEntryId equals (int?)target.Id where target.SalesInvoiceId.HasValue select target.SalesInvoiceId.Value).Distinct().ToListAsync(ct);
                foreach(var target in targets)links.Add(new(){SourceDocumentType="SI",SourceDocumentId=target,TargetDocumentType="PR",TargetDocumentId=current.Id,Status=1});
            }
            foreach(var h in links)
            {
                var other=h.SourceDocumentType==current.Kind&&h.SourceDocumentId==current.Id?(h.TargetDocumentType,h.TargetDocumentId):(h.SourceDocumentType,h.SourceDocumentId);
                object info;try{info=await DocumentInfo(actor,other.Item1,other.Item2,ct);}catch(AdministrationException ex) when(ex.Status==403){continue;}if(info==null)continue;
                edges.Add(new{h.Id,h.SourceDocumentType,h.SourceDocumentId,h.TargetDocumentType,h.TargetDocumentId,h.Status});if(visited.Add(other)){nodes.Add(info);queue.Enqueue(other);}
            }
            if(current.Kind is "SI" or "SR")
            {
                var returns=await db.SalesReturns.AsNoTracking().Where(r=>r.UserConfigId==company&&(current.Kind=="SI"?r.SalesInvoiceId==current.Id:r.SalesReceiptId==current.Id)).Select(r=>r.Id).ToListAsync(ct);
                foreach(var rid in returns){try{var info=await DocumentInfo(actor,"SRT",rid,ct);if(visited.Add(("SRT",rid)))nodes.Add(info);}catch(AdministrationException ex)when(ex.Status==403){}}
                if(current.Kind=="SI")
                {
                    var payments=await (from payment in db.JournalEntries where payment.UserConfigId==company&&payment.SalesInvoicePaymentId.HasValue join target in db.JournalEntries on payment.PaymentToJournalEntryId equals (int?)target.Id where target.SalesInvoiceId==current.Id select payment.SalesInvoicePaymentId.Value).Distinct().ToListAsync(ct);
                    foreach(var pid in payments){try{var info=await DocumentInfo(actor,"PR",pid,ct);if(visited.Add(("PR",pid)))nodes.Add(info);}catch(AdministrationException ex)when(ex.Status==403){}}
                }
            }
        }
        return new{Documents=nodes,Relationships=edges};
    }
    async Task<object> DocumentInfo(User actor,string kind,int id,CancellationToken ct)
    {
        Demand(actor,kind,"canView","canCreate","canEdit");var company=Company(actor);
        if(kind is "QT" or "SO" or "DR"){var r=await Find(actor,kind,id,ct);return new{Kind=kind,r.Id,r.ReferenceNo,r.Status,r.Version};}
        if(kind=="SI"){var r=await db.SalesInvoices.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);if(r==null)return null;PostedJournalValidator.DemandScope(actor.UserRole,r.ResponsibilityCenterEntry);return new{Kind=kind,r.Id,ReferenceNo=r.InvoiceNo,r.Status,Version=await db.SalesWorkflowInvoices.Where(x=>x.UserConfigId==company&&x.Kind==kind&&x.InvoiceId==id).Select(x=>(long?)x.Version).SingleOrDefaultAsync(ct)};}
        if(kind=="SR"){var r=await db.SalesReceipts.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);if(r==null)return null;PostedJournalValidator.DemandScope(actor.UserRole,r.ResponsibilityCenterEntry);return new{Kind=kind,r.Id,ReferenceNo=r.ReceiptNo,r.Status,Version=await db.SalesWorkflowInvoices.Where(x=>x.UserConfigId==company&&x.Kind==kind&&x.InvoiceId==id).Select(x=>(long?)x.Version).SingleOrDefaultAsync(ct)};}
        if(kind=="SRT"){var r=await db.SalesReturns.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);if(r==null)return null;PostedJournalValidator.DemandScope(actor.UserRole,r.ResponsibilityCenterEntry);return new{Kind=kind,r.Id,r.ReferenceNo,r.Status,r.Version};}
        var p=await db.SalesInvoicePayments.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);if(p==null)return null;PostedJournalValidator.DemandScope(actor.UserRole,p.ResponsibilityCenterEntry);return new{Kind=kind,p.Id,p.ReferenceNo,p.Status};
    }
}
