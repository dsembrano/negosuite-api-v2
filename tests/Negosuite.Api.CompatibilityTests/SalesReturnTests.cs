using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using negosuite_api.Models;
using negosuite_api.Services;
using negosuite_api.Contracts.Transactions;
using Newtonsoft.Json;
using Xunit;
namespace Negosuite.Api.CompatibilityTests;
public class SalesReturnTests
{
 [Fact] public void Partial_rounding_and_allocation_preserve_posted_cents(){var shares=SalesReturnSourceService.Allocate(100m,new[]{1m,1m,1m});Assert.Equal(100m,shares.Sum());Assert.Equal(new[]{33.34m,33.33m,33.33m},shares);Assert.Equal(100m,Enumerable.Range(0,3).Sum(i=>SalesReturnSourceService.Portion(100,i,1,3)));}
 [PagePreferenceTests.MySqlTheory,InlineData(false),InlineData(true)]
 public async Task Draft_post_partial_returns_credit_and_void_are_atomic(bool cash)
 {
  await using var f=await Fixture.Start(cash);var request=f.Write(1);request.Applications=cash?new():new(){new(){JournalEntryId=f.Target,Amount=200}};
  var draft=await f.Send(HttpMethod.Post,"/api/sales-returns",request);var id=draft.GetProperty("id").GetInt32();Assert.Equal(0,draft.GetProperty("status").GetInt32());Assert.Equal(336m,draft.GetProperty("amount").GetDecimal());
  Assert.Equal(0,await f.Db.JournalEntries.CountAsync(j=>j.Source=="SRT"));Assert.Equal(1008m,await f.TargetBalance());
  var repeated=await f.Send(HttpMethod.Post,"/api/sales-returns",request);Assert.Equal(id,repeated.GetProperty("id").GetInt32());
  var posted=await f.Send(HttpMethod.Post,$"/api/sales-returns/{id}/post",new{version=1});Assert.Equal(1,posted.GetProperty("status").GetInt32());Assert.Equal(cash?336m:136m,posted.GetProperty("balance").GetDecimal());
  Assert.Equal(cash?1008m:808m,await f.TargetBalance());
  var list=await f.Send(HttpMethod.Get,"/api/sales-returns?pageNumber=1&pageSize=25&sortBy=balance&sortDirection=desc");Assert.Equal(cash?336m:136m,list.GetProperty("items")[0].GetProperty("balance").GetDecimal());
  var exported=await f.Send(HttpMethod.Get,"/api/sales-returns?sortBy=referenceNo&sortDirection=asc");Assert.Single(exported.EnumerateArray());
  var entries=await f.Db.JournalEntries.AsNoTracking().Where(j=>j.SalesReturnId==id).ToListAsync();Assert.Equal(0,entries.Sum(j=>j.Nature=="D"?j.Amount:-j.Amount));Assert.All(entries,j=>Assert.Equal("SRT",j.Source));Assert.DoesNotContain(entries,j=>j.AccountId==f.Cash.Id);Assert.Equal(1m,await f.Stock());
  await f.Send(HttpMethod.Post,$"/api/sales-returns/{id}/post",new{version=1});Assert.Equal(entries.Count,await f.Db.JournalEntries.CountAsync(j=>j.SalesReturnId==id));
  var over=f.Write(3);await f.Reject(HttpMethod.Post,"/api/sales-returns",over,HttpStatusCode.Conflict);
  var second=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(2));await f.Send(HttpMethod.Post,$"/api/sales-returns/{second.GetProperty("id")}/post",new{version=1});Assert.Equal(3m,await f.Stock());
  Assert.Equal(1008m,await f.Db.SalesReturns.Where(r=>r.Status==1).SumAsync(r=>r.Amount));Assert.Equal(108m,await f.Db.SalesReturns.Where(r=>r.Status==1).SumAsync(r=>r.TaxAmount));
  await f.Reject(HttpMethod.Put,$"/api/sales-returns/{id}",request,HttpStatusCode.BadRequest);
  await f.Send(HttpMethod.Post,$"/api/sales-returns/{id}/void",new{version=2,reason="Wrong quantity"});Assert.Equal(1008m,await f.TargetBalance());Assert.Equal(2m,await f.Stock());
  Assert.Equal(-1,(await f.Send(HttpMethod.Get,$"/api/sales-returns/{id}")).GetProperty("status").GetInt32());
 }
 [MySqlFact] public async Task Stale_versions_tenant_and_permissions_are_enforced()
 {
  await using var f=await Fixture.Start();var request=f.Write(1);var draft=await f.Send(HttpMethod.Post,"/api/sales-returns",request);var id=draft.GetProperty("id").GetInt32();request.Version=1;request.ReferenceNo=draft.GetProperty("referenceNo").GetString();await f.Send(HttpMethod.Put,$"/api/sales-returns/{id}",request);await f.Reject(HttpMethod.Put,$"/api/sales-returns/{id}",request,HttpStatusCode.Conflict);
  request.SourceId=99999;await f.Reject(HttpMethod.Post,"/api/sales-returns/preview",request,HttpStatusCode.NotFound);
  var page=await f.Send(HttpMethod.Get,"/api/sales-returns?status=0&pageNumber=1&pageSize=1&sortBy=referenceNo&sortDirection=asc");Assert.Equal(1,page.GetProperty("totalCount").GetInt32());Assert.Single(page.GetProperty("items").EnumerateArray());
  await f.Send(HttpMethod.Put,"/api/me/page-preferences/sales-returns",new{version=0,columns=new{referenceDate=false,sourceNo=true}});var preferences=await f.Send(HttpMethod.Get,"/api/me/page-preferences/sales-returns");Assert.False(preferences.GetProperty("columns").GetProperty("referenceDate").GetBoolean());
  f.Actor.UserRole.IsAdmin=false;f.Actor.UserRole.Permission="[]";await f.Db.SaveChangesAsync();await f.Reject(HttpMethod.Get,"/api/sales-returns",null,HttpStatusCode.Forbidden);
 }
 [MySqlFact] public async Task Overapplication_is_rejected_and_consumed_credit_blocks_void()
 {
  await using var f=await Fixture.Start();var request=f.Write(1);request.Applications=new(){new(){JournalEntryId=f.Target,Amount=337}};await f.Reject(HttpMethod.Post,"/api/sales-returns",request,HttpStatusCode.BadRequest);Assert.Equal(1008m,await f.TargetBalance());
  request.Applications.Clear();var draft=await f.Send(HttpMethod.Post,"/api/sales-returns",request);var id=draft.GetProperty("id").GetInt32();await f.Send(HttpMethod.Post,$"/api/sales-returns/{id}/post",new{version=1});var credit=await f.Db.JournalEntries.SingleAsync(j=>j.SalesReturnId==id&&j.AccountId==f.Base.Account.Id&&j.Nature=="C");credit.Balance-=1;await f.Db.SaveChangesAsync();await f.Reject(HttpMethod.Post,$"/api/sales-returns/{id}/void",new{version=2,reason="Cancel"},HttpStatusCode.BadRequest);Assert.Equal(1m,await f.Stock());
 }
 [MySqlFact] public async Task Concurrent_posts_cannot_return_more_than_sold()
 {
  await using var f=await Fixture.Start();var a=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(2));var b=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(2));
  var results=await Task.WhenAll(f.Base.Host.Client.PostAsJsonAsync($"/api/sales-returns/{a.GetProperty("id")}/post",new{version=1}),f.Base.Host.Client.PostAsJsonAsync($"/api/sales-returns/{b.GetProperty("id")}/post",new{version=1}));Assert.Single(results,r=>r.IsSuccessStatusCode);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);Assert.Equal(2m,await f.Stock());
 }
 [MySqlFact] public async Task Migrations_are_repeatable_and_source_financial_edits_are_blocked()
 {
  await using var f=await Fixture.Start();await f.Migrate();var draft=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(1));await f.Send(HttpMethod.Post,$"/api/sales-returns/{draft.GetProperty("id")}/post",new{version=1});
  await Assert.ThrowsAsync<MySqlException>(()=>f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE salesinvoice SET Amount=Amount+1 WHERE Id={f.SourceId}"));
  Assert.Equal(-336m,await f.Db.Database.SqlQueryRaw<decimal>("SELECT SUM(Amount) AS Value FROM salestransaction WHERE Source='SRT' AND Status=1").SingleAsync());
 }
 [MySqlFact] public async Task Return_after_void_still_reverses_exact_original_cents()
 {
  await using var f=await Fixture.Start();var db=f.Db;
  var invoice=await db.SalesInvoices.Include(i=>i.SalesInvoiceDetails).Include(i=>i.JournalEntries).ThenInclude(j=>j.Account).SingleAsync(i=>i.Id==f.SourceId);
  invoice.Amount=100;invoice.Balance=100;invoice.Taxes=invoice.Taxes.Replace("108","10.71");var line=invoice.SalesInvoiceDetails.Single();line.Amount=100;line.Rate=33.3333m;line.TaxAmount=10.71m;
  foreach(var j in invoice.JournalEntries){if(j.Id==f.Target)j.Amount=j.Balance=100;if(j.Account.Name=="Sales")j.Amount=j.Balance=89.29m;if(j.Account.Name=="Output VAT")j.Amount=j.Balance=10.71m;}await db.SaveChangesAsync();
  var ids=new List<int>();for(var n=0;n<3;n++){var r=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(1));ids.Add(r.GetProperty("id").GetInt32());await f.Send(HttpMethod.Post,$"/api/sales-returns/{ids.Last()}/post",new{version=1});}
  await f.Send(HttpMethod.Post,$"/api/sales-returns/{ids.Last()}/void",new{version=2,reason="Re-enter"});var replacement=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(1));await f.Send(HttpMethod.Post,$"/api/sales-returns/{replacement.GetProperty("id")}/post",new{version=1});Assert.Equal(100m,await db.SalesReturns.Where(r=>r.Status==1).SumAsync(r=>r.Amount));
 }
 [MySqlFact] public async Task Payment_and_return_share_the_same_live_invoice_balance()
 {
  await using var f=await Fixture.Start();var request=f.Write(1);request.Applications=new(){new(){JournalEntryId=f.Target,Amount=336}};var draft=await f.Send(HttpMethod.Post,"/api/sales-returns",request);await f.Send(HttpMethod.Post,$"/api/sales-returns/{draft.GetProperty("id")}/post",new{version=1});
  var customer=await f.Db.SalesInvoices.Where(i=>i.Id==f.SourceId).Select(i=>i.CustomerId).SingleAsync();
  var payment=new SalesInvoicePayment{UserConfigId=f.Base.Company.Id,CustomerId=customer,ReferenceNo="PAY-AFTER-RETURN",ReferenceDate=DateTime.Today,Amount=672,Balance=0,Status=1,PaymentModeId=f.Base.Mode.Id,DepositToAccountId=f.Cash.Id,JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Base.Company.Id,AccountId=f.Cash.Id,ReferenceNo="PAY-AFTER-RETURN",JournalDate=DateTime.Today,Nature="D",Source="PR",Status=1,Amount=672,Balance=672},new(){UserConfigId=f.Base.Company.Id,AccountId=f.Base.Account.Id,CustomerId=customer,ReferenceNo="PAY-AFTER-RETURN",JournalDate=DateTime.Today,Nature="C",Source="PR",Status=1,Amount=672,Balance=0,PaymentToJournalEntryId=f.Target}}};
  await f.Send(HttpMethod.Post,"/api/sales-invoice-payments",payment);Assert.Equal(0,await f.TargetBalance());payment.ReferenceNo="OVER";await f.Reject(HttpMethod.Post,"/api/sales-invoice-payments",payment,HttpStatusCode.Conflict);Assert.Equal(0,await f.TargetBalance());
  await f.Send(HttpMethod.Post,$"/api/sales-returns/{draft.GetProperty("id")}/void",new{version=2,reason="Undo return"});Assert.Equal(336,await f.TargetBalance());
 }
 [MySqlFact] public async Task Separate_discount_is_reversed_from_the_original_journals()
 {
  await using var f=await Fixture.Start(discount:true);var d=await f.Send(HttpMethod.Post,"/api/sales-returns",f.Write(3));Assert.Equal(100m,d.GetProperty("discountAmount").GetDecimal());Assert.Equal(1008m,d.GetProperty("amount").GetDecimal());await f.Send(HttpMethod.Post,$"/api/sales-returns/{d.GetProperty("id")}/post",new{version=1});var posted=await f.Db.JournalEntries.AsNoTracking().Where(j=>j.SalesReturnId==d.GetProperty("id").GetInt32()).ToListAsync();Assert.Contains(posted,j=>j.AccountId==f.Base.Company.DiscountAccountId&&j.Nature=="C"&&j.Amount==100);
 }
 internal sealed class Fixture:IAsyncDisposable
 {
  public BillPaymentTests.Fixture Base;public negosuiteContext Db=>Base.Db;public User Actor;public Account Cash;public int SourceId,LineId,Target,Location;public bool IsCash;
  public static async Task<Fixture> Start(bool cash=false,bool discount=false)
  {
   var f=new Fixture{Base=await BillPaymentTests.Fixture.Start(),IsCash=cash};var db=f.Db;var company=f.Base.Company.Id;
   try{
    f.Actor=await db.Users.SingleAsync(u=>u.ConfigId==company);f.Actor.UserRole=new(){UserConfigId=company,Name="Return test administrator",IsAdmin=true};
    Account Account(string name,string code){var a=new Account{UserConfigId=company,Name=name,Code=code,CategoryId=f.Base.Account.CategoryId};db.Accounts.Add(a);return a;}
    var discountAccount=Account("Sales discounts","4900");var sales=Account("Sales","4000");var tax=Account("Output VAT","2100");var inventory=Account("Inventory","1300");var cost=Account("Cost of sales","5000");f.Cash=Account("Cash","1000");
    var customer=new Customer{UserConfigId=company,Name="Return customer",Status=true};db.Customers.Add(customer);var location=new InventoryLocation{UserConfigId=company,Name="Main",Status=true};db.InventoryLocations.Add(location);await db.SaveChangesAsync();f.Location=location.Id;
    var vat=new TaxRate{UserConfigId=company,Name="Output VAT",Rate=12,TaxAccountId=tax.Id};db.TaxRates.Add(vat);await db.SaveChangesAsync();
    f.Base.Company.ARTradeAccountId=f.Base.Account.Id;if(discount)f.Base.Company.DiscountAccountId=discountAccount.Id;f.Base.Company.AutoReferenceNoConfig="{\"autoSRTReferenceNo\":true,\"autoSRTReferenceNoPrefix\":\"RET-\",\"autoSRTReferenceNoFormat\":\"######\"}";
    f.Base.Item.SalesAccountId=sales.Id;f.Base.Item.InventoryAccountId=inventory.Id;f.Base.Item.PurchaseAccountId=cost.Id;f.Base.Item.Unit="PCS";
    JournalEntry Entry(Account account,string nature,decimal amount)=>new(){UserConfigId=company,ReferenceNo="ORIGINAL",JournalDate=DateTime.Today,AccountId=account.Id,Nature=nature,Amount=amount,Balance=amount,CustomerId=customer.Id,Status=1,Source=cash?"SR":"SI"};
    var entries=new List<JournalEntry>{Entry(cash?f.Cash:f.Base.Account,"D",1008),Entry(sales,"C",discount?1000:900),Entry(tax,"C",108),Entry(inventory,"C",120),Entry(cost,"D",120)};
    if(discount)entries.Add(Entry(discountAccount,"D",100));
    var taxes=JsonConvert.SerializeObject(new[]{new{taxRate=new{id=vat.Id,name=vat.Name,rate=12,taxAccountId=tax.Id},amount=108}});
    if(cash){var s=new SalesReceipt{UserConfigId=company,CustomerId=customer.Id,ReceiptNo="CASH-1",ReceiptDate=DateTime.Today,Amount=1008,Balance=0,DepositToAccountId=f.Cash.Id,InventoryLocationId=location.Id,Taxes=taxes,Status=1,JournalEntries=entries};s.SalesReceiptDetails.Add(new(){ItemId=f.Base.Item.Id,Quantity=3,Rate=discount?373.3333m:336,Amount=discount?1120:1008,DiscountAmount=discount?100:0,Cost=40,TaxRateId=vat.Id,TaxAmount=108,IsInventoryTransaction=true,Status=1});db.SalesReceipts.Add(s);await db.SaveChangesAsync();f.SourceId=s.Id;f.LineId=s.SalesReceiptDetails.Single().Id;}
    else{var s=new SalesInvoice{UserConfigId=company,CustomerId=customer.Id,InvoiceNo="CHARGE-1",InvoiceDate=DateTime.Today,Amount=1008,Balance=1008,InventoryLocationId=location.Id,Taxes=taxes,Status=1,JournalEntries=entries};s.SalesInvoiceDetails.Add(new(){ItemId=f.Base.Item.Id,Quantity=3,Rate=discount?373.3333m:336,Amount=discount?1120:1008,DiscountAmount=discount?100:0,Cost=40,TaxRateId=vat.Id,TaxAmount=108,IsInventoryTransaction=true,Status=1});db.SalesInvoices.Add(s);await db.SaveChangesAsync();f.SourceId=s.Id;f.LineId=s.SalesInvoiceDetails.Single().Id;}
    f.Target=entries[0].Id;await f.InstallViews();await f.RemoveGeneratedReturnSchema();await f.Migrate();return f;
   }catch{await f.DisposeAsync();throw;}
  }
  public SalesReturnWriteRequest Write(decimal quantity)=>new(){RequestKey=Guid.NewGuid().ToString(),Source=IsCash?"SR":"SI",SourceId=SourceId,ReferenceDate=DateTime.Today,InventoryLocationId=Location,Reason="Damaged goods",Lines=new(){new(){SourceDetailId=LineId,Quantity=quantity}}};
  public async Task<JsonElement> Send(HttpMethod method,string url,object body=null){using var response=await Base.Host.Client.SendAsync(new HttpRequestMessage(method,url){Content=body==null?null:JsonContent.Create(body)});Assert.True(response.IsSuccessStatusCode,$"{url}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");return await response.Content.ReadFromJsonAsync<JsonElement>();}
  public async Task Reject(HttpMethod method,string url,object body,HttpStatusCode status){using var response=await Base.Host.Client.SendAsync(new HttpRequestMessage(method,url){Content=body==null?null:JsonContent.Create(body)});Assert.True(response.StatusCode==status,$"Expected {status}; {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");}
  public Task<decimal> TargetBalance()=>Db.JournalEntries.AsNoTracking().Where(j=>j.Id==Target).Select(j=>j.Balance).SingleAsync();
  public Task<decimal> Stock()=>Db.Database.SqlQueryRaw<decimal>("SELECT COALESCE(SUM(QuantityIn),0) AS Value FROM inventorytransaction WHERE Source='SRT'").SingleAsync();
  public async Task Migrate(){var root=Directory.GetCurrentDirectory();while(!File.Exists(Path.Combine(root,"negosuite-api.csproj")))root=Directory.GetParent(root)!.FullName;await using var connection=new MySqlConnection(Db.Database.GetConnectionString());await connection.OpenAsync();foreach(var name in new[]{"002_sales_return.sql","003_sales_return_views.sql","004_sales_return_source_guards.sql","005_sales_return_deleted_source_links.sql"}){var script=new MySqlScript(connection,await File.ReadAllTextAsync(Path.Combine(root,"Db","migrations",name)));script.Execute();}}
  private async Task RemoveGeneratedReturnSchema()
  {
   // This fixture owns a GUID-named database on the isolated port, never configured data.
   var connection=new MySqlConnectionStringBuilder(Db.Database.GetConnectionString());Assert.Equal(33316u,connection.Port);Assert.StartsWith("negosuite_billpayments_",connection.Database);
   var keys=await Db.Database.SqlQueryRaw<string>("SELECT CONSTRAINT_NAME AS Value FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='journalentry' AND COLUMN_NAME='SalesReturnId' AND REFERENCED_TABLE_NAME='salesreturn'").ToListAsync();
   foreach(var key in keys)await Db.Database.ExecuteSqlRawAsync("ALTER TABLE journalentry DROP FOREIGN KEY `"+key.Replace("`","``")+"`");
   await Db.Database.ExecuteSqlRawAsync("ALTER TABLE journalentry DROP COLUMN SalesReturnId");await Db.Database.ExecuteSqlRawAsync("DROP TABLE salesreturndetail");await Db.Database.ExecuteSqlRawAsync("DROP TABLE salesreturn");
  }
  private async Task InstallViews(){await Db.Database.ExecuteSqlRawAsync("DROP VIEW inventorytransaction");
   await Db.Database.ExecuteSqlRawAsync("CREATE VIEW inventorytransaction AS SELECT 0 AS UserConfigId,'' AS ReferenceNo,CURRENT_DATE AS ReferenceDate,NULL AS CustomerId,'' AS CustomerName,NULL AS SupplierId,'' AS SupplierName,0 AS DetailId,0 AS ItemId,'' AS ItemName,0 AS ItemCost,0 AS AverageCost,0 AS ItemReorderPoint,NULL AS LastPurchasedDate,0 AS Quantity,0 AS QuantityIn,0 AS QuantityOut,0 AS Rate,0 AS Amount,1 AS Status,'' AS Source,'' AS SourceName,'' AS TransactionType,0 AS InventoryLocationId,'' AS InventoryLocationName,'' AS Notes,NULL AS ResponsibilityCenterEntry WHERE FALSE");
   await Db.Database.ExecuteSqlRawAsync("CREATE VIEW salestransaction AS SELECT '' AS SourceId,0 AS UserConfigId,'' AS ReferenceNo,CURRENT_DATE AS ReferenceDate,0 AS CustomerId,'' AS CustomerName,0 AS DiscountPercent,0 AS DiscountAmount,0 AS Cost,0 AS Amount,0 AS Balance,NULL AS Taxes,0 AS TaxAmount,'' AS Source,'' AS SourceName,NULL AS ResponsibilityCenterEntry,1 AS Status WHERE FALSE");}
  public ValueTask DisposeAsync()=>Base.DisposeAsync();
 }
}
