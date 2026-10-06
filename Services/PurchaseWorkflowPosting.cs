using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using negosuite_api.Models;
using negosuite_api.Contracts.Transactions;
using static negosuite_api.Services.AdministrationSupport;
namespace negosuite_api.Services;
public sealed partial class PurchaseWorkflowService
{
 async Task<decimal> Stock(int company,int item,CancellationToken ct)=>await db.InventoryTransactions.AsNoTracking().Where(t=>t.UserConfigId==company&&t.ItemId==item&&t.Status==1).SumAsync(t=>t.QuantityIn-t.QuantityOut,ct);
 public sealed class StockStamp {public string Source {get;set;} public int DetailId {get;set;} public string ReferenceNo {get;set;} public DateTime ReferenceDate {get;set;} public decimal QuantityIn {get;set;} public decimal QuantityOut {get;set;} public decimal? Amount {get;set;} public int? InventoryLocationId {get;set;}}
 async Task<string> Fingerprint(int company,int item,CancellationToken ct)=>Hash(await db.Database.SqlQuery<StockStamp>($"SELECT Source,DetailId,ReferenceNo,ReferenceDate,QuantityIn,QuantityOut,Amount,InventoryLocationId FROM inventorytransaction WHERE UserConfigId={company} AND ItemId={item} AND Status=1 ORDER BY Source,DetailId,ReferenceNo").ToListAsync(ct));
 async Task RequireView(CancellationToken ct)=>Require(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction_before_purchaseworkflow'").SingleAsync(ct)==1,"Install purchase workflow migration 011 before posting.",409);
 public async Task<object> Post(User actor,string kind,int id,PurchaseAction input,CancellationToken ct)
 {
  Demand(actor,kind,"canCreate","canEdit");if(kind=="LC")Admin(actor);var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);var d=await Find(actor,kind,id,ct);if(d.Status==1)return await Get(actor,kind,id,ct);Require(d.Status==0&&d.Version==input.Version,"Document changed. Reload it.",409);
  var cfg=await Configuration(actor,ct);Require(kind!="PO"||cfg.ProcessingMode!="DIRECT","New purchase orders are disabled in Direct Purchase mode.");
  var p=await Build(actor,kind,Payload(d),cfg,ct);if(kind!="PO")await RequireView(ct);
  if(kind is "GR" or "LC"){var itemIds=p.Lines.Select(l=>l.ItemId).ToArray();Require(!await db.InventoryTransactions.AnyAsync(t=>t.UserConfigId==company&&itemIds.Contains(t.ItemId)&&t.Status==1&&t.ReferenceDate>d.ReferenceDate,ct),"Choose a date on or after existing inventory movements for these items.",409);}if(kind=="GR")await Receive(actor,d,p,ct);else if(kind=="PB")await Bill(actor,d,p,ct);else if(kind=="LC")await Adjust(actor,d,p,ct);
  d.PayloadJson=JsonConvert.SerializeObject(p);d.Amount=p.Lines.Sum(l=>l.Amount);d.Status=1;d.PostedDate=DateTime.UtcNow;d.Version++;
  for(var i=0;i<p.Lines.Count;i++){var l=p.Lines[i];if(l.SourceDocumentId.HasValue)db.PurchaseWorkflowLinks.Add(new(){UserConfigId=company,SourceId=l.SourceDocumentId.Value,SourceLine=l.SourceLineId.Value,TargetId=d.Id,TargetLine=i+1,Quantity=kind=="LC"?0:l.Quantity});}
  await db.SaveChangesAsync(ct);if(kind is "GR" or "LC"){foreach(var group in p.Lines.GroupBy(l=>l.ItemId)){var fingerprint=await Fingerprint(company,group.Key,ct);foreach(var line in group)line.StockFingerprint=fingerprint;}d.PayloadJson=JsonConvert.SerializeObject(p);await db.SaveChangesAsync(ct);}await tx.CommitAsync(ct);return await Get(actor,kind,id,ct);
 }
 JournalEntry Entry(User actor,PurchaseWorkflowDocument d,int account,string nature,decimal amount,string source)=>new(){UserConfigId=d.UserConfigId,ReferenceNo=d.ReferenceNo,JournalDate=d.ReferenceDate,AccountId=account,Nature=nature,Amount=amount,Balance=amount,Source=source,Status=1,SupplierId=d.SupplierId,ResponsibilityCenterEntry=d.ResponsibilityCenterEntry,CreatedByUserId=actor.Id,PostedByUserId=actor.Id,CreatedDate=DateTime.UtcNow,PostedDate=DateTime.UtcNow};
 async Task Receive(User actor,PurchaseWorkflowDocument d,PurchaseWorkflowWrite p,CancellationToken ct)
 {
  Require(!await db.ReceivingReports.AnyAsync(x=>x.UserConfigId==d.UserConfigId&&x.ReferenceNo==d.ReferenceNo,ct),"Receipt reference already exists.",409);
  var source=await Find(actor,"PO",p.Lines[0].SourceDocumentId.Value,ct);var now=DateTime.UtcNow;
  var rr=new ReceivingReport{UserConfigId=d.UserConfigId,SupplierId=d.SupplierId,ReferenceNo=d.ReferenceNo,ReferenceDate=d.ReferenceDate,PurchaseOrderNo=source.ReferenceNo,DeliveryReceiptNo=p.DeliveryReceiptNo,CreditAccountId=p.GrniAccountId.Value,InventoryLocationId=p.InventoryLocationId,ResponsibilityCenterEntry=p.ResponsibilityCenterEntry,Notes=p.Notes,Status=1,Amount=p.Lines.Sum(l=>l.Amount),Balance=0,CreatedDate=now,CreatedByUserId=actor.Id,PostedDate=now,ReceivingReportDetails=new List<ReceivingReportDetail>(),JournalEntries=new List<JournalEntry>()};
  var quantities=new Dictionary<int,decimal>();var items=new Dictionary<int,Item>();
  foreach(var group in p.Lines.GroupBy(l=>l.ItemId))
  {
   var item=await db.Items.SingleAsync(x=>x.Id==group.Key&&x.UserConfigId==d.UserConfigId,ct);items.Add(item.Id,item);var onHand=await Stock(d.UserConfigId,item.Id,ct);Require(onHand>=0,"Resolve negative stock before receiving through Purchase Processing.",409);var qty=group.Sum(l=>l.Quantity);var amount=group.Sum(l=>l.Amount);var average=Four((onHand*(item.AverageCost??item.Cost??0)+amount)/(onHand+qty));
   foreach(var line in group){line.PreviousAverageCost=item.AverageCost;line.PreviousCost=item.Cost;line.PreviousPurchasedDate=item.LastPurchasedDate;} quantities.Add(item.Id,onHand);item.AverageCost=average;item.Cost=Four(group.Sum(l=>l.NetAmount)/qty);item.LastPurchasedDate=d.ReferenceDate;foreach(var l in group){l.PostOnHand=onHand+qty;l.PostAverageCost=average;}
  }
  foreach(var l in p.Lines)
  {
   // V1 RR landedCost and its allocation JSON remain PER UNIT.
   rr.ReceivingReportDetails.Add(new(){ItemId=l.ItemId,Quantity=l.Quantity,Rate=Four(l.NetAmount/l.Quantity),Amount=l.Amount,LandedCost=l.LandedCosts.Sum(c=>c.Amount),LandedCostJson=JsonConvert.SerializeObject(l.LandedCosts.Select(c=>new{accountId=c.AccountId,amount=c.Amount})),InventoryLocationId=p.InventoryLocationId,Status=1,CreatedDate=now,CreatedByUserId=actor.Id,PostedDate=now});
   rr.JournalEntries.Add(Entry(actor,d,l.InventoryAccountId,"D",l.Amount,"RR"));rr.JournalEntries.Add(Entry(actor,d,p.GrniAccountId.Value,"C",l.NetAmount,"RR"));
   // Allocate rounding residual to the final cost entry so posted journals balance exactly.
   decimal allocated=0;for(var i=0;i<l.LandedCosts.Count;i++){var c=l.LandedCosts[i];var amount=i==l.LandedCosts.Count-1?l.Amount-l.NetAmount-allocated:Money(l.Quantity*c.Amount);allocated+=amount;if(amount!=0)rr.JournalEntries.Add(Entry(actor,d,c.AccountId,"C",amount,"RR"));}
  }
  await new PostedJournalValidator(db).ValidateAsync(d.UserConfigId,rr,rr.JournalEntries.ToArray(),actor.UserRole,ct);db.ReceivingReports.Add(rr);await db.SaveChangesAsync(ct);d.LegacyId=rr.Id;var details=rr.ReceivingReportDetails.ToArray();for(var i=0;i<p.Lines.Count;i++)p.Lines[i].LegacyLineId=details[i].Id;
 }
 async Task Bill(User actor,PurchaseWorkflowDocument d,PurchaseWorkflowWrite p,CancellationToken ct)
 {
  Require(!await db.Bills.AnyAsync(x=>x.UserConfigId==d.UserConfigId&&x.BillNo==d.ReferenceNo,ct),"Bill reference already exists.",409);var config=await db.Configs.AsNoTracking().SingleAsync(c=>c.Id==d.UserConfigId,ct);Require(config.APTradeAccountId.HasValue,"Configure Accounts Payable Trade.");var now=DateTime.UtcNow;
  var bill=new Bill{UserConfigId=d.UserConfigId,SupplierId=d.SupplierId,BillNo=d.ReferenceNo,BillDate=d.ReferenceDate,DueDate=p.DueDate.Value,PaymentTermId=p.PaymentTermId,InventoryLocationId=p.InventoryLocationId,Notes=p.Notes,ResponsibilityCenterEntry=p.ResponsibilityCenterEntry,IsTaxExclusive=true,HasItemLevelDiscount=true,Amount=p.Lines.Sum(l=>l.Amount),Balance=p.Lines.Sum(l=>l.Amount),Status=1,CreatedDate=now,CreatedByUserId=actor.Id,PostedDate=now,Taxes=JsonConvert.SerializeObject(p.Lines.Where(l=>l.TaxRateId.HasValue).GroupBy(l=>l.TaxRateId).Select(g=>new{taxRate=new{id=g.Key,rate=g.First().TaxPercent,taxAccountId=g.First().TaxAccountId},amount=g.Sum(l=>l.TaxAmount)}))};
  foreach(var l in p.Lines)
  {
   // Receipt owns stock and landed cost. This path deliberately does not call BillInventorySnapshot.
   bill.BillDetails.Add(new(){ItemId=l.ItemId,Quantity=l.Quantity,Rate=Four(l.NetAmount/l.Quantity),Amount=l.NetAmount,DiscountAmount=0,TaxRateId=l.TaxRateId,TaxAmount=l.TaxAmount,IsInventoryTransaction=false,LandedCost=0,LandedCostJson="[]",InventoryLocationId=p.InventoryLocationId,Status=1,CreatedDate=now,CreatedByUserId=actor.Id,PostedDate=now});
   bill.JournalEntries.Add(Entry(actor,d,p.GrniAccountId.Value,"D",l.NetAmount,"PU"));if(l.TaxAmount!=0){Require(l.TaxAccountId.HasValue,"Receipt tax snapshot is missing an account.");bill.JournalEntries.Add(Entry(actor,d,l.TaxAccountId.Value,"D",l.TaxAmount,"PU"));}
  }
  var payable=Entry(actor,d,config.APTradeAccountId.Value,"C",bill.Amount.Value,"PU");payable.DueDate=bill.DueDate;bill.JournalEntries.Add(payable);await new PostedJournalValidator(db).ValidateAsync(d.UserConfigId,bill,bill.JournalEntries.ToArray(),actor.UserRole,ct);db.Bills.Add(bill);await db.SaveChangesAsync(ct);d.LegacyId=bill.Id;
 }
 async Task Adjust(User actor,PurchaseWorkflowDocument d,PurchaseWorkflowWrite p,CancellationToken ct)
 {
  // Allocation is explicit and administrator-reviewed. We cannot reconstruct historic
  // weighted-average consumption reliably from today's quantity alone.
  Require(!await db.GeneralJournals.AnyAsync(x=>x.UserConfigId==d.UserConfigId&&x.ReferenceNo==d.ReferenceNo,ct),"Adjustment reference already exists.",409);
  var journal=new GeneralJournal{UserConfigId=d.UserConfigId,ReferenceNo=d.ReferenceNo,ReferenceDate=d.ReferenceDate,ResponsibilityCenterEntry=p.ResponsibilityCenterEntry,Notes=p.Notes,Status=1,CreatedByUserId=actor.Id,CreatedDate=DateTime.UtcNow,PostedDate=DateTime.UtcNow};
  foreach(var group in p.Lines.GroupBy(l=>l.ItemId))
  {
   var item=await db.Items.SingleAsync(x=>x.Id==group.Key&&x.UserConfigId==d.UserConfigId,ct);var qty=await Stock(d.UserConfigId,item.Id,ct);var delta=group.Sum(l=>l.InventoryAdjustment);Require(delta==0||qty>0,"There is no positive stock on hand to receive an inventory cost adjustment.",409);var value=qty*(item.AverageCost??item.Cost??0)+delta;Require(value>=0,"Adjustment would make inventory value negative.");foreach(var line in group){line.PreviousAverageCost=item.AverageCost;line.PreviousCost=item.Cost;line.PreviousPurchasedDate=item.LastPurchasedDate;} if(delta!=0)item.AverageCost=Four(value/qty);
   foreach(var l in group){l.PostOnHand=qty;l.PostAverageCost=item.AverageCost??0;}
  }
  foreach(var l in p.Lines)
  {
   void Add(int account,decimal amount,bool debit){if(amount!=0)journal.JournalEntries.Add(Entry(actor,d,account,(amount>0)==debit?"D":"C",Math.Abs(amount),"GJ"));}
   Add(l.InventoryAccountId,l.InventoryAdjustment,true);if(l.CogsAdjustment!=0){Require(l.CostAccountId.HasValue,"Configure the item's COGS account.");Add(l.CostAccountId.Value,l.CogsAdjustment,true);}Add(l.AdjustmentAccountId.Value,l.Amount,false);
  }
  await new PostedJournalValidator(db).ValidateAsync(d.UserConfigId,journal,journal.JournalEntries.ToArray(),actor.UserRole,ct);db.GeneralJournals.Add(journal);await db.SaveChangesAsync(ct);d.LegacyId=journal.Id;
 }
 public async Task<object> Cancel(User actor,string kind,int id,PurchaseAction input,CancellationToken ct)
 {
  Demand(actor,kind,"canDelete");if(kind=="LC")Admin(actor);var company=Company(actor);await using var tx=await LockCompanyAsync(db,company,ct);var d=await Find(actor,kind,id,ct);if(d.Status==-1)return await Get(actor,kind,id,ct);Require(d.Version==input.Version&&!string.IsNullOrWhiteSpace(input.Reason)&&input.Reason.Length<=250,"Reload the document and enter a cancellation reason.",409);
  Require(!await (from l in db.PurchaseWorkflowLinks join target in db.PurchaseWorkflowDocuments on l.TargetId equals target.Id where l.SourceId==id&&target.Status==1 select l).AnyAsync(ct),"Cancel dependent bills or cost adjustments first.",409);
  var p=Payload(d);if(d.Status==1&&kind is "GR" or "LC")
  {
   foreach(var group in p.Lines.GroupBy(l=>l.ItemId))
   {
    var item=await db.Items.SingleAsync(x=>x.Id==group.Key&&x.UserConfigId==company,ct);var onHand=await Stock(company,item.Id,ct);var sample=group.First();Require(await Fingerprint(company,item.Id,ct)==sample.StockFingerprint,"Reverse later inventory movements before cancellation.",409);Require(onHand==sample.PostOnHand&&Four(item.AverageCost??0)==sample.PostAverageCost,"Inventory changed after posting. Reverse later movements/adjustments before cancelling this document.",409);
    var quantity=kind=="GR"?group.Sum(l=>l.Quantity):0;var value=kind=="GR"?group.Sum(l=>l.Amount):group.Sum(l=>l.InventoryAdjustment);var remaining=onHand-quantity;Require(remaining>=0,"Cancellation would make stock negative.",409);var cost=onHand*(item.AverageCost??0)-value;Require(cost>=-0.01m,"Cancellation would make inventory value negative.");item.AverageCost=sample.PreviousAverageCost;if(kind=="GR"){item.Cost=sample.PreviousCost;item.LastPurchasedDate=sample.PreviousPurchasedDate;}
   }
  }
  if(d.LegacyId.HasValue)
  {
   var lid=d.LegacyId.Value;var journals=await db.JournalEntries.Where(j=>j.UserConfigId==company&&(kind=="GR"?EF.Property<int?>(j,"ReceivingReportId")==lid:kind=="PB"?j.BillId==lid:j.GeneralJournalId==lid)).ToListAsync(ct);var ids=journals.Select(j=>j.Id).ToArray();Require(!await db.JournalEntries.AnyAsync(j=>j.Status==1&&j.PaymentToJournalEntryId.HasValue&&ids.Contains(j.PaymentToJournalEntryId.Value),ct),"Reverse payments or credit applications before cancellation.",409);foreach(var j in journals){j.Status=-1;j.LastUpdatedByUserId=actor.Id;j.LastUpdatedDate=DateTime.UtcNow;}
   if(kind=="GR"){var rr=await db.ReceivingReports.Include(r=>r.ReceivingReportDetails).SingleAsync(r=>r.Id==lid&&r.UserConfigId==company,ct);rr.Status=-1;rr.LastUpdatedDate=DateTime.UtcNow;rr.LastUpdatedByUserId=actor.Id;foreach(var l in rr.ReceivingReportDetails)l.Status=-1;}
   if(kind=="PB"){var b=await db.Bills.Include(b=>b.BillDetails).SingleAsync(b=>b.Id==lid&&b.UserConfigId==company,ct);b.Status=-1;b.Balance=0;b.LastUpdatedDate=DateTime.UtcNow;b.LastUpdatedByUserId=actor.Id;foreach(var l in b.BillDetails)l.Status=-1;}
   if(kind=="LC"){var j=await db.GeneralJournals.SingleAsync(j=>j.Id==lid&&j.UserConfigId==company,ct);j.Status=-1;j.LastUpdatedDate=DateTime.UtcNow;j.LastUpdatedByUserId=actor.Id;}
  }
  d.Status=-1;d.Version++;d.VoidReason=input.Reason.Trim();d.VoidedByUserId=actor.Id;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return await Get(actor,kind,id,ct);
 }
}
