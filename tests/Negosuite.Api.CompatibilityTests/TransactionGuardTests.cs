using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class TransactionGuardTests
{
    [MySqlFact]
    public async Task Legacy_expense_with_mixed_header_and_journal_ownership_is_not_exposed()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var body = new ExpensePayment { ReferenceNo = "MIXED", ReferenceDate = DateTime.Today, SupplierId = f.ForeignSupplier.Id, Amount = 10, Balance = 10, Status = 1,
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = "MIXED", JournalDate = DateTime.Today,
                Nature = "D", Source = "EP", Amount = 10, Balance = 10, Status = 1 } } };
        f.Db.ExpensePayments.Add(body); await f.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"/api/expense-payments/{body.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.DeleteAsync($"/api/expense-payments/{body.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task New_supplier_application_cannot_target_a_non_trade_account()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var account = new Account { UserConfigId = f.Company.Id, Code = "TAX", Name = "Tax liability", CategoryId = f.Account.CategoryId };
        f.Db.Accounts.Add(account); await f.Db.SaveChangesAsync();
        var bill = new Bill { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, BillNo = "TARGET", BillDate = DateTime.Today, DueDate = DateTime.Today, Status = 1, Amount = 100, Balance = 100,
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = account.Id, ReferenceNo = "TARGET", JournalDate = DateTime.Today,
                Nature = "C", Source = "PU", Amount = 100, Balance = 100, Status = 1 } } };
        f.Db.Bills.Add(bill); await f.Db.SaveChangesAsync();
        var payment = new Payment { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, ReferenceNo = "WRONG-ACCOUNT", ReferenceDate = DateTime.Today, Status = 1, Amount = 40, Balance = 0, IsBillPayment = true,
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = account.Id, ReferenceNo = "PAY", JournalDate = DateTime.Today,
                Nature = "D", Source = "PV", Amount = 40, Balance = 0, Status = 1, PaymentToJournalEntryId = bill.JournalEntries.Single().Id } } };
        FixtureJournals.Balance(payment);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/payments", payment)).StatusCode);
        Assert.Equal(100m, await f.Db.Bills.AsNoTracking().Select(b => b.Balance).SingleAsync()); Assert.False(await f.Db.Payments.AnyAsync());
    }

    [MySqlFact]
    public async Task Existing_center_scope_cannot_be_bypassed_by_clearing_the_submitted_assignment()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var journal = new GeneralJournal { UserConfigId = f.Company.Id, ReferenceNo = "SCOPED", ReferenceDate = DateTime.Today, Status = 1,
            ResponsibilityCenterEntry = "[{\"typeId\":1,\"id\":2}]" };
        f.Db.GeneralJournals.Add(journal);
        var actor = await f.Db.Users.SingleAsync(u => u.ConfigId == f.Company.Id);
        actor.UserRole = new UserRole { UserConfigId = f.Company.Id, Name = "Branch one", Permission = "[{\"moduleId\":\"4310\",\"canEdit\":true,\"canDelete\":true}]",
            AdvancePermission = "[{\"responsibilityCenterTypeId\":1,\"responsibilityCenterIds\":[1]}]" };
        await f.Db.SaveChangesAsync();
        var body = await f.Host.Client.GetFromJsonAsync<GeneralJournal>($"/api/general-journals/{journal.Id}");
        body!.ResponsibilityCenterEntry = null;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"/api/general-journals/{journal.Id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.DeleteAsync($"/api/general-journals/{journal.Id}")).StatusCode);
        Assert.Equal(journal.ResponsibilityCenterEntry, await f.Db.GeneralJournals.AsNoTracking().Select(j => j.ResponsibilityCenterEntry).SingleAsync());
    }

    [MySqlFact]
    public async Task Owned_legacy_expense_payment_keeps_its_create_edit_and_delete_contract()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        var body = new ExpensePayment { ReferenceNo = "LEGACY", ReferenceDate = DateTime.Today, SupplierId = f.Supplier.Id, Amount = 10, Balance = 10, Status = 1,
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = "LEGACY", JournalDate = DateTime.Today,
                Nature = "D", Source = "EP", Amount = 10, Balance = 10, Status = 1 } } };
        FixtureJournals.Balance(body);
        var created = await f.Host.Client.PostAsJsonAsync("/api/expense-payments", body);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var saved = await created.Content.ReadFromJsonAsync<ExpensePayment>(); saved!.Notes = "Updated";
        var update = await f.Host.Client.PutAsJsonAsync($"/api/expense-payments/{saved.Id}", saved);
        Assert.True(update.StatusCode == HttpStatusCode.NoContent, await update.Content.ReadAsStringAsync());
        Assert.Equal("Updated", await f.Db.ExpensePayments.AsNoTracking().Select(p => p.Notes).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"/api/expense-payments/{saved.Id}")).StatusCode);
        Assert.Equal(-1, await f.Db.ExpensePayments.AsNoTracking().Select(p => p.Status).SingleAsync());
        Assert.All(await f.Db.JournalEntries.AsNoTracking().ToListAsync(), j => Assert.Equal(-1, j.Status));
    }

    [MySqlFact]
    public async Task Invalid_purchase_quantity_or_mixed_posting_status_returns_400_and_rolls_back()
    {
        await using var f = await BillPaymentTests.Fixture.Start();
        foreach (var (quantity, status) in new[] { (-1m, (short)1), (1m, (short)0) })
        {
            var body = new Bill { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, BillNo = "INVALID", BillDate = DateTime.Today, DueDate = DateTime.Today,
                Status = 1, Amount = 10, Balance = 10, BillDetails = new List<BillDetail> { new() { ItemId = f.Item.Id, Quantity = quantity, Rate = 10, Amount = 10, Status = status } },
                JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = "INVALID", JournalDate = DateTime.Today,
                    Nature = "C", Source = "PU", Amount = 10, Balance = 10, Status = 1 } } };
            FixtureJournals.Balance(body);
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/bills", body)).StatusCode);
            Assert.False(await f.Db.Bills.AnyAsync()); Assert.False(await f.Db.JournalEntries.AnyAsync());
        }
    }
}
