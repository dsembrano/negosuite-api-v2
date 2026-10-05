using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using negosuite_api.Contracts.Transactions;
using negosuite_api.Models;
using static negosuite_api.Services.AdministrationSupport;

namespace negosuite_api.Services;
public sealed class SalesReturnSourceService
{
    private readonly negosuiteContext db;
    public SalesReturnSourceService(negosuiteContext db)=>this.db=db;
    public async Task LockAsync(int company,string kind,int id,CancellationToken ct)
    {
        Require(db.Database.CurrentTransaction!=null,"Source locking requires a transaction.");
        if(kind=="SI") {
            await db.SalesInvoices.FromSqlInterpolated($"SELECT * FROM salesinvoice WHERE Id={id} AND UserConfigId={company} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.SalesInvoiceDetails.FromSqlInterpolated($"SELECT * FROM salesinvoicedetail WHERE SalesInvoiceId={id} ORDER BY Id FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.JournalEntries.FromSqlInterpolated($"SELECT * FROM journalentry WHERE SalesInvoiceId={id} AND UserConfigId={company} ORDER BY Id FOR UPDATE").AsNoTracking().ToListAsync(ct);
        } else {
            await db.SalesReceipts.FromSqlInterpolated($"SELECT * FROM salesreceipt WHERE Id={id} AND UserConfigId={company} FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.SalesReceiptDetails.FromSqlInterpolated($"SELECT * FROM salesreceiptdetail WHERE SalesReceiptId={id} ORDER BY Id FOR UPDATE").AsNoTracking().ToListAsync(ct);
            await db.JournalEntries.FromSqlInterpolated($"SELECT * FROM journalentry WHERE SalesReceiptId={id} AND UserConfigId={company} ORDER BY Id FOR UPDATE").AsNoTracking().ToListAsync(ct);
        }
    }
    public async Task<ReturnSource> LoadAsync(int company,string kind,int id,CancellationToken ct)
    {
        Require(kind is "SI" or "SR","Select a Charge Invoice or Cash Invoice.");
        var config=await db.Configs.AsNoTracking().SingleAsync(c=>c.Id==company,ct);
        Require(config.ARTradeAccountId.HasValue,"Configure Accounts Receivable Trade before creating a return.");
        ReturnSource result; List<JournalEntry> journals;List<Item> items;bool exclusive;string taxes;int? settlement;short status;
        if(kind=="SI")
        {
            var s=await db.SalesInvoices.AsNoTracking().Include(x=>x.Customer).Include(x=>x.SalesInvoiceDetails).ThenInclude(x=>x.Item).Include(x=>x.JournalEntries).ThenInclude(x=>x.Account).SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);
            Require(s!=null,"Source invoice not found.",404);status=s.Status;exclusive=s.IsTaxExclusive==true;taxes=s.Taxes;settlement=config.ARTradeAccountId;
            result=new(){Source=kind,Id=id,ReferenceNo=s.InvoiceNo,ReferenceDate=s.InvoiceDate,CustomerId=s.CustomerId,CustomerName=s.Customer.Name,BillingAddress=s.BillingAddress,BillingContactName=s.BillingContactName,BillingContactEmail=s.BillingContactEmail,ResponsibilityCenterEntry=s.ResponsibilityCenterEntry,InventoryLocationId=s.InventoryLocationId,Amount=s.Amount??0,ReceivableAccountId=config.ARTradeAccountId.Value};
            result.Lines=s.SalesInvoiceDetails.Where(d=>d.Status==1).OrderBy(d=>d.Id).Select(d=>new ReturnSourceLine{Id=d.Id,ItemId=d.ItemId,Name=d.Item.Name,Unit=d.Item.Unit,Quantity=d.Quantity,Rate=d.Rate,GrossAmount=d.Amount,TaxExemptAmount=d.TaxExemptAmount??0,Cost=d.Cost,DiscountAmount=d.DiscountAmount??0,TaxAmount=d.TaxAmount??0,TaxRateId=d.TaxRateId,TrackInventory=d.IsInventoryTransaction==true}).ToList();
            items=s.SalesInvoiceDetails.Select(d=>d.Item).ToList();journals=s.JournalEntries.Where(j=>j.Status==1).OrderBy(j=>j.Id).ToList();
        }
        else
        {
            var s=await db.SalesReceipts.AsNoTracking().Include(x=>x.Customer).Include(x=>x.SalesReceiptDetails).ThenInclude(x=>x.Item).Include(x=>x.JournalEntries).ThenInclude(x=>x.Account).SingleOrDefaultAsync(x=>x.Id==id&&x.UserConfigId==company,ct);
            Require(s!=null,"Source invoice not found.",404);status=s.Status;exclusive=s.IsTaxExclusive==true;taxes=s.Taxes;settlement=s.DepositToAccountId;
            result=new(){Source=kind,Id=id,ReferenceNo=s.ReceiptNo,ReferenceDate=s.ReceiptDate,CustomerId=s.CustomerId,CustomerName=s.Customer.Name,BillingAddress=s.BillingAddress,BillingContactName=s.BillingContactName,BillingContactEmail=s.BillingContactEmail,ResponsibilityCenterEntry=s.ResponsibilityCenterEntry,InventoryLocationId=s.InventoryLocationId,Amount=s.Amount??0,ReceivableAccountId=config.ARTradeAccountId.Value};
            result.Lines=s.SalesReceiptDetails.Where(d=>d.Status==1).OrderBy(d=>d.Id).Select(d=>new ReturnSourceLine{Id=d.Id,ItemId=d.ItemId,Name=d.Item.Name,Unit=d.Item.Unit,Quantity=d.Quantity,Rate=d.Rate,GrossAmount=d.Amount,TaxExemptAmount=d.TaxExemptAmount??0,Cost=d.Cost,DiscountAmount=d.DiscountAmount??0,TaxAmount=d.TaxAmount??0,TaxRateId=d.TaxRateId,TrackInventory=d.IsInventoryTransaction==true}).ToList();
            items=s.SalesReceiptDetails.Select(d=>d.Item).ToList();journals=s.JournalEntries.Where(j=>j.Status==1).OrderBy(j=>j.Id).ToList();
        }
        Require(status==1&&result.Lines.Count>0,"Only posted invoices with item details can be returned.");
        Require(result.Lines.All(l=>l.Quantity>0&&l.Cost>=0&&l.Rate>=0),"The source contains unsupported negative or zero quantity/value lines.");
        Require(journals.All(j=>j.UserConfigId==company&&j.Amount>=0&&j.Account!=null)&&journals.Sum(j=>j.Nature=="D"?j.Amount:-j.Amount)==0,"The source invoice has incomplete or unbalanced journals.");
        var main=journals.Where(j=>j.AccountId==settlement&&j.Nature=="D"&&j.Amount==Math.Round(result.Amount,2)).ToList();
        Require(main.Count==1,"The original invoice settlement entry is ambiguous. Review its accounting before returning it.");
        if(kind=="SI")result.ReceivableJournalId=main[0].Id;
        var taxRows=string.IsNullOrWhiteSpace(taxes)?new JArray():JArray.Parse(taxes);
        var weights=new List<(int Line,int Account,string Nature,string Kind,decimal Value)>();
        void Add(ReturnSourceLine l,int? account,string nature,string component,decimal value){if(value==0)return;Require(account.HasValue&&value>0,"The historical invoice account mapping cannot be reconstructed.");weights.Add((l.Id,account.Value,nature,component,value));}
        foreach(var line in result.Lines)
        {
            var item=items.First(i=>i.Id==line.ItemId);var tax=taxRows.FirstOrDefault(t=>(int?)t["taxRate"]?["id"]==line.TaxRateId)?["taxRate"];
            Require(line.TaxAmount==0||tax!=null,"Historical tax information is missing from the invoice.");
            var taxRate=(decimal?)tax?["rate"]??0;line.TaxName=(string)tax?["name"];
            var gross=line.GrossAmount;
            var revenue=(exclusive?gross:Math.Round(gross/(1+taxRate/100),2,MidpointRounding.AwayFromZero))-line.DiscountAmount-line.TaxExemptAmount;
            if(config.DiscountAccountId.HasValue)revenue+=line.DiscountAmount;
            Add(line,item.SalesAccountId,"C","revenue",revenue);
            if(config.DiscountAccountId.HasValue)Add(line,config.DiscountAccountId,"D","discount",line.DiscountAmount);
            Add(line,(int?)tax?["taxAccountId"],"C","tax",line.TaxAmount);
            if(line.TrackInventory){Add(line,item.InventoryAccountId,"C","inventory",line.Cost*line.Quantity);Add(line,item.PurchaseAccountId,"D","cost",line.Cost*line.Quantity);}
        }
        var matched=new HashSet<int>{main[0].Id};
        foreach(var group in weights.GroupBy(w=>(w.Account,w.Nature)))
        {
            var candidates=journals.Where(j=>j.Id!=main[0].Id&&j.AccountId==group.Key.Account&&j.Nature==group.Key.Nature).ToList();
            Require(candidates.Count==1&&group.Select(w=>w.Kind).Distinct().Count()==1,"The original account allocations are ambiguous. Review the source invoice.");
            var j=candidates[0];matched.Add(j.Id);
            Require(Math.Abs(Math.Round(group.Sum(w=>w.Value),2)-j.Amount)<=0.01m,"The item/account configuration no longer matches the posted invoice. Its historical allocation must be reviewed before returning it.");
            var values=group.GroupBy(w=>w.Line).OrderBy(g=>g.Key).Select(g=>(Id:g.Key,Value:g.Sum(w=>w.Value))).ToArray();
            var shares=Allocate(j.Amount,values.Select(v=>v.Value).ToArray());
            for(var n=0;n<values.Length;n++)result.Lines.Single(l=>l.Id==values[n].Id).Components.Add(new(){SourceJournalId=j.Id,AccountId=j.AccountId,AccountName=j.Account.Name,Nature=j.Nature=="C"?"D":"C",Amount=shares[n],Kind=group.First().Kind,SupplierId=j.SupplierId,CustomerId=j.CustomerId,ResponsibilityCenterEntry=j.ResponsibilityCenterEntry});
        }
        Require(journals.All(j=>matched.Contains(j.Id)||j.Amount==0),"The source invoice contains unsupported manual journal adjustments.");
        Require(result.Lines.All(l=>l.Components.Sum(c=>c.Nature=="D"?c.Amount:-c.Amount)>=0),"A source line has a negative return value.");
        return result;
    }
    // Largest remainder: exact posted cents are distributed deterministically across source lines.
    public static decimal[] Allocate(decimal amount,decimal[] weights)
    {
        var total=weights.Sum();Require(amount>=0&&total>0,"Invalid historical allocation.");
        var raw=weights.Select(w=>amount*100*w/total).ToArray();var cents=raw.Select(decimal.Floor).ToArray();
        var remaining=(int)(Math.Round(amount*100)-cents.Sum());
        foreach(var i in Enumerable.Range(0,weights.Length).OrderByDescending(i=>raw[i]-cents[i]).ThenBy(i=>i).Take(remaining))cents[i]++;
        return cents.Select(c=>c/100).ToArray();
    }
    public static decimal Portion(decimal original,decimal already,decimal quantity,decimal sold)=>Math.Round(original*(already+quantity)/sold,2)-Math.Round(original*already/sold,2);
}
