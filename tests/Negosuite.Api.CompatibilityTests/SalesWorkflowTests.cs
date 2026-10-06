using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Models;
using Xunit;
namespace Negosuite.Api.CompatibilityTests;

public class SalesWorkflowTests
{
    [Fact] public void Schema_has_company_scoped_idempotency_and_source_indexes()
    {
        using var db=new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;port=33316;database=unused;user=root").Options);
        Assert.Contains(db.Model.FindEntityType(typeof(SalesWorkflowDocument))!.GetIndexes(),i=>i.IsUnique&&i.Properties.Select(p=>p.Name).SequenceEqual(new[]{"UserConfigId","RequestKey"}));
        Assert.Contains(db.Model.FindEntityType(typeof(DocumentRelationship))!.GetIndexes(),i=>i.IsUnique&&i.Properties.Count==5);
        // Opt-in development artifact; never connects to a database.
        var output=Environment.GetEnvironmentVariable("NEGOSUITE_WORKFLOW_SCHEMA_OUTPUT");
        if(output!=null)
        {
            var names=new[]{"salesconfiguration","salesworkflowdocument","salesworkflowline","documentrelationship","documentlinerelationship","salespostingrecord","salesworkflowinvoice"};
            var sql=db.Database.GenerateCreateScript();var statements=Regex.Split(sql,@";\s*(?:\r?\n|$)");
            File.WriteAllText(output,string.Join(";\n\n",statements.Where(s=>names.Any(n=>Regex.IsMatch(s,@"(?:CREATE TABLE| ON)\s+`?"+n+@"`?\s*[\(\r\n]"))))+";\n");
        }
    }
    [PagePreferenceTests.MySqlTheory,InlineData("DELIVERY",false),InlineData("DELIVERY",true),InlineData("INVOICE",false),InlineData("INVOICE",true)]
    public async Task Partial_delivery_invoice_return_and_cancellation_preserve_stock_and_cost(string point,bool cash)
    {
        await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f,point);
        var order=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Order(f,3));var orderId=order.GetProperty("id").GetInt32();var ol=order.GetProperty("lines")[0].GetProperty("id").GetInt32();
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/SO/{orderId}/post",new{version=1});
        var stock=await f.Send(HttpMethod.Get,$"/api/sales-workflow/stock?itemId={f.Base.Item.Id}&inventoryLocationId={f.Location}");Assert.Equal(3,stock.GetProperty("committed").GetDecimal());
        var delivery=await f.Send(HttpMethod.Post,"/api/sales-workflow/DR",Delivery(f,orderId,ol,3));var did=delivery.GetProperty("id").GetInt32();var dl=delivery.GetProperty("lines")[0].GetProperty("id").GetInt32();
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/DR/{did}/post",new{version=1});await f.Send(HttpMethod.Post,$"/api/sales-workflow/DR/{did}/post",new{version=1});
        Assert.Equal(-3,await Net(f));Assert.Equal(point=="DELIVERY"?240:0,await f.Db.JournalEntries.Where(j=>j.Source=="DR"&&j.Status==1&&j.Nature=="D").SumAsync(j=>j.Amount));
        // Later average-cost/config changes must not reprice this delivery.
        f.Base.Item.AverageCost=999;await f.Db.SaveChangesAsync();
        var request=new{requestKey=Guid.NewGuid().ToString(),kind=cash?"SR":"SI",referenceDate=DateTime.Today,dueDate=DateTime.Today,paymentModeId=f.Base.Mode.Id,depositToAccountId=f.Cash.Id,lines=new[]{new{deliveryId=did,lineId=dl,quantity=1}}};
        var invoice=await f.Send(HttpMethod.Post,"/api/sales-workflow/invoices",request);var iid=invoice.GetProperty("id").GetInt32();var kind=cash?"SR":"SI";
        var retry=await f.Send(HttpMethod.Post,"/api/sales-workflow/invoices",request);Assert.Equal(iid,retry.GetProperty("id").GetInt32());Assert.Equal(-3,await Net(f));
        var second=await f.Send(HttpMethod.Post,"/api/sales-workflow/invoices",new{requestKey=Guid.NewGuid().ToString(),kind,referenceDate=DateTime.Today,dueDate=DateTime.Today,paymentModeId=f.Base.Mode.Id,depositToAccountId=f.Cash.Id,lines=new[]{new{deliveryId=did,lineId=dl,quantity=2}}});
        await f.Reject(HttpMethod.Post,"/api/sales-workflow/invoices",new{requestKey=Guid.NewGuid().ToString(),kind,referenceDate=DateTime.Today,dueDate=DateTime.Today,paymentModeId=f.Base.Mode.Id,depositToAccountId=f.Cash.Id,lines=new[]{new{deliveryId=did,lineId=dl,quantity=1}}},HttpStatusCode.Conflict);Assert.Equal(-3,await Net(f));
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/{kind}/{second.GetProperty("id")}/cancel",new{version=1,reason="Undo second invoice"});
        var invoiceCost=await (from p in f.Db.SalesPostingRecords where p.DocumentType==kind&&p.DocumentId==iid&&p.Effect=="cost" join j in f.Db.JournalEntries on p.JournalEntryId equals j.Id select j.Amount).SumAsync();Assert.Equal(point=="INVOICE"?80:0,invoiceCost);
        await f.Reject(HttpMethod.Post,$"/api/sales-workflow/DR/{did}/cancel",new{version=2,reason="Has invoice"},HttpStatusCode.Conflict);
        var source=await f.Send(HttpMethod.Get,$"/api/sales-returns/sources/{kind}/{iid}");
        var sourceLine=source.GetProperty("lines")[0].GetProperty("id").GetInt32();
        var returned=await f.Send(HttpMethod.Post,"/api/sales-returns",new{requestKey=Guid.NewGuid().ToString(),source=kind,sourceId=iid,referenceDate=DateTime.Today,inventoryLocationId=f.Location,reason="Returned",lines=new[]{new{sourceDetailId=sourceLine,quantity=1}},applications=Array.Empty<object>()});
        var rid=returned.GetProperty("id").GetInt32();await f.Send(HttpMethod.Post,$"/api/sales-returns/{rid}/post",new{version=1});Assert.Equal(-2,await Net(f));
        await f.Reject(HttpMethod.Post,$"/api/sales-workflow/{kind}/{iid}/cancel",new{version=1,reason="Return exists"},HttpStatusCode.Conflict);
        await f.Send(HttpMethod.Post,$"/api/sales-returns/{rid}/void",new{version=2,reason="Undo return"});await f.Send(HttpMethod.Post,$"/api/sales-workflow/{kind}/{iid}/cancel",new{version=1,reason="Undo invoice"});Assert.Equal(-3,await Net(f));
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/DR/{did}/cancel",new{version=2,reason="Undo delivery"});Assert.Equal(0,await Net(f));
        stock=await f.Send(HttpMethod.Get,$"/api/sales-workflow/stock?itemId={f.Base.Item.Id}&inventoryLocationId={f.Location}");Assert.Equal(3,stock.GetProperty("committed").GetDecimal());
    }
    [MySqlFact] public async Task Concurrent_deliveries_revalidate_source_and_stock_failure_rolls_back()
    {
        await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f,"INVOICE");var order=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Order(f,3));var oid=order.GetProperty("id").GetInt32();var line=order.GetProperty("lines")[0].GetProperty("id").GetInt32();await f.Send(HttpMethod.Post,$"/api/sales-workflow/SO/{oid}/post",new{version=1});
        var a=await f.Send(HttpMethod.Post,"/api/sales-workflow/DR",Delivery(f,oid,line,2));var b=await f.Send(HttpMethod.Post,"/api/sales-workflow/DR",Delivery(f,oid,line,2));
        var responses=await Task.WhenAll(f.Base.Host.Client.PostAsJsonAsync($"/api/sales-workflow/DR/{a.GetProperty("id")}/post",new{version=1}),f.Base.Host.Client.PostAsJsonAsync($"/api/sales-workflow/DR/{b.GetProperty("id")}/post",new{version=1}));Assert.Single(responses,r=>r.IsSuccessStatusCode);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);Assert.Equal(-2,await Net(f));
        var cfg=await f.Send(HttpMethod.Get,"/api/sales-workflow/configuration");await f.Send(HttpMethod.Put,"/api/sales-workflow/configuration",new{version=cfg.GetProperty("version").GetInt64(),salesProcessingMode="BOTH",enableSalesQuotation=false,cogsRecognitionPoint="INVOICE",enableInventoryCommitment=true,allowNegativeInventory=false});
        var next=await f.Send(HttpMethod.Post,"/api/sales-workflow/DR",Delivery(f,oid,line,1));await f.Reject(HttpMethod.Post,$"/api/sales-workflow/DR/{next.GetProperty("id")}/post",new{version=1},HttpStatusCode.Conflict);Assert.Equal(-2,await Net(f));
        await f.Reject(HttpMethod.Post,"/api/sales-workflow/QT",Order(f,1),HttpStatusCode.BadRequest);await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Order(f,1));
    }
    static object Order(SalesReturnTests.Fixture f,decimal qty)=>new{requestKey=Guid.NewGuid().ToString(),referenceDate=DateTime.Today,customerId=f.Db.SalesInvoices.Where(i=>i.Id==f.SourceId).Select(i=>i.CustomerId).Single(),inventoryLocationId=f.Location,isTaxExclusive=true,lines=new[]{new{itemId=f.Base.Item.Id,quantity=qty,rate=100,inventoryLocationId=f.Location}}};
    [MySqlFact] public async Task Migrations_install_missing_tables_and_preserve_legacy_invoice_rows()
    {
        await using var f=await SalesReturnTests.Fixture.Start();var connection=new MySqlConnectionStringBuilder(f.Db.Database.GetConnectionString());Assert.Equal(33316u,connection.Port);Assert.StartsWith("negosuite_billpayments_",connection.Database);
        var before=await f.Db.SalesInvoices.CountAsync();
        foreach(var name in new[]{"salespostingrecord","documentlinerelationship","documentrelationship","salesworkflowline","salesworkflowdocument","salesworkflowinvoice","salesconfiguration"})await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE `"+name+"`");
        await Prepare(f,"INVOICE");Assert.Equal(before,await f.Db.SalesInvoices.CountAsync());
        var order=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Order(f,1));Assert.Equal(0,order.GetProperty("status").GetInt32());
    }
    [MySqlFact] public async Task Quotation_is_optional_and_sources_permissions_and_retries_are_enforced()
    {
        await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f,"INVOICE");var request=Order(f,3);var order=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",request);var retry=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",request);Assert.Equal(order.GetProperty("id").GetInt32(),retry.GetProperty("id").GetInt32());
        var quotation=await f.Send(HttpMethod.Post,"/api/sales-workflow/QT",Order(f,3));var qid=quotation.GetProperty("id").GetInt32();await f.Send(HttpMethod.Post,$"/api/sales-workflow/QT/{qid}/post",new{version=1});
        Assert.Equal(0,await f.Db.JournalEntries.CountAsync(j=>j.Source=="QT"||j.Source=="SO"));
        await f.Reject(HttpMethod.Post,"/api/sales-workflow/DR",Order(f,1),HttpStatusCode.BadRequest);
        var customer=order.GetProperty("customerId").GetInt32();
        var fromQuotation=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",new{requestKey=Guid.NewGuid().ToString(),referenceDate=DateTime.Today,customerId=customer,inventoryLocationId=f.Location,isTaxExclusive=true,lines=new[]{new{itemId=f.Base.Item.Id,quantity=2,rate=100,sourceDocumentId=qid,sourceLineId=quotation.GetProperty("lines")[0].GetProperty("id").GetInt32()}}});
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/SO/{fromQuotation.GetProperty("id")}/post",new{version=1});
        await f.Reject(HttpMethod.Post,$"/api/sales-workflow/QT/{qid}/cancel",new{version=2,reason="Already ordered"},HttpStatusCode.Conflict);
        var history=await f.Send(HttpMethod.Get,$"/api/sales-workflow/SO/{fromQuotation.GetProperty("id")}/related");Assert.Equal(2,history.GetProperty("documents").GetArrayLength());
        f.Actor.UserRole.IsAdmin=false;f.Actor.UserRole.Permission="[]";await f.Db.SaveChangesAsync();
        await f.Reject(HttpMethod.Get,$"/api/sales-workflow/SO/{order.GetProperty("id")}",null,HttpStatusCode.Forbidden);
        f.Actor.UserRole.IsAdmin=true;await f.Db.SaveChangesAsync();
        await f.Reject(HttpMethod.Get,"/api/sales-workflow/SO/2147483647",null,HttpStatusCode.NotFound);
    }
    static object Delivery(SalesReturnTests.Fixture f,int order,int line,decimal qty)=>new{requestKey=Guid.NewGuid().ToString(),referenceDate=DateTime.Today,customerId=f.Db.SalesInvoices.Where(i=>i.Id==f.SourceId).Select(i=>i.CustomerId).Single(),inventoryLocationId=f.Location,isTaxExclusive=true,lines=new[]{new{itemId=f.Base.Item.Id,quantity=qty,sourceDocumentId=order,sourceLineId=line,inventoryLocationId=f.Location}}};
    [PagePreferenceTests.MySqlTheory,InlineData(false),InlineData(true)]
    public async Task Direct_invoice_cancellation_retains_document_lines_and_journals(bool cash)
    {
        await using var f=await SalesReturnTests.Fixture.Start(cash);await Prepare(f,"INVOICE");var kind=cash?"SR":"SI";
        await f.Reject(HttpMethod.Delete,$"/api/{(cash?"sales-receipts":"sales-invoices")}/{f.SourceId}",null,HttpStatusCode.Conflict);
        var response=await f.Send(HttpMethod.Post,$"/api/sales-workflow/{kind}/{f.SourceId}/cancel",new{reason="Cancelled direct sale",version=0});Assert.Equal(-1,response.GetProperty("status").GetInt32());
        var entries=await f.Db.JournalEntries.AsNoTracking().Where(j=>cash?j.SalesReceiptId==f.SourceId:j.SalesInvoiceId==f.SourceId).ToListAsync();Assert.NotEmpty(entries);Assert.All(entries,j=>Assert.Equal(-1,j.Status));
        Assert.True(cash?await f.Db.SalesReceipts.AnyAsync(i=>i.Id==f.SourceId&&i.Status==-1):await f.Db.SalesInvoices.AnyAsync(i=>i.Id==f.SourceId&&i.Status==-1));
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/{kind}/{f.SourceId}/cancel",new{reason="Repeat cancellation",version=0});
    }
    static Task<decimal> Net(SalesReturnTests.Fixture f)=>f.Db.Database.SqlQueryRaw<decimal>("SELECT COALESCE(SUM(QuantityIn-QuantityOut),0) AS Value FROM inventorytransaction").SingleAsync();

    [PagePreferenceTests.MySqlTheory,InlineData(true,"percent",true),InlineData(true,"amount",true),InlineData(false,"percent",true),InlineData(false,"amount",true),InlineData(true,"percent",false),InlineData(true,"amount",false),InlineData(false,"percent",false),InlineData(false,"amount",false)]
    public async Task Discount_modes_roundtrip_and_partial_delivery_preserve_allocations(bool perItem,string mode,bool exclusive)
    {
        await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f,"INVOICE");
        var company=f.Actor.ConfigId;var customer=await f.Db.SalesInvoices.Where(x=>x.Id==f.SourceId).Select(x=>x.CustomerId).SingleAsync();var tax=await f.Db.TaxRates.FirstAsync(x=>x.UserConfigId==company&&x.Rate==12);
        object Request(decimal discount)=>new{requestKey=Guid.NewGuid().ToString(),referenceDate=DateTime.Today,customerId=customer,inventoryLocationId=f.Location,isTaxExclusive=exclusive,hasItemLevelDiscount=perItem,discountMode=mode,discountValue=perItem?0:discount,lines=new[]{new{itemId=f.Base.Item.Id,quantity=3,rate=exclusive?100:112,inventoryLocationId=f.Location,taxRateId=tax.Id,discountPercent=perItem&&mode=="percent"?(decimal?)discount:null,discountAmount=perItem&&mode=="amount"?discount:0}}};
        var result=await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Request(mode=="percent"?10:30));var id=result.GetProperty("id").GetInt32();var line=result.GetProperty("lines")[0];
        Assert.Equal(30,result.GetProperty("discountAmount").GetDecimal());Assert.Equal(302.40m,result.GetProperty("amount").GetDecimal());Assert.Equal(perItem,result.GetProperty("hasItemLevelDiscount").GetBoolean());Assert.Equal(mode,result.GetProperty("discountMode").GetString());
        var reopened=await f.Send(HttpMethod.Get,$"/api/sales-workflow/SO/{id}");Assert.Equal(mode=="amount",reopened.GetProperty("lines")[0].GetProperty("discountPercent").ValueKind==JsonValueKind.Null);
        await f.Reject(HttpMethod.Post,"/api/sales-workflow/SO",Request(mode=="percent"?101:301),HttpStatusCode.BadRequest);
        await f.Send(HttpMethod.Post,$"/api/sales-workflow/SO/{id}/post",new{version=1});
        var delivery=await f.Send(HttpMethod.Post,"/api/sales-workflow/DR",new{requestKey=Guid.NewGuid().ToString(),referenceDate=DateTime.Today,customerId=customer,inventoryLocationId=f.Location,isTaxExclusive=exclusive,lines=new[]{new{itemId=f.Base.Item.Id,quantity=1,sourceDocumentId=id,sourceLineId=line.GetProperty("id").GetInt32(),inventoryLocationId=f.Location}}});
        Assert.Equal(10,delivery.GetProperty("discountAmount").GetDecimal());Assert.Equal(100.80m,delivery.GetProperty("amount").GetDecimal());
        var did=delivery.GetProperty("id").GetInt32();await f.Send(HttpMethod.Post,$"/api/sales-workflow/DR/{did}/post",new{version=1});
        var invoice=await f.Send(HttpMethod.Post,"/api/sales-workflow/invoices",new{requestKey=Guid.NewGuid().ToString(),kind="SI",referenceDate=DateTime.Today,dueDate=DateTime.Today,lines=new[]{new{deliveryId=did,lineId=delivery.GetProperty("lines")[0].GetProperty("id").GetInt32(),quantity=1}}});
        var iid=invoice.GetProperty("id").GetInt32();Assert.Equal(100.80m,await f.Db.SalesInvoices.Where(x=>x.Id==iid).Select(x=>x.Amount).SingleAsync());
    }

    [MySqlFact] public async Task Workflow_list_sort_export_and_column_preferences_use_existing_scope()
    {
        await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f,"INVOICE");await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Order(f,3));await f.Send(HttpMethod.Post,"/api/sales-workflow/SO",Order(f,1));
        var all=await f.Send(HttpMethod.Get,"/api/sales-workflow/SO?status=0&sortBy=amount&sortDirection=asc");Assert.Equal(JsonValueKind.Array,all.ValueKind);Assert.Equal(100,all[0].GetProperty("amount").GetDecimal());Assert.Equal(300,all[1].GetProperty("amount").GetDecimal());
        foreach(var key in new[]{"sales-quotations","sales-orders","deliveries"}){var saved=await f.Send(HttpMethod.Put,$"/api/me/page-preferences/{key}",new{version=0,columns=new{customerName=false,referenceNo=false}});Assert.False(saved.GetProperty("columns").GetProperty("customerName").GetBoolean());Assert.False(saved.GetProperty("columns").TryGetProperty("referenceNo",out _));var loaded=await f.Send(HttpMethod.Get,$"/api/me/page-preferences/{key}");Assert.Equal(1,loaded.GetProperty("version").GetInt32());}
        f.Actor.UserRole.IsAdmin=false;f.Actor.UserRole.Permission="[]";await f.Db.SaveChangesAsync();await f.Reject(HttpMethod.Get,"/api/me/page-preferences/sales-orders",null,HttpStatusCode.Forbidden);
    }
    internal static async Task Prepare(SalesReturnTests.Fixture f,string point)
    {
        f.Base.Item.ToSell=true;f.Base.Item.TrackInventory=true;f.Base.Item.AverageCost=80;await f.Db.SaveChangesAsync();
        var root=Directory.GetCurrentDirectory();while(!File.Exists(Path.Combine(root,"negosuite-api.csproj")))root=Directory.GetParent(root)!.FullName;
        await using var connection=new MySqlConnection(f.Db.Database.GetConnectionString());await connection.OpenAsync();
        foreach(var file in new[]{"006_sales_workflow.sql","007_sales_workflow_views.sql","008_sales_workflow_guards.sql","009_sales_workflow_discounts.sql"})new MySqlScript(connection,await File.ReadAllTextAsync(Path.Combine(root,"Db","migrations",file))).Execute();
        // Installation is repeatable with an existing EF-created schema and existing rows.
        foreach(var file in new[]{"006_sales_workflow.sql","007_sales_workflow_views.sql","008_sales_workflow_guards.sql","009_sales_workflow_discounts.sql"})new MySqlScript(connection,await File.ReadAllTextAsync(Path.Combine(root,"Db","migrations",file))).Execute();
        var cfg=await f.Send(HttpMethod.Get,"/api/sales-workflow/configuration");await f.Send(HttpMethod.Put,"/api/sales-workflow/configuration",new{version=cfg.GetProperty("version").GetInt64(),salesProcessingMode="BOTH",enableSalesQuotation=true,cogsRecognitionPoint=point,enableInventoryCommitment=true,allowNegativeInventory=true});
    }
}
