using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Models;
using negosuite_api.Contracts.Transactions;
using Xunit;
namespace Negosuite.Api.CompatibilityTests;
public class PurchaseWorkflowTests
{
 static int Grni(SalesReturnTests.Fixture f)=>f.Db.Accounts.Single(a=>a.UserConfigId==f.Base.Company.Id&&a.Name=="GRNI").Id;
 static PurchaseWorkflowWrite Write(SalesReturnTests.Fixture f,decimal qty=10)=>new(){RequestKey=Guid.NewGuid().ToString(),ReferenceDate=DateTime.Today,DueDate=DateTime.Today,SupplierId=f.Base.Supplier.Id,InventoryLocationId=f.Location,Lines=new(){new(){ItemId=f.Base.Item.Id,Quantity=qty,Rate=100}}};
 static async Task<JsonElement> Create(SalesReturnTests.Fixture f,string kind,PurchaseWorkflowWrite value,bool post=true){var d=await f.Send(HttpMethod.Post,"/api/purchase-workflow/"+kind,value);return post?await f.Send(HttpMethod.Post,$"/api/purchase-workflow/{kind}/{d.GetProperty("id")}/post",new{version=1}):d;}
 static PurchaseWorkflowWrite From(SalesReturnTests.Fixture f,JsonElement source,decimal qty){var p=Write(f,qty);p.Lines[0].SourceDocumentId=source.GetProperty("id").GetInt32();p.Lines[0].SourceLineId=1;return p;}
 static async Task<decimal> Stock(SalesReturnTests.Fixture f)=>await f.Db.InventoryTransactions.Where(x=>x.UserConfigId==f.Base.Company.Id&&x.ItemId==f.Base.Item.Id&&x.Status==1).SumAsync(x=>x.QuantityIn-x.QuantityOut);
 static async Task<decimal?> Average(SalesReturnTests.Fixture f)=>await f.Db.Items.AsNoTracking().Where(i=>i.Id==f.Base.Item.Id).Select(i=>i.AverageCost).SingleAsync();
 [MySqlFact] public async Task Receipt_owns_stock_landed_cost_and_partial_bills_only_clear_grni()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var po=await Create(f,"PO",Write(f));Assert.Equal(0,await Stock(f));
  var input=From(f,po,10);input.Lines[0].LandedCosts.Add(new(){AccountId=f.Cash.Id,Amount=10});var gr=await Create(f,"GR",input);var rid=gr.GetProperty("legacyId").GetInt32();Assert.Equal(10,await Stock(f));Assert.Equal(110m,await Average(f));
  var rr=await f.Db.ReceivingReports.AsNoTracking().Include(r=>r.ReceivingReportDetails).Include(r=>r.JournalEntries).SingleAsync(r=>r.Id==rid);Assert.Equal(10m,rr.ReceivingReportDetails.Single().LandedCost);Assert.Equal(1100m,rr.Amount);Assert.Equal(0,rr.JournalEntries.Sum(j=>j.Nature=="D"?j.Amount:-j.Amount));Assert.Contains(rr.JournalEntries,j=>j.AccountId==f.Cash.Id&&j.Nature=="C"&&j.Amount==100);
  var bill=await Create(f,"PB",From(f,gr,4));var bid=bill.GetProperty("legacyId").GetInt32();Assert.Equal(10,await Stock(f));Assert.Equal(110m,await Average(f));var posted=await f.Db.Bills.AsNoTracking().Include(b=>b.BillDetails).Include(b=>b.JournalEntries).SingleAsync(b=>b.Id==bid);Assert.Equal(400m,posted.Amount);Assert.False(posted.BillDetails.Single().IsInventoryTransaction);Assert.Equal(0,posted.BillDetails.Single().LandedCost);Assert.Contains(posted.JournalEntries,j=>j.AccountId==Grni(f)&&j.Nature=="D"&&j.Amount==400);
  await Create(f,"PB",From(f,gr,6));await f.Reject(HttpMethod.Post,"/api/purchase-workflow/PB",From(f,gr,1),HttpStatusCode.Conflict);await f.Reject(HttpMethod.Post,$"/api/purchase-workflow/GR/{gr.GetProperty("id")}/cancel",new{version=2,reason="Has bills"},HttpStatusCode.Conflict);
  await f.Reject(HttpMethod.Delete,$"/api/receiving-reports/{rid}",null,HttpStatusCode.Conflict);await f.Reject(HttpMethod.Delete,$"/api/bills/{bid}",null,HttpStatusCode.Conflict);Assert.Equal(10,await Stock(f));
  var legacy=await f.Send(HttpMethod.Get,$"/api/receiving-reports/{rid}");Assert.Equal(10m,legacy.GetProperty("receivingReportDetails")[0].GetProperty("landedCost").GetDecimal());
 }
 [MySqlFact] public async Task Later_landed_adjustment_is_value_only_and_cancellation_restores_cost()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var po=await Create(f,"PO",Write(f));var input=From(f,po,10);input.Lines[0].LandedCosts.Add(new(){AccountId=f.Cash.Id,Amount=10});var gr=await Create(f,"GR",input);
  var adjustment=From(f,gr,10);adjustment.Notes="Freight actual exceeds estimate; all ten units remain in stock.";adjustment.Lines[0].InventoryAdjustment=50;adjustment.Lines[0].AdjustmentAccountId=f.Cash.Id;var lc=await Create(f,"LC",adjustment);Assert.Equal(10,await Stock(f));Assert.Equal(115m,await Average(f));Assert.Equal(50,await f.Db.InventoryTransactions.Where(t=>t.Source=="LC").SumAsync(t=>t.Amount));
  await f.Reject(HttpMethod.Delete,$"/api/general-journals/{lc.GetProperty("legacyId")}",null,HttpStatusCode.Conflict);
  await f.Send(HttpMethod.Post,$"/api/purchase-workflow/LC/{lc.GetProperty("id")}/cancel",new{version=2,reason="Correct allocation"});Assert.Equal(110m,await Average(f));Assert.Equal(10,await Stock(f));
  await f.Send(HttpMethod.Post,$"/api/purchase-workflow/GR/{gr.GetProperty("id")}/cancel",new{version=2,reason="Return entire unbilled receipt"});Assert.Equal(0,await Stock(f));
 }
 [MySqlFact] public async Task Concurrent_receipts_cannot_overreceive_and_stale_writes_fail()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var po=await Create(f,"PO",Write(f));var a=await Create(f,"GR",From(f,po,7),false);var b=await Create(f,"GR",From(f,po,7),false);
  var results=await Task.WhenAll(f.Base.Host.Client.PostAsJsonAsync($"/api/purchase-workflow/GR/{a.GetProperty("id")}/post",new{version=1}),f.Base.Host.Client.PostAsJsonAsync($"/api/purchase-workflow/GR/{b.GetProperty("id")}/post",new{version=1}));Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);Assert.Equal(7,await Stock(f));
  var order=Write(f);var draft=await Create(f,"PO",order,false);var retry=await Create(f,"PO",order,false);Assert.Equal(draft.GetProperty("id").GetInt32(),retry.GetProperty("id").GetInt32());order.Version=0;await f.Reject(HttpMethod.Put,$"/api/purchase-workflow/PO/{draft.GetProperty("id")}",order,HttpStatusCode.Conflict);
 }
 [MySqlFact] public async Task Legacy_receipt_and_direct_bill_units_survive_and_configuration_is_versioned()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var company=f.Base.Company.Id;
  var rr=await f.Send(HttpMethod.Post,"/api/receiving-reports",new{userConfigId=company,referenceNo="V1-RR",referenceDate=DateTime.Today,supplierId=f.Base.Supplier.Id,inventoryLocationId=f.Location,creditAccountId=f.Base.Account.Id,status=1,amount=220,responsibilityCenterEntry="[]",receivingReportDetails=new[]{new{itemId=f.Base.Item.Id,quantity=2,rate=100,landedCost=10,landedCostJson=$"[{{\"accountId\":{f.Cash.Id},\"amount\":10}}]",amount=220,inventoryLocationId=f.Location,status=1}},journalEntries=new[]{new{userConfigId=company,referenceNo="V1-RR",accountId=f.Base.Item.InventoryAccountId,nature="D",amount=220,balance=220,status=1,source="RR"},new{userConfigId=company,referenceNo="V1-RR",accountId=(int?)f.Base.Account.Id,nature="C",amount=200,balance=200,status=1,source="RR"},new{userConfigId=company,referenceNo="V1-RR",accountId=(int?)f.Cash.Id,nature="C",amount=20,balance=20,status=1,source="RR"}}});Assert.Equal(10,rr.GetProperty("receivingReportDetails")[0].GetProperty("landedCost").GetDecimal());Assert.Equal(2,await Stock(f));
  var cfg=await f.Send(HttpMethod.Get,"/api/purchase-workflow/configuration");await f.Send(HttpMethod.Put,"/api/purchase-workflow/configuration",new{version=cfg.GetProperty("version").GetInt64(),processingMode="ORDER_TO_PURCHASE",grniAccountId=Grni(f)});await f.Reject(HttpMethod.Put,"/api/purchase-workflow/configuration",new{version=0,processingMode="DIRECT"},HttpStatusCode.Conflict);
  await f.Reject(HttpMethod.Post,"/api/bills",new{userConfigId=company,billNo="V1",supplierId=f.Base.Supplier.Id,billDate=DateTime.Today,dueDate=DateTime.Today,status=0,billDetails=Array.Empty<object>(),journalEntries=Array.Empty<object>()},HttpStatusCode.Conflict);
 }
 [MySqlFact] public async Task Invalid_accounts_permissions_and_source_company_are_rejected()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);await f.Reject(HttpMethod.Put,"/api/purchase-workflow/configuration",new{version=1,processingMode="BOTH",grniAccountId=int.MaxValue},HttpStatusCode.BadRequest);
  var p=Write(f);p.SupplierId=int.MaxValue;await f.Reject(HttpMethod.Post,"/api/purchase-workflow/PO",p,HttpStatusCode.BadRequest);
  f.Actor.UserRole.IsAdmin=false;f.Actor.UserRole.Permission="[]";await f.Db.SaveChangesAsync();await f.Reject(HttpMethod.Get,"/api/purchase-workflow/PO",null,HttpStatusCode.Forbidden);
 }
 [MySqlFact] public async Task Direct_bill_line_total_costing_and_payment_dependency_remain_compatible()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var company=f.Base.Company.Id;
  var input=new negosuite_api.Contracts.Bills.BillCreateRequest{UserConfigId=company,BillNo="V1-DIRECT",BillDate=DateTime.Today,DueDate=DateTime.Today,SupplierId=f.Base.Supplier.Id,InventoryLocationId=f.Location,Status=1,Amount=1000,Balance=1000,IsTaxExclusive=true,ResponsibilityCenterEntry="[]",Taxes="[]",BillDetails=new(){new(){ItemId=f.Base.Item.Id,Quantity=10,Rate=100,Amount=1000,InventoryLocationId=f.Location,Status=1,IsInventoryTransaction=true,LandedCost=100,LandedCostJson=$"[{{\"accountId\":{f.Cash.Id},\"amount\":100}}]"}}};
  input.JournalEntries=new(){new(){UserConfigId=company,ReferenceNo=input.BillNo,AccountId=f.Base.Item.InventoryAccountId.Value,Nature="D",Amount=1100,Balance=1100,Status=1,Source="PU",SupplierId=f.Base.Supplier.Id},new(){UserConfigId=company,ReferenceNo=input.BillNo,AccountId=f.Base.Account.Id,Nature="C",Amount=1000,Balance=1000,Status=1,Source="PU",SupplierId=f.Base.Supplier.Id},new(){UserConfigId=company,ReferenceNo=input.BillNo,AccountId=f.Cash.Id,Nature="C",Amount=100,Balance=100,Status=1,Source="PU",SupplierId=f.Base.Supplier.Id}};
  var direct=await f.Send(HttpMethod.Post,"/api/bills",input);Assert.Equal(100m,direct.GetProperty("billDetails")[0].GetProperty("landedCost").GetDecimal());Assert.Equal(10,await Stock(f));Assert.Equal(110m,await Average(f));
  var po=await Create(f,"PO",Write(f,2));var gr=await Create(f,"GR",From(f,po,2));var bill=await Create(f,"PB",From(f,gr,2));var legacyId=bill.GetProperty("legacyId").GetInt32();var billView=await f.Send(HttpMethod.Get,$"/api/bills/{legacyId}");Assert.Equal(bill.GetProperty("id").GetInt32(),billView.GetProperty("purchaseWorkflowId").GetInt32());
  var payable=await f.Db.JournalEntries.AsNoTracking().SingleAsync(j=>j.BillId==legacyId&&j.AccountId==f.Base.Account.Id&&j.Nature=="C");
  f.Db.JournalEntries.Add(new(){UserConfigId=company,AccountId=f.Base.Account.Id,ReferenceNo="PAYMENT",JournalDate=DateTime.Today,Source="PY",Status=1,Nature="D",Amount=20,Balance=20,PaymentToJournalEntryId=payable.Id});await f.Db.SaveChangesAsync();
  await f.Reject(HttpMethod.Post,$"/api/purchase-workflow/PB/{bill.GetProperty("id")}/cancel",new{version=2,reason="Has payment"},HttpStatusCode.Conflict);Assert.Equal(12,await Stock(f));
 }
 [MySqlFact] public async Task Receipt_tax_snapshot_rounding_and_config_changes_do_not_reprice_bills()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var input=Write(f,3);input.Lines[0].Rate=112;input.IsTaxExclusive=false;input.DiscountValue=10;input.Lines[0].TaxRateId=await f.Db.TaxRates.Where(t=>t.UserConfigId==f.Base.Company.Id).Select(t=>t.Id).FirstAsync();var po=await Create(f,"PO",input);var gr=await Create(f,"GR",From(f,po,3));Assert.Equal(270m,gr.GetProperty("amount").GetDecimal());
  var config=await f.Send(HttpMethod.Get,"/api/purchase-workflow/configuration");await f.Send(HttpMethod.Put,"/api/purchase-workflow/configuration",new{version=config.GetProperty("version").GetInt64(),processingMode="DIRECT",grniAccountId=f.Cash.Id});
  decimal total=0;for(var i=0;i<3;i++){var bill=await Create(f,"PB",From(f,gr,1));total+=bill.GetProperty("amount").GetDecimal();var id=bill.GetProperty("legacyId").GetInt32();Assert.True(await f.Db.JournalEntries.AnyAsync(j=>j.BillId==id&&j.AccountId==Grni(f)&&j.Nature=="D"&&j.Amount==90));}Assert.Equal(302.4m,total);Assert.Equal(3,await Stock(f));
 }
 [MySqlFact] public async Task Purchase_migration_creates_missing_tables_and_is_repeatable()
 {
  await using var f=await SalesReturnTests.Fixture.Start();var connection=new MySqlConnectionStringBuilder(f.Db.Database.GetConnectionString());Assert.Equal(33316u,connection.Port);Assert.StartsWith("negosuite_billpayments_",connection.Database);
  await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE purchaseworkflowlink");await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE purchaseworkflowdocument");await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE purchaseconfiguration");await Prepare(f);var po=await Create(f,"PO",Write(f));Assert.Equal(1,po.GetProperty("status").GetInt32());Assert.Equal(0,await Stock(f));
 }
 [MySqlFact] public async Task Unestimated_freight_can_be_capitalized_and_expense_bills_remain_direct()
 {
  await using var f=await SalesReturnTests.Fixture.Start();await Prepare(f);var po=await Create(f,"PO",Write(f));var gr=await Create(f,"GR",From(f,po,10));
  var adjustment=From(f,gr,10);adjustment.Notes="Freight invoice arrived after receipt; all units remain.";adjustment.Lines[0].InventoryAdjustment=50;adjustment.Lines[0].AdjustmentAccountId=f.Cash.Id;
  await Create(f,"LC",adjustment);Assert.Equal(10,await Stock(f));Assert.Equal(105m,await Average(f));
  await f.Send(HttpMethod.Put,"/api/purchase-workflow/configuration",new{version=1,processingMode="ORDER_TO_PURCHASE",grniAccountId=Grni(f)});
  var item=await f.Db.Items.SingleAsync(i=>i.Id==f.Base.Item.Id);item.TrackInventory=false;await f.Db.SaveChangesAsync();
  var input=new negosuite_api.Contracts.Bills.BillCreateRequest{UserConfigId=f.Base.Company.Id,BillNo="FREIGHT",BillDate=DateTime.Today,DueDate=DateTime.Today,SupplierId=f.Base.Supplier.Id,Status=0,ResponsibilityCenterEntry="[]",BillDetails=new(){new(){ItemId=item.Id,Quantity=1,Rate=50,Amount=50,Status=0,IsInventoryTransaction=false}}};
  var bill=await f.Send(HttpMethod.Post,"/api/bills",input);Assert.Equal("FREIGHT",bill.GetProperty("billNo").GetString());
 }
 internal static async Task Prepare(SalesReturnTests.Fixture f)
 {
  await SalesWorkflowTests.Prepare(f,"INVOICE");f.Db.Accounts.Add(new Account{UserConfigId=f.Base.Company.Id,Name="GRNI",Code="GRNI",CategoryId=f.Base.Account.CategoryId});f.Base.Company.APTradeAccountId=f.Base.Account.Id;await f.Db.SaveChangesAsync();
  // The fixture's original inventory view is deliberately empty. Add actual legacy RR/PU
  // table projections before installing the production wrapper, including PU rows even
  // when IsInventoryTransaction is false to exercise source-link suppression independently.
  var definition=await f.Db.Database.SqlQueryRaw<string>("SELECT VIEW_DEFINITION AS Value FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventorytransaction'").SingleAsync(); await f.Db.Database.ExecuteSqlRawAsync("CREATE VIEW purchase_test_base AS "+definition);
  var select="d.UserConfigId,d.REFERENCE AS ReferenceNo,d.DATE AS ReferenceDate,NULL AS CustomerId,NULL AS CustomerName,d.SupplierId,s.Name AS SupplierName,l.Id AS DetailId,l.ItemId,i.Name AS ItemName,i.Cost AS ItemCost,i.AverageCost,i.ReorderPoint AS ItemReorderPoint,i.LastPurchasedDate,l.Quantity,l.Quantity AS QuantityIn,0 AS QuantityOut,l.Rate,l.Amount,d.Status,'SOURCE' AS Source,'NAME' AS SourceName,'IN' AS TransactionType,l.InventoryLocationId,w.Name AS InventoryLocationName,d.Notes,d.ResponsibilityCenterEntry";
  string Projection(string head,string detail,string fk,string reference,string date,string source)=>"SELECT "+select.Replace("REFERENCE",reference).Replace("DATE",date).Replace("SOURCE",source).Replace("NAME",head)+$" FROM {head} d JOIN {detail} l ON l.{fk}=d.Id JOIN item i ON i.Id=l.ItemId JOIN supplier s ON s.Id=d.SupplierId JOIN inventorylocation w ON w.Id=l.InventoryLocationId WHERE d.Status=1 AND l.Status=1";
  await f.Db.Database.ExecuteSqlRawAsync("CREATE OR REPLACE VIEW inventorytransaction AS SELECT * FROM purchase_test_base UNION ALL "+Projection("receivingreport","receivingreportdetail","ReceivingReportId","ReferenceNo","ReferenceDate","RR")+" UNION ALL "+Projection("bill","billdetail","BillId","BillNo","BillDate","PU"));
  var root=Directory.GetCurrentDirectory();while(!File.Exists(Path.Combine(root,"negosuite-api.csproj")))root=Directory.GetParent(root)!.FullName;await using var connection=new MySqlConnection(f.Db.Database.GetConnectionString());await connection.OpenAsync();for(var pass=0;pass<2;pass++)foreach(var name in new[]{"010_purchase_workflow.sql","011_purchase_workflow_views.sql"})new MySqlScript(connection,await File.ReadAllTextAsync(Path.Combine(root,"Db","migrations",name))).Execute();
  await f.Send(HttpMethod.Put,"/api/purchase-workflow/configuration",new{version=0,processingMode="BOTH",grniAccountId=Grni(f)});
 }
}
