using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class CashDisbursementTests
{
    private readonly Config config = new() { APTradeAccountId = 1, ARTradeAccountId = 8 };
    private readonly Dictionary<int, Account> accounts = new[] {
        new Account { Id = 1, Name = "AP" }, new Account { Id = 2, Name = "Bank", Category = new() {Type="Asset"} },
        new Account { Id = 3, Name = "Input VAT" }, new Account { Id = 4, Name = "Expense", Category = new() {Type="Expense"} }, new Account { Id = 7, Name="Inventory" }
    }.ToDictionary(a => a.Id);
    private readonly Dictionary<int, TaxRate> taxes = new() { [1] = new() {Id=1,Rate=12,TaxAccountId=3} };
    private readonly HashSet<int> inventory = new() { 7 };
    private static JournalEntry Entry(int account, decimal amount, string nature, bool computed = false) => new() {
        AccountId=account,Amount=amount,Balance=amount,Nature=nature,IsComputed=computed,Source="PV",Status=1,ResponsibilityCenterEntry="[]"
    };
    private static Payment Payment() => new() { ReferenceNo="CD-001",ReferenceDate=DateTime.Today,Payee="Sample",PaymentModeId=1,PaidThroughAccountId=2,Amount=100,Balance=null,Status=1,ResponsibilityCenterEntry="[]",JournalEntries=new List<JournalEntry> {Entry(4,100,"D"),Entry(2,100,"C",true)} };
    private string Validate(Payment payment, bool advanced = false, ResponsibilityCenterType[] types = null, ResponsibilityCenter[] centers = null, UserRole role = null) => CashDisbursementRules.Validate(payment, config, accounts, inventory, taxes, types ?? [], centers ?? [], role ?? new() {IsAdmin=true}, advanced);

    [Fact] public void Ordinary_balanced_direct_payment_is_valid() => Assert.Null(Validate(Payment()));
    [Fact] public void Draft_has_draft_journals() { var p=Payment();p.Status=0;Assert.NotNull(Validate(p));foreach(var j in p.JournalEntries)j.Status=0;Assert.Null(Validate(p)); }
    [Theory, InlineData(1), InlineData(7)] public void AP_and_inventory_accounts_are_blocked(int account) {var p=Payment();p.JournalEntries.First().AccountId=account;Assert.NotNull(Validate(p));}
    [Fact] public void AP_flow_cannot_be_converted() {var p=Payment();p.IsBillPayment=true;Assert.NotNull(Validate(p));Assert.NotNull(CashDisbursementRules.Existing(p,1,inventory));}
    [Fact] public void Credits_require_advanced_permission() {var p=Payment();p.JournalEntries.First().Amount=p.JournalEntries.First().Balance=110;p.JournalEntries.Add(Entry(3,10,"C"));Assert.Contains("General Journal",Validate(p));Assert.Null(Validate(p,true));}
    [Theory, InlineData(12,112), InlineData(-2,98), InlineData(0,100)] public void Tax_entries_are_matched_to_source_formula(decimal rate, decimal total) {var p=Payment();taxes[1].Rate=rate;p.Amount=total;p.JournalEntries.First().TaxRateId=1;p.JournalEntries.Last().Amount=p.JournalEntries.Last().Balance=total;p.JournalEntries.Add(Entry(3,rate,"D",true));Assert.Null(Validate(p));p.JournalEntries.Last().Amount+=1;Assert.NotNull(Validate(p));}
    [Fact] public void Cannot_disguise_manual_credit_as_computed() {var p=Payment();p.JournalEntries.First().Amount=p.JournalEntries.First().Balance=110;p.JournalEntries.Add(Entry(3,10,"C",true));Assert.Contains("Unexpected",Validate(p));}
    [Fact] public void Imbalanced_journal_is_rejected() {var p=Payment();p.JournalEntries.First().Amount=p.JournalEntries.First().Balance=90;Assert.Contains("balance",Validate(p));}
    [Fact] public void Check_requires_number() {var p=Payment();p.PaymentModeId=2;Assert.Contains("check",Validate(p));p.CheckNo="123";Assert.Null(Validate(p));}
    [Fact] public void Required_party_is_checked_for_tax_accounts() {var p=Payment();accounts[4].RequireSupplier=true;Assert.Contains("supplier",Validate(p));p.JournalEntries.First().SupplierId=9;Assert.Null(Validate(p));}
    [Fact] public void Required_centers_and_role_restrictions_are_enforced() {
        var p=Payment();var types=new[]{new ResponsibilityCenterType{Id=10,Name="Branch",RequiredBy="COST",RequiredByTags="[]"}};
        var centers=new[]{new ResponsibilityCenter{Id=20,ResponsibilityCenterTypeId=10}};
        Assert.Contains("Branch",Validate(p,false,types,centers));
        p.JournalEntries.First().ResponsibilityCenterEntry="[{\"id\":20,\"typeId\":10}]";
        Assert.Null(Validate(p,false,types,centers));
        Assert.Contains("access denied",Validate(p,false,types,centers,new(){AdvancePermission="[{\"responsibilityCenterTypeId\":10,\"responsibilityCenterIds\":[]}]"}));
    }
    [Fact] public void Applied_balances_and_links_prevent_edit_or_delete() {var p=Payment();p.JournalEntries.First().Balance=0;Assert.NotNull(CashDisbursementRules.Existing(p,1,inventory));p.JournalEntries.First().Balance=100;p.JournalEntries.First().PaymentToJournalEntryId=44;Assert.NotNull(CashDisbursementRules.Existing(p,1,inventory));}
    [Fact] public void Permission_requires_exact_module_and_action() {
        var role=new UserRole {Permission="[{\"moduleId\":\"4240\",\"canCreate\":true,\"canEdit\":false}]"};
        Assert.True(CashDisbursementRules.Can(role,"4240","canCreate"));Assert.False(CashDisbursementRules.Can(role,"4240","canEdit"));Assert.False(CashDisbursementRules.Can(role,"4310","canCreate"));
        role.Permission="broken";Assert.False(CashDisbursementRules.Can(role,"4240","canCreate"));
    }
    [Fact] public void Inventory_account_flag_translates_to_scoped_SQL_without_loading_items() {
        using var db=new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var sql=new AccountService(db).ListQuery(42).ToQueryString();Assert.Contains("EXISTS",sql);Assert.Contains("TrackInventory",sql);Assert.Contains("InventoryAccountId",sql);Assert.Contains("UserConfigId",sql);
    }
}
