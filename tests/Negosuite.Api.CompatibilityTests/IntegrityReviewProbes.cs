using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

// Review probes assert the integrity invariant, not the defective behavior.
// Run with Test-Phase3MySql.ps1: fixtures reject any host/port other than the sandbox.
public class IntegrityReviewProbes
{
    private static async Task Accepted(HttpResponseMessage response) =>
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private static async Task<(int Bill, int Journal)> SeedBill(BillPaymentTests.Fixture f)
    {
        var bill = new Bill { UserConfigId=f.Company.Id, SupplierId=f.Supplier.Id, BillNo="TARGET", BillDate=DateTime.Today,
            DueDate=DateTime.Today, Amount=100, Balance=100, Status=1 };
        var journal = new JournalEntry { UserConfigId=f.Company.Id, AccountId=f.Account.Id, SupplierId=f.Supplier.Id,
            ReferenceNo="TARGET", JournalDate=DateTime.Today, Nature="C", Source="PU", Amount=100, Balance=100, Status=1 };
        bill.JournalEntries.Add(journal);f.Db.Bills.Add(bill);await f.Db.SaveChangesAsync();return(bill.Id,journal.Id);
    }
    private static object PaymentBody(BillPaymentTests.Fixture f,int target,decimal amount=40) => new Payment {
        UserConfigId=f.Company.Id,SupplierId=f.Supplier.Id,ReferenceNo="PAY",ReferenceDate=DateTime.Today,
        Amount=amount,Balance=0,Status=1,IsBillPayment=true,PaidThroughAccountId=f.Account.Id,
        JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,SupplierId=f.Supplier.Id,
            ReferenceNo="PAY",JournalDate=DateTime.Today,Nature="D",Source="PA",Amount=amount,Balance=0,Status=1,PaymentToJournalEntryId=target},
            Counter(f,amount,"C")}
    };
    private static JournalEntry Counter(BillPaymentTests.Fixture f,decimal amount,string nature,short status=1) => new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,
        ReferenceNo="COUNTER",JournalDate=DateTime.Today,Nature=nature,Source="GJ",Amount=amount,Balance=amount,Status=status};
    private static async Task<JsonNode> CreatePayment(BillPaymentTests.Fixture f,int target)
    {
        var response=await f.Host.Client.PostAsJsonAsync("/api/payments/bill",PaymentBody(f,target));await Accepted(response);
        var id=(JsonNode.Parse(await response.Content.ReadAsStringAsync()))!["id"]!.GetValue<int>();
        return JsonNode.Parse(await f.Host.Client.GetStringAsync($"/api/payments/{id}"))!;
    }
    private static Task<decimal?> Balance(BillPaymentTests.Fixture f,int id)=>f.Db.Bills.AsNoTracking().Where(b=>b.Id==id).Select(b=>b.Balance).SingleAsync();

    [MySqlFact] public async Task AP_edit_existing_allocation_must_adjust_bill_balance()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);var payment=await CreatePayment(f,target.Journal);
        payment["amount"]=25;payment["journalEntries"]![0]!["amount"]=25;
        payment["journalEntries"]![1]!["amount"]=25;payment["journalEntries"]![1]!["balance"]=25;
        await Accepted(await f.Host.Client.PutAsJsonAsync($"/api/payments/bill/{payment["id"]}",payment));
        Assert.Equal(75m,await Balance(f,target.Bill));
    }
    [MySqlFact] public async Task AP_delete_line_must_restore_persisted_amount_not_submitted_amount()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);var payment=await CreatePayment(f,target.Journal);
        var applied=payment["journalEntries"]!.AsArray().Single(j=>j!["paymentToJournalEntryId"]!=null)!;
        applied["amount"]=25;applied["deleted"]=true;
        payment["journalEntries"]!.AsArray().Add(System.Text.Json.JsonSerializer.SerializeToNode(Counter(f,40,"D"),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        await Accepted(await f.Host.Client.PutAsJsonAsync($"/api/payments/bill/{payment["id"]}",payment));
        Assert.Equal(100m,await Balance(f,target.Bill));
    }
    [MySqlFact] public async Task Generic_payment_delete_must_not_bypass_AP_restoration()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);var payment=await CreatePayment(f,target.Journal);
        var response=await f.Host.Client.DeleteAsync($"/api/payments/{payment["id"]}");
        if(!response.IsSuccessStatusCode){Assert.Contains(response.StatusCode,new[]{HttpStatusCode.BadRequest,HttpStatusCode.Conflict});return;}
        Assert.Equal(100m,await Balance(f,target.Bill));
    }
    [MySqlFact] public async Task AP_overapplication_must_be_rejected()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);
        var response=await f.Host.Client.PostAsJsonAsync("/api/payments/bill",PaymentBody(f,target.Journal,120));
        Assert.False(response.IsSuccessStatusCode,$"Accepted overpayment; remaining balance {await Balance(f,target.Bill)}");
        var missingBalance=(Payment)PaymentBody(f,target.Journal,40);missingBalance.Amount=0;missingBalance.Balance=null;
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Host.Client.PostAsJsonAsync("/api/payments",missingBalance)).StatusCode);
        Assert.Equal(100m,await Balance(f,target.Bill));Assert.False(await f.Db.Payments.AnyAsync());
    }
    [MySqlFact] public async Task Legacy_expense_payment_must_not_write_another_company()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var response=await f.Host.Client.PostAsJsonAsync("/api/expense-payments",new ExpensePayment {
            ReferenceNo="FOREIGN",ReferenceDate=DateTime.Today,Status=1,Amount=10,Balance=10,
            JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Other.Id,AccountId=f.ForeignAccount.Id,
                ReferenceNo="FOREIGN",JournalDate=DateTime.Today,Nature="D",Source="EP",Amount=10,Balance=10,Status=1}}});
        Assert.False(response.IsSuccessStatusCode,$"Cross-company write returned {response.StatusCode}");
    }
    [MySqlFact] public async Task Posted_general_journal_must_be_balanced()
    {
        await using var f=await BillPaymentTests.Fixture.Start();f.Company.ARTradeAccountId=f.Account.Id;await f.Db.SaveChangesAsync();
        var response=await f.Host.Client.PostAsJsonAsync("/api/general-journals",new GeneralJournal{
            UserConfigId=f.Company.Id,ReferenceNo="UNBALANCED",ReferenceDate=DateTime.Today,Status=1,
            JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,
                ReferenceNo="UNBALANCED",JournalDate=DateTime.Today,Nature="D",Source="GJ",Amount=100,Balance=100,Status=1}}});
        Assert.False(response.IsSuccessStatusCode,$"Unbalanced posted journal returned {response.StatusCode}");
    }
    [MySqlFact] public async Task Multiple_bill_lines_for_one_item_must_accumulate_average_cost()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var response=await f.Host.Client.PostAsJsonAsync("/api/bills",new Bill{
            UserConfigId=f.Company.Id,SupplierId=f.Supplier.Id,BillNo="DUP-ITEM",BillDate=DateTime.Today,DueDate=DateTime.Today,
            Amount=400,Balance=400,Status=1,BillDetails=new List<BillDetail>{
                new(){ItemId=f.Item.Id,Quantity=10,Rate=10,Amount=100,Status=1,IsInventoryTransaction=true},
                new(){ItemId=f.Item.Id,Quantity=10,Rate=30,Amount=300,Status=1,IsInventoryTransaction=true}},
            JournalEntries=new List<JournalEntry>{Counter(f,400,"D"),Counter(f,400,"C")}});
        await Accepted(response);
        Assert.Equal(20m,await f.Db.Items.AsNoTracking().Where(i=>i.Id==f.Item.Id).Select(i=>i.AverageCost).SingleAsync());
    }
    [MySqlFact] public async Task Member_without_transaction_permission_must_not_post_payment()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var actor=await f.Db.Users.SingleAsync(u=>u.ConfigId==f.Company.Id);
        actor.UserRole=new UserRole{UserConfigId=f.Company.Id,Name="No transaction access",IsAdmin=false,Permission="[]"};await f.Db.SaveChangesAsync();
        var response=await f.Host.Client.PostAsJsonAsync("/api/payments",new Payment{
            UserConfigId=f.Company.Id,ReferenceNo="NO-ACCESS",ReferenceDate=DateTime.Today,Amount=10,Balance=10,Status=1});
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);
    }
    [MySqlFact] public async Task Stale_invoice_save_must_not_erase_a_subsequent_payment()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var customer=new Customer{UserConfigId=f.Company.Id,Name="Customer",Status=true};f.Db.Customers.Add(customer);
        f.Company.ARTradeAccountId=f.Account.Id;await f.Db.SaveChangesAsync();
        var invoice=new SalesInvoice{UserConfigId=f.Company.Id,CustomerId=customer.Id,InvoiceNo="STALE",InvoiceDate=DateTime.Today,DueDate=DateTime.Today,
            Amount=100,Balance=100,Status=1,JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,
                CustomerId=customer.Id,ReferenceNo="STALE",JournalDate=DateTime.Today,Nature="D",Source="SI",Amount=100,Balance=100,Status=1}}};
        f.Db.SalesInvoices.Add(invoice);await f.Db.SaveChangesAsync();
        var stale=JsonNode.Parse(await f.Host.Client.GetStringAsync($"/api/sales-invoices/{invoice.Id}"))!;
        await Accepted(await f.Host.Client.PostAsJsonAsync("/api/sales-invoice-payments",new SalesInvoicePayment{
            UserConfigId=f.Company.Id,CustomerId=customer.Id,ReferenceNo="LATER-PAY",ReferenceDate=DateTime.Today,Amount=40,Balance=0,Status=1,
            JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,CustomerId=customer.Id,
                ReferenceNo="LATER-PAY",JournalDate=DateTime.Today,Nature="C",Source="PR",Amount=40,Balance=0,Status=1,PaymentToJournalEntryId=invoice.JournalEntries.Single().Id},Counter(f,40,"D")}}));
        stale["notes"]="Old browser tab";var response=await f.Host.Client.PutAsJsonAsync($"/api/sales-invoices/{invoice.Id}",stale);
        if(!response.IsSuccessStatusCode){Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);return;}
        Assert.Equal(60m,await f.Db.SalesInvoices.AsNoTracking().Where(i=>i.Id==invoice.Id).Select(i=>i.Balance).SingleAsync());
    }
    [MySqlFact] public async Task Draft_customer_payment_must_not_reduce_posted_invoice_balance()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var customer=new Customer{UserConfigId=f.Company.Id,Name="Customer",Status=true};f.Db.Customers.Add(customer);
        f.Company.ARTradeAccountId=f.Account.Id;await f.Db.SaveChangesAsync();
        var invoice=new SalesInvoice{UserConfigId=f.Company.Id,CustomerId=customer.Id,InvoiceNo="DRAFT-TARGET",InvoiceDate=DateTime.Today,DueDate=DateTime.Today,
            Amount=100,Balance=100,Status=1,JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,
                CustomerId=customer.Id,ReferenceNo="DRAFT-TARGET",JournalDate=DateTime.Today,Nature="D",Source="SI",Amount=100,Balance=100,Status=1}}};
        f.Db.SalesInvoices.Add(invoice);await f.Db.SaveChangesAsync();
        await Accepted(await f.Host.Client.PostAsJsonAsync("/api/sales-invoice-payments",new SalesInvoicePayment{
            UserConfigId=f.Company.Id,CustomerId=customer.Id,ReferenceNo="DRAFT-PAY",ReferenceDate=DateTime.Today,Amount=40,Balance=0,Status=0,
            JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Company.Id,AccountId=f.Account.Id,CustomerId=customer.Id,
                ReferenceNo="DRAFT-PAY",JournalDate=DateTime.Today,Nature="C",Source="PR",Amount=40,Balance=0,Status=0,PaymentToJournalEntryId=invoice.JournalEntries.Single().Id}}}));
        Assert.Equal(100m,await f.Db.SalesInvoices.AsNoTracking().Where(i=>i.Id==invoice.Id).Select(i=>i.Balance).SingleAsync());
    }
    [MySqlFact] public async Task Stock_transfer_must_preserve_the_requested_document_date()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var from=new InventoryLocation{UserConfigId=f.Company.Id,Name="From",Status=true};var to=new InventoryLocation{UserConfigId=f.Company.Id,Name="To",Status=true};
        f.Db.InventoryLocations.AddRange(from,to);await f.Db.SaveChangesAsync();var date=new DateTime(2026,1,15);
        var response=await f.Host.Client.PostAsJsonAsync("/api/stock-transfers",new StockTransfer{
            UserConfigId=f.Company.Id,ReferenceNo="BACKDATE",ReferenceDate=date,Status=1,FromInventoryLocationId=from.Id,ToInventoryLocationId=to.Id,
            StockTransferDetails=new List<StockTransferDetail>{new(){ItemId=f.Item.Id,Quantity=1,Status=1}}});
        await Accepted(response);Assert.Equal(date,(await response.Content.ReadFromJsonAsync<StockTransfer>())!.ReferenceDate);
    }
    [MySqlFact] public async Task Database_failure_during_payment_save_must_roll_back_all_effects()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER review_reject_payment_journal BEFORE INSERT ON journalentry FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected review failure'");
        Exception failure=null;HttpResponseMessage response=null;
        try{response=await f.Host.Client.PostAsJsonAsync("/api/payments/bill",PaymentBody(f,target.Journal));}catch(Exception ex){failure=ex;}
        Assert.True(failure!=null||response?.IsSuccessStatusCode==false,"Injected write failure must not report success");
        Assert.Equal(100m,await Balance(f,target.Bill));
        Assert.Equal(100m,await f.Db.JournalEntries.AsNoTracking().Where(j=>j.Id==target.Journal).Select(j=>j.Balance).SingleAsync());
        Assert.False(await f.Db.Payments.AnyAsync());
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER review_reject_payment_journal");
        var payment=await CreatePayment(f,target.Journal);
        var paymentId=payment["id"]!.GetValue<int>();
        // The controller deletes the payment first; the outer transaction must also undo
        // that successful save when the subsequent balance-restoration save fails.
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER review_reject_restoration BEFORE UPDATE ON bill FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected restoration failure'");
        failure=null;response=null;
        try{response=await f.Host.Client.DeleteAsync($"/api/payments/{paymentId}");}catch(Exception ex){failure=ex;}
        Assert.True(failure!=null||response?.IsSuccessStatusCode==false);
        Assert.True(await f.Db.Payments.AnyAsync(p=>p.Id==paymentId));
        Assert.Equal(2,await f.Db.JournalEntries.CountAsync(j=>j.PaymentId==paymentId));
        Assert.Equal(60m,await Balance(f,target.Bill));
        Assert.Equal(60m,await f.Db.JournalEntries.AsNoTracking().Where(j=>j.Id==target.Journal).Select(j=>j.Balance).SingleAsync());
    }

    [MySqlFact] public async Task Concurrent_supplier_payments_cannot_overapply_the_same_bill()
    {
        await using var f=await BillPaymentTests.Fixture.Start(); var target=await SeedBill(f);
        var first=(Payment)PaymentBody(f,target.Journal,70); first.ReferenceNo="RACE-A";
        var second=(Payment)PaymentBody(f,target.Journal,70); second.ReferenceNo="RACE-B";
        var responses=await Task.WhenAll(f.Host.Client.PostAsJsonAsync("/api/payments/bill",first),f.Host.Client.PostAsJsonAsync("/api/payments",second));
        Assert.Single(responses,r=>r.IsSuccessStatusCode); Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(30m,await Balance(f,target.Bill)); Assert.Equal(1,await f.Db.Payments.CountAsync());
    }

    [MySqlFact] public async Task Duplicate_payment_retry_is_rejected_without_applying_twice()
    {
        await using var f=await BillPaymentTests.Fixture.Start(); var target=await SeedBill(f);
        await Accepted(await f.Host.Client.PostAsJsonAsync("/api/payments/bill",PaymentBody(f,target.Journal)));
        Assert.Equal(HttpStatusCode.Conflict,(await f.Host.Client.PostAsJsonAsync("/api/payments/bill",PaymentBody(f,target.Journal))).StatusCode);
        Assert.Equal(60m,await Balance(f,target.Bill)); Assert.Equal(1,await f.Db.Payments.CountAsync());
    }

    [MySqlFact] public async Task Posted_to_draft_to_posted_restores_and_reapplies_once()
    {
        await using var f=await BillPaymentTests.Fixture.Start(); var target=await SeedBill(f); var payment=await CreatePayment(f,target.Journal);
        payment["status"]=0; foreach(var j in payment["journalEntries"]!.AsArray()) j!["status"]=0;
        await Accepted(await f.Host.Client.PutAsJsonAsync($"/api/payments/{payment["id"]}",payment)); Assert.Equal(100m,await Balance(f,target.Bill));
        payment=JsonNode.Parse(await f.Host.Client.GetStringAsync($"/api/payments/{payment["id"]}"))!;
        payment["status"]=1; foreach(var j in payment["journalEntries"]!.AsArray()) j!["status"]=1;
        await Accepted(await f.Host.Client.PutAsJsonAsync($"/api/payments/bill/{payment["id"]}",payment)); Assert.Equal(60m,await Balance(f,target.Bill));
        await Accepted(await f.Host.Client.DeleteAsync($"/api/payments/{payment["id"]}")); Assert.Equal(100m,await Balance(f,target.Bill));
    }

    [MySqlFact] public async Task Reversal_uses_persisted_links_after_AP_configuration_changes()
    {
        await using var f=await BillPaymentTests.Fixture.Start(); var target=await SeedBill(f); var payment=await CreatePayment(f,target.Journal);
        f.Company.APTradeAccountId=null; await f.Db.SaveChangesAsync();
        await Accepted(await f.Host.Client.DeleteAsync($"/api/payments/bill/{payment["id"]}")); Assert.Equal(100m,await Balance(f,target.Bill));
    }

    [MySqlFact] public async Task Same_company_wrong_supplier_application_is_rejected_atomically()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);
        var other=new Supplier{UserConfigId=f.Company.Id,Name="Another vendor",Status=true};f.Db.Suppliers.Add(other);await f.Db.SaveChangesAsync();
        var body=(Payment)PaymentBody(f,target.Journal);body.SupplierId=other.Id;
        foreach(var j in body.JournalEntries)j.SupplierId=other.Id;
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Host.Client.PostAsJsonAsync("/api/payments",body)).StatusCode);
        Assert.Equal(100m,await Balance(f,target.Bill));Assert.False(await f.Db.Payments.AnyAsync());
    }

    [MySqlFact] public async Task Balanced_journal_is_accepted_and_unbalanced_update_rolls_back()
    {
        await using var f=await BillPaymentTests.Fixture.Start(); f.Company.ARTradeAccountId=f.Account.Id;await f.Db.SaveChangesAsync();
        var body=new GeneralJournal{UserConfigId=f.Company.Id,ReferenceNo="BALANCED",ReferenceDate=DateTime.Today,Status=1,
            JournalEntries=new List<JournalEntry>{Counter(f,100,"D"),Counter(f,100,"C")}};
        var response=await f.Host.Client.PostAsJsonAsync("/api/general-journals",body);await Accepted(response);
        var created=JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        created["notes"]="Must roll back";created["journalEntries"]![0]!["amount"]=99;
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Host.Client.PutAsJsonAsync($"/api/general-journals/{created["id"]}",created)).StatusCode);
        Assert.Null(await f.Db.GeneralJournals.Select(j=>j.Notes).SingleAsync());
        Assert.All(await f.Db.JournalEntries.ToListAsync(),j=>Assert.Equal(100,j.Amount));
    }

    [MySqlFact] public async Task Legacy_payment_details_are_company_scoped()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var expense=new ExpensePayment{ReferenceNo="FOREIGN",ReferenceDate=DateTime.Today,Status=1,
            JournalEntries=new List<JournalEntry>{new(){UserConfigId=f.Other.Id,AccountId=f.ForeignAccount.Id,ReferenceNo="FOREIGN",JournalDate=DateTime.Today,Nature="D",Source="EP",Status=1,Amount=10,Balance=10}}};
        var bill=new BillPayment{UserConfigId=f.Other.Id,SupplierId=f.ForeignSupplier.Id,ReferenceNo="FOREIGN",ReferenceDate=DateTime.Today,Status=1};
        f.Db.ExpensePayments.Add(expense);f.Db.BillPayments.Add(bill);await f.Db.SaveChangesAsync();
        foreach(var url in new[]{$"/api/expense-payments/{expense.Id}",$"/api/bill-payments/{bill.Id}"})
        {Assert.Equal(HttpStatusCode.NotFound,(await f.Host.Client.GetAsync(url)).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await f.Host.Client.DeleteAsync(url)).StatusCode);}
    }

    [MySqlFact] public async Task Repeated_item_costs_restore_on_edit_and_delete()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        var bill=new Bill{UserConfigId=f.Company.Id,SupplierId=f.Supplier.Id,BillNo="COST",BillDate=DateTime.Today,DueDate=DateTime.Today,Status=1,Amount=400,Balance=400,
            BillDetails=new List<BillDetail>{new(){ItemId=f.Item.Id,Quantity=10,Rate=10,Amount=100,Status=1,IsInventoryTransaction=true},new(){ItemId=f.Item.Id,Quantity=10,Rate=30,Amount=300,Status=1,IsInventoryTransaction=true}},
            JournalEntries=new List<JournalEntry>{Counter(f,400,"D"),Counter(f,400,"C")}};
        var response=await f.Host.Client.PostAsJsonAsync("/api/bills",bill);await Accepted(response);
        var saved=(await response.Content.ReadFromJsonAsync<Bill>())!;saved.BillDetails.Last().Rate=50;saved.BillDetails.Last().Amount=500;saved.BillDetails.Last().Touched=true;
        saved.Amount=saved.Balance=600;foreach(var j in saved.JournalEntries)j.Amount=j.Balance=600;
        await Accepted(await f.Host.Client.PutAsJsonAsync($"/api/bills/{saved.Id}",saved));
        Assert.Equal(30m,await f.Db.Items.AsNoTracking().Where(i=>i.Id==f.Item.Id).Select(i=>i.AverageCost).SingleAsync());
        await Accepted(await f.Host.Client.DeleteAsync($"/api/bills/{saved.Id}"));Assert.False(await f.Db.BillDetails.AnyAsync());
        Assert.Equal(0,await f.Db.InventoryTransactions.Where(i=>i.ItemId==f.Item.Id).SumAsync(i=>i.QuantityIn-i.QuantityOut));
    }

    [MySqlFact] public async Task Supplier_discount_preserves_V1_cash_and_credit_distinction()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var target=await SeedBill(f);
        var payment=(Payment)PaymentBody(f,target.Journal,90);var link=payment.JournalEntries.Single(j=>j.PaymentToJournalEntryId.HasValue);
        link.Amount=100;link.PaymentAdjustmentEntry=System.Text.Json.JsonSerializer.Serialize(new{amount=10,accountId=f.Account.Id,nature="C"});
        payment.JournalEntries.Add(Counter(f,10,"C"));
        var response=await f.Host.Client.PostAsJsonAsync("/api/payments/bill",payment);await Accepted(response);
        var saved=(await response.Content.ReadFromJsonAsync<Payment>())!;Assert.Equal(0m,saved.Balance);Assert.Equal(0m,await Balance(f,target.Bill));
        await Accepted(await f.Host.Client.DeleteAsync($"/api/payments/{saved.Id}"));Assert.Equal(100m,await Balance(f,target.Bill));
    }

    [MySqlFact] public async Task Customer_adjustment_preserves_V1_cash_and_credit_distinction()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var customer=new Customer{UserConfigId=f.Company.Id,Name="Buyer",Status=true};f.Db.Customers.Add(customer);
        f.Company.ARTradeAccountId=f.Account.Id;await f.Db.SaveChangesAsync();
        var invoice=new SalesInvoice{UserConfigId=f.Company.Id,CustomerId=customer.Id,InvoiceNo="DISCOUNT",InvoiceDate=DateTime.Today,DueDate=DateTime.Today,Amount=100,Balance=100,Status=1};
        var target=Counter(f,100,"D");target.Source="SI";target.CustomerId=customer.Id;invoice.JournalEntries.Add(target);f.Db.SalesInvoices.Add(invoice);await f.Db.SaveChangesAsync();
        var linked=Counter(f,100,"C");linked.PaymentToJournalEntryId=target.Id;linked.CustomerId=customer.Id;linked.Balance=0;
        linked.PaymentAdjustmentEntry=System.Text.Json.JsonSerializer.Serialize(new{amount=10,accountId=f.Account.Id,nature="D"});
        var payment=new SalesInvoicePayment{UserConfigId=f.Company.Id,CustomerId=customer.Id,ReferenceNo="ADJUST",ReferenceDate=DateTime.Today,Status=1,Amount=90,Balance=0,
            JournalEntries=new List<JournalEntry>{linked,Counter(f,90,"D"),Counter(f,10,"D")}};
        var response=await f.Host.Client.PostAsJsonAsync("/api/sales-invoice-payments",payment);await Accepted(response);
        var saved=(await response.Content.ReadFromJsonAsync<SalesInvoicePayment>())!;Assert.Equal(0m,saved.Balance);
        Assert.Equal(0m,await f.Db.SalesInvoices.AsNoTracking().Where(i=>i.Id==invoice.Id).Select(i=>i.Balance).SingleAsync());
        await Accepted(await f.Host.Client.DeleteAsync($"/api/sales-invoice-payments/{saved.Id}"));
        Assert.Equal(100m,await f.Db.SalesInvoices.AsNoTracking().Where(i=>i.Id==invoice.Id).Select(i=>i.Balance).SingleAsync());
    }

    [MySqlFact] public async Task Concurrent_purchase_costs_accumulate_and_backdating_keeps_latest_purchase_cost()
    {
        await using var f=await BillPaymentTests.Fixture.Start();
        Bill Make(string number,decimal rate,DateTime date)=>new(){UserConfigId=f.Company.Id,SupplierId=f.Supplier.Id,BillNo=number,BillDate=date,DueDate=date,Status=1,Amount=10*rate,Balance=10*rate,
            BillDetails=new List<BillDetail>{new(){ItemId=f.Item.Id,Quantity=10,Rate=rate,Amount=10*rate,Status=1,IsInventoryTransaction=true}},JournalEntries=new List<JournalEntry>{Counter(f,10*rate,"D"),Counter(f,10*rate,"C")}};
        var results=await Task.WhenAll(f.Host.Client.PostAsJsonAsync("/api/bills",Make("CURRENT",30,DateTime.Today)),f.Host.Client.PostAsJsonAsync("/api/bills",Make("BACKDATED",10,DateTime.Today.AddMonths(-1))));
        foreach(var response in results)await Accepted(response);
        var item=await f.Db.Items.AsNoTracking().SingleAsync(i=>i.Id==f.Item.Id);Assert.Equal(20m,item.AverageCost);Assert.Equal(30m,item.Cost);Assert.Equal(DateTime.Today,item.LastPurchasedDate);
    }

    [MySqlFact] public async Task Automatic_numbering_rolls_back_on_rejection_and_serializes_concurrent_posts()
    {
        await using var f=await BillPaymentTests.Fixture.Start();var customer=new Customer{UserConfigId=f.Company.Id,Name="Numbering buyer",Status=true};f.Db.Customers.Add(customer);
        f.Company.AutoReferenceNoConfig="{\"autoSIReferenceNo\":true,\"autoSIReferenceNoFormat\":\"########\",\"autoSIReferenceNoPrefix\":\"SEQ-\"}";await f.Db.SaveChangesAsync();
        SalesInvoice Make()=>new(){UserConfigId=f.Company.Id,CustomerId=customer.Id,InvoiceNo="AUTO",InvoiceDate=DateTime.Today,DueDate=DateTime.Today,Status=1,Amount=100,Balance=100,
            JournalEntries=new List<JournalEntry>{Counter(f,100,"D"),Counter(f,100,"C")}};
        var invalid=Make();invalid.JournalEntries.Last().Amount=99;
        Assert.Equal(HttpStatusCode.BadRequest,(await f.Host.Client.PostAsJsonAsync("/api/sales-invoices",invalid)).StatusCode);Assert.False(await f.Db.TransactionSequences.AnyAsync());
        var responses=await Task.WhenAll(f.Host.Client.PostAsJsonAsync("/api/sales-invoices",Make()),f.Host.Client.PostAsJsonAsync("/api/sales-invoices",Make()));
        foreach(var response in responses)await Accepted(response);
        Assert.Equal(new[]{"SEQ-00000001","SEQ-00000002"},await f.Db.SalesInvoices.OrderBy(i=>i.InvoiceNo).Select(i=>i.InvoiceNo).ToArrayAsync());
        Assert.Equal(2,await f.Db.TransactionSequences.Select(s=>s.LastSequence).SingleAsync());
    }
}
