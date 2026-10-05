using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.Bills;
using negosuite_api.Contracts.Payments;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class BillPaymentTests
{
    public static readonly string[] BillSorts = { "billNo", "billDate", "dueDate", "supplierName", "supplierTIN", "paymentTermName", "amount", "balance", "notes", "status", "statusName" };
    public static readonly string[] PaymentSorts = { "referenceNo", "referenceDate", "supplierName", "customerName", "customerrName", "payee", "checkNo", "paymentModeName", "paidThroughAccountName", "amount", "balance", "notes", "status", "statusName" };
    public static IEnumerable<object[]> SortCases() => from bill in new[] { true, false }
                                                       from field in bill ? BillSorts : PaymentSorts
                                                       from direction in new[] { "asc", "desc" }
                                                       select new object[] { bill, field, direction };

    [Theory, MemberData(nameof(SortCases))]
    public void All_sorts_search_and_paging_translate_to_sql(bool bill, string field, string direction)
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var sql = bill
            ? BillQuery.Sort(BillQuery.Search(new BillService(db).Query(1, new(), "[1,2]", null, DateTime.Today), "needle"), field, direction).Skip(10).Take(10).ToQueryString()
            : PaymentQuery.Sort(PaymentQuery.Search(new PaymentService(db).Query(1, new(), "[1,2]", null, null), "needle"), field, direction).Skip(10).Take(10).ToQueryString();
        Assert.Contains("JSON_CONTAINS", sql); Assert.Contains("ORDER BY", sql); Assert.Contains("LIMIT", sql); Assert.Contains("OFFSET", sql);
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    [PagePreferenceTests.MySqlTheory, InlineData(true), InlineData(false)]
    public async Task Lists_preserve_contracts_and_filter_before_sorting_and_paging(bool bill)
    {
        await using var f = await Fixture.Start();
        var db = f.Db; var today = DateTime.Today;
        var names = new[] { "Z-%_&", "A", "B", "Draft", "Deleted", "E" };
        for (var i = 0; i < names.Length; i++)
        {
            var state = (short)(i == 3 ? 0 : i == 4 ? -1 : 1);
            var amount = i == 0 ? 100 : i == 1 ? 20 : i == 2 ? 10 : 0;
            var balance = i == 0 ? 100 : i == 2 ? 2 : 0;
            var centers = i == 0 ? "[{\"id\":1},{\"id\":2}]" : i == 1 ? "[{\"id\":1}]" : null;
            if (bill) db.Bills.Add(new()
            {
                UserConfigId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                BillNo = names[i],
                BillDate = i == 5 ? today.AddHours(12) : today,
                Amount = amount,
                Balance = balance,
                Status = state,
                PaymentTermId = f.Term.Id,
                DueDate = today,
                ResponsibilityCenterEntry = centers,
                Notes = i == 0 ? "Special delivery" : null
            });
            else db.Payments.Add(new()
            {
                UserConfigId = f.Company.Id,
                SupplierId = f.Supplier.Id,
                ReferenceNo = names[i],
                ReferenceDate = i == 5 ? today.AddHours(12) : today,
                Amount = amount,
                Balance = balance,
                Status = state,
                IsBillPayment = i < 2,
                PaymentModeId = f.Mode.Id,
                PaidThroughAccountId = f.Account.Id,
                ResponsibilityCenterEntry = centers,
                Notes = i == 0 ? "Special delivery" : null
            });
        }
        if (bill) db.Bills.Add(new() { UserConfigId = f.Other.Id, SupplierId = f.ForeignSupplier.Id, BillNo = "Foreign", BillDate = today, Status = 1 });
        else db.Payments.Add(new() { UserConfigId = f.Other.Id, SupplierId = f.ForeignSupplier.Id, ReferenceNo = "Foreign", ReferenceDate = today, Status = 1 });
        await db.SaveChangesAsync();
        var path = bill ? "/api/bills" : "/api/payments";
        string Url(object criteria) => path + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(criteria));
        var url = Url(new { userConfigId = f.Company.Id });
        var rows = await f.Host.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(4, rows.GetArrayLength()); Assert.Equal(bill ? 15 : 19, rows[0].EnumerateObject().Count());
        if (!bill)
        {
            Assert.True(rows[0].TryGetProperty("customerrName", out _));
            Assert.Equal(2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&isBillPayment=true")).GetArrayLength());
            Assert.Equal(2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&isBillPayment=false")).GetArrayLength());
        }
        var number = bill ? "billNo" : "referenceNo";
        Assert.Equal(new[] { "A", "B", "Z-%_&", "E" }, rows.EnumerateArray().Select(i => i.GetProperty(number).GetString()));
        var firstId = rows[2].GetProperty("id").GetInt32();
        if (!bill) Assert.All(rows.EnumerateArray(), i => Assert.Equal("Posted", i.GetProperty("statusName").GetString()));
        else Assert.Equal(new[] { "Paid", "Partially paid", "Due today", "Due today" }, rows.EnumerateArray().Select(i => i.GetProperty("statusName").GetString()));
        Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, showDeleted = true, status = false }))).GetArrayLength());
        Assert.Equal(3, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, periodStart = today, periodEnd = today }))).GetArrayLength());
        Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, periodStart = today.AddYears(1) }))).GetArrayLength());
        Assert.Equal("Draft", (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&status=0"))[0].GetProperty("statusName").GetString());
        Assert.Equal("Deleted", (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&status=-1"))[0].GetProperty("statusName").GetString());
        Assert.Equal(1, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, referenceNo = "Z-%_&" }))).GetArrayLength());
        Assert.Equal(1, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1, 2" }))).GetArrayLength());
        Assert.Equal(2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1" }))).GetArrayLength());
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, supplierId = f.ForeignSupplier.Id }))).EnumerateArray());
        foreach (var term in new[] { "%_&", "Special delivery" }) Assert.Equal(firstId, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=" + Uri.EscapeDataString(" " + term + " ")))[0].GetProperty("id").GetInt32());
        foreach (var term in new[] { "Fixture vendor", bill ? "Net 30" : "Cash" }) Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=" + Uri.EscapeDataString(term))).GetArrayLength());
        var combined = await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1" }) + "&search=Special&pageNumber=1&pageSize=1");
        Assert.Equal(1, combined.GetProperty("totalCount").GetInt32()); Assert.Equal(firstId, combined.GetProperty("items")[0].GetProperty("id").GetInt32());
        var page = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=2&sortBy=amount&sortDirection=asc");
        Assert.Equal(4, page.GetProperty("totalCount").GetInt32()); Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(20m, page.GetProperty("items")[0].GetProperty("amount").GetDecimal());
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=50")).GetProperty("items").EnumerateArray());
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var field in bill ? BillSorts : PaymentSorts)
            foreach (var direction in new[] { "asc", "desc" })
            {
                var sorted = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + $"&sortBy={field}&sortDirection={direction}");
                var expected = bill ? BillQuery.Sort(rows.Deserialize<BillListItemDto[]>(options).AsQueryable(), field, direction).Select(i => i.Id)
                    : PaymentQuery.Sort(rows.Deserialize<PaymentListItemDto[]>(options).AsQueryable(), field, direction).Select(i => i.Id);
                Assert.Equal(expected, sorted.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
            }
        foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=2147483647&pageSize=200", "&sortBy=unknown", "&sortDirection=up", "&status=2" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(url + suffix)).StatusCode);
        foreach (var criteria in new[] { "", "?criteria=null", "?criteria=%7B%7D", "?criteria=%7Bbroken" }) Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(path + criteria)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(Url(new { userConfigId = f.Company.Id, arrayString = "1); SELECT 1" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync(Url(new { userConfigId = f.Other.Id }))).StatusCode);
        var foreignId = bill ? await db.Bills.Where(r => r.UserConfigId == f.Other.Id).Select(r => r.Id).SingleAsync() : await db.Payments.Where(p => p.UserConfigId == f.Other.Id).Select(p => p.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.DeleteAsync($"{path}/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Host.Client.GetAsync($"{path}/{firstId}")).StatusCode);
        for (var i = 0; i < 205; i++)
            if (bill) db.Bills.Add(new() { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, BillNo = "BULK-" + i, BillDate = today, Status = 1 });
            else db.Payments.Add(new() { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, ReferenceNo = "BULK-" + i, ReferenceDate = today, Status = 1 });
        await db.SaveChangesAsync();
        Assert.Equal(209, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url)).GetArrayLength());
    }

    [MySqlFact]
    public async Task Payments_apply_reverse_nested_allocations_and_reject_foreign_targets_atomically()
    {
        await using var f = await Fixture.Start();
        var invoice = new Bill { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, BillNo = "BILL-TARGET", BillDate = DateTime.Today, DueDate = DateTime.Today, Amount = 100, Balance = 100, Status = 1 };
        var invoiceJournal = new JournalEntry { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = invoice.BillNo, JournalDate = DateTime.Today, Nature = "C", Source = "PU", Amount = 100, Balance = 100, Status = 1 };
        invoice.JournalEntries.Add(invoiceJournal); f.Db.Bills.Add(invoice);
        var foreignTarget = new JournalEntry { UserConfigId = f.Other.Id, AccountId = f.ForeignAccount.Id, ReferenceNo = "FOREIGN", JournalDate = DateTime.Today, Nature = "D", Source = "GJ", Amount = 100, Balance = 100, Status = 1 };
        f.Db.JournalEntries.Add(foreignTarget); await f.Db.SaveChangesAsync();
        const string path = "/api/payments/bill";
        JournalEntry Allocation(decimal amount, int target) => new()
        {
            UserConfigId = f.Company.Id,
            AccountId = f.Account.Id,
            ReferenceNo = "PAY",
            JournalDate = DateTime.Today,
            Amount = amount,
            Balance = amount,
            Nature = "D",
            Source = "PA",
            Status = 1,
            PaymentToJournalEntryId = target
        };
        Payment Payment(string number) => FixtureJournals.Balance(new Payment()
        {
            UserConfigId = f.Company.Id,
            SupplierId = f.Supplier.Id,
            ReferenceNo = number,
            ReferenceDate = DateTime.Today,
            Amount = 100,
            Balance = 60,
            Status = 1,
            PaymentModeId = f.Mode.Id,
            PaidThroughAccountId = f.Account.Id,
            JournalEntries = new List<JournalEntry> { Allocation(40, invoiceJournal.Id) }
        });
        var invalid = Payment("Invalid"); invalid.JournalEntries.Add(Allocation(10, foreignTarget.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(path, invalid)).StatusCode);
        Assert.Equal(100m, await f.Db.Bills.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.False(await f.Db.Payments.AnyAsync(p => p.ReferenceNo == "Invalid"));
        var response = await f.Host.Client.PostAsJsonAsync(path, Payment("PAY-1"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); Assert.NotNull(response.Headers.Location);
        var saved = await response.Content.ReadFromJsonAsync<Payment>();
        Assert.Equal(60m, await f.Db.Bills.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());


        var journal = Assert.Single(FixtureJournals.Business(saved.JournalEntries)); journal.Deleted = true;
        saved.JournalEntries.Add(Allocation(25.55m, invoiceJournal.Id)); saved.Balance = 74.45m;
        FixtureJournals.Balance(saved);
        var updated = await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved);
        Assert.True(updated.StatusCode == HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
        Assert.Equal(74.45m, await f.Db.Bills.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(74.45m, await f.Db.JournalEntries.AsNoTracking().Where(j => j.Id == invoiceJournal.Id).Select(j => j.Balance).SingleAsync());
        // Fetch includes the linked invoice journal; a normal write submits scalar fields only.
        saved = await f.Host.Client.GetFromJsonAsync<Payment>($"/api/payments/{saved.Id}");
        saved.Supplier = null; saved.PaymentMode = null; saved.PaidThroughAccount = null;
        journal = Assert.Single(FixtureJournals.Business(saved.JournalEntries)); journal.Account = null; journal.PaymentToJournalEntry = null;
        journal.PaymentToJournalEntryId = foreignTarget.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        journal.PaymentToJournalEntryId = invoiceJournal.Id; journal.Deleted = true; saved.Balance = 100;
        var unapplied = Allocation(0, invoiceJournal.Id); unapplied.PaymentToJournalEntryId = null; unapplied.Amount = unapplied.Balance = 100;
        saved.JournalEntries.Add(unapplied); FixtureJournals.Balance(saved);
        var reversed = await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved);
        Assert.True(reversed.StatusCode == HttpStatusCode.NoContent, await reversed.Content.ReadAsStringAsync());
        Assert.Equal(100m, await f.Db.Bills.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        Assert.False(await f.Db.JournalEntries.AnyAsync(j => j.PaymentId == saved.Id));
        Assert.Equal(100m, await f.Db.JournalEntries.AsNoTracking().Where(j => j.Id == foreignTarget.Id).Select(j => j.Balance).SingleAsync());
        // Whole-payment deletion also restores the linked bill and journal balances.
        response = await f.Host.Client.PostAsJsonAsync(path, Payment("PAY-2"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        saved = await response.Content.ReadFromJsonAsync<Payment>();
        Assert.Equal(60m, await f.Db.Bills.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        Assert.Equal(100m, await f.Db.Bills.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
    }

    [MySqlFact]
    public async Task Bills_preserve_inventory_costs_touched_updates_child_deletion_and_payment_guard()
    {
        await using var f = await Fixture.Start();
        const string path = "/api/bills";
        Bill NewBill(string number) => FixtureJournals.Balance(new Bill()
        {
            UserConfigId = f.Company.Id,
            SupplierId = f.Supplier.Id,
            BillNo = number,
            BillDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            Amount = 100,
            Balance = 100,
            Status = 1,
            BillDetails = new List<BillDetail> { new() { ItemId = f.Item.Id, Quantity = 10, Rate = 10, Amount = 100, LandedCost = 20, Status = 1 } },
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id,
                ReferenceNo = number, Nature = "C", Source = "BILL", Amount = 100.125m, Balance = 100.125m, Status = 1 } }
        });
        var invalid = NewBill("Invalid"); invalid.BillDetails.First().Quantity = 0;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(path, invalid)).StatusCode);
        Assert.False(await f.Db.Bills.AnyAsync());
        var response = await f.Host.Client.PostAsJsonAsync(path, NewBill("BILL-1"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var saved = await response.Content.ReadFromJsonAsync<Bill>();
        Assert.Equal(100.12m, Assert.Single(FixtureJournals.Business(saved.JournalEntries)).Amount);
        async Task AssertCost(decimal expected)
        {
            var item = await f.Db.Items.AsNoTracking().SingleAsync(i => i.Id == f.Item.Id);
            Assert.Equal(expected, item.AverageCost); Assert.Equal(10m, item.Cost); Assert.Equal(DateTime.Today, item.LastPurchasedDate);
        }
        await AssertCost(12);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(path, NewBill("BILL-1"))).StatusCode);
        var secondResponse = await f.Host.Client.PostAsJsonAsync(path, NewBill("BILL-2"));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await secondResponse.Content.ReadFromJsonAsync<Bill>();
        var detail = Assert.Single(saved.BillDetails); var ownJournals = saved.JournalEntries; var journal = Assert.Single(FixtureJournals.Business(saved.JournalEntries));
        saved.BillDetails = second.BillDetails;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.BillDetails = new List<BillDetail> { detail }; saved.JournalEntries = second.JournalEntries;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.JournalEntries = ownJournals; saved.SupplierId = f.ForeignSupplier.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.SupplierId = f.Supplier.Id; saved.UserConfigId = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.UserConfigId = f.Company.Id;
        detail.Quantity = 20; detail.LandedCost = 40; detail.Touched = true;
        FixtureJournals.Balance(saved);
        var updated = await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved);
        Assert.True(updated.StatusCode == HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
        await AssertCost(12);
        Assert.Equal(20m, await f.Db.BillDetails.Where(d => d.Id == detail.Id).Select(d => d.Quantity).SingleAsync());
        saved = await f.Host.Client.GetFromJsonAsync<Bill>($"{path}/{saved.Id}");
        saved.Balance = 50;
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        Assert.Equal(100m, await f.Db.Bills.AsNoTracking().Where(b => b.Id == saved.Id).Select(b => b.Balance).SingleAsync());
        await f.Db.Bills.Where(b => b.Id == saved.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Balance, 50));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        await f.Db.Bills.Where(b => b.Id == saved.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Balance, 100));
        saved = await f.Host.Client.GetFromJsonAsync<Bill>($"{path}/{saved.Id}");
        saved.Status = 0;
        foreach (var line in saved.BillDetails) line.Deleted = true;
        foreach (var entry in saved.JournalEntries) entry.Deleted = true;
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        Assert.False(await f.Db.BillDetails.AnyAsync(d => d.Id == detail.Id)); Assert.False(await f.Db.JournalEntries.AnyAsync(j => j.Id == journal.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        // Removing the remaining stock must not divide by zero in average-cost calculation.
        var deleted = await f.Host.Client.DeleteAsync($"{path}/{second.Id}");
        Assert.True(deleted.StatusCode == HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        Assert.False(await f.Db.BillDetails.AnyAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{second.Id}")).StatusCode);
    }

    [MySqlFact]
    public async Task General_payments_keep_write_contract_and_missing_detail_returns_404()
    {
        await using var f = await Fixture.Start();
        const string path = "/api/payments";
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync(path + "/99999")).StatusCode);
        Payment NewPayment(string number) => FixtureJournals.Balance(new Payment()
        {
            UserConfigId = f.Company.Id,
            SupplierId = f.Supplier.Id,
            ReferenceNo = number,
            ReferenceDate = DateTime.Today,
            Amount = 10,
            Balance = 0,
            Status = 1,
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id,
                ReferenceNo = number, Nature = "D", Source = "PAY", Amount = 10.125m, Status = 1 } }
        });
        var response = await f.Host.Client.PostAsJsonAsync(path, NewPayment("OTHER-1"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var saved = await response.Content.ReadFromJsonAsync<Payment>();
        Assert.Equal(10.125m, Assert.Single(FixtureJournals.Business(saved.JournalEntries)).Amount);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(path, NewPayment("OTHER-1"))).StatusCode);
        saved.Notes = "Updated";
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        Assert.Equal("Updated", (await f.Host.Client.GetFromJsonAsync<Payment>($"{path}/{saved.Id}")).Notes);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        Assert.False(await f.Db.JournalEntries.AnyAsync(j => j.PaymentId == saved.Id));
    }

    [PagePreferenceTests.MySqlTheory, InlineData(true), InlineData(false)]
    public async Task Dto_writes_ignore_nested_master_objects_and_unrelated_journal_parents(bool bill)
    {
        await using var f = await Fixture.Start();
        var path = bill ? "/api/bills" : "/api/payments";
        var journal = new JournalEntry { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = "DTO-1",
            Nature = "D", Source = "TEST", Amount = 10, Balance = 10, Status = 1,
            SalesInvoiceId = 999999, GeneralJournalId = 999999,
            Account = new Account { UserConfigId = f.Company.Id, Code = "INJECT", Name = "Unwanted account" } };
        var supplier = new Supplier { UserConfigId = f.Company.Id, Name = "Unwanted supplier" };
        object request = bill ? new Bill { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, Supplier = supplier,
            BillNo = "DTO-1", BillDate = DateTime.Today, DueDate = DateTime.Today, Status = 1, Amount = 10, Balance = 10,
            BillDetails = new List<BillDetail> { new() { ItemId = f.Item.Id, Quantity = 1, Rate = 10, Amount = 10, Status = 1,
                Item = new Item { UserConfigId = f.Company.Id, Name = "Unwanted item" } } }, JournalEntries = new List<JournalEntry> { journal } }
            : new Payment { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, Supplier = supplier, ReferenceNo = "DTO-1",
                ReferenceDate = DateTime.Today, Status = 1, Amount = 10, Balance = 10, JournalEntries = new List<JournalEntry> { journal } };
        FixtureJournals.Balance(request);
        var response = await f.Host.Client.PostAsJsonAsync(path, request);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();
        var savedJournal = created.GetProperty("journalEntries")[0];
        Assert.Equal(JsonValueKind.Null, savedJournal.GetProperty("salesInvoiceId").ValueKind);
        Assert.Equal(JsonValueKind.Null, savedJournal.GetProperty("generalJournalId").ValueKind);
        Assert.Equal(id, savedJournal.GetProperty(bill ? "billId" : "paymentId").GetInt32());
        // Resubmit the complete detail payload, as a legacy client may do.
        var detail = System.Text.Json.Nodes.JsonNode.Parse(await f.Host.Client.GetStringAsync($"{path}/{id}"));
        detail["supplier"]["name"] = "Overposted supplier";
        detail["journalEntries"][0]["account"]["name"] = "Overposted account";
        detail["notes"] = "DTO round trip";
        if (bill) detail["billDetails"][0]["item"]["name"] = "Overposted item";
        var update = await f.Host.Client.PutAsJsonAsync($"{path}/{id}", detail);
        Assert.True(update.StatusCode == HttpStatusCode.NoContent, await update.Content.ReadAsStringAsync());
        Assert.Equal("DTO round trip", (await f.Host.Client.GetFromJsonAsync<JsonElement>($"{path}/{id}")).GetProperty("notes").GetString());
        Assert.Equal("Fixture vendor", await f.Db.Suppliers.AsNoTracking().Where(s => s.Id == f.Supplier.Id).Select(s => s.Name).SingleAsync());
        Assert.Equal("Receivables", await f.Db.Accounts.AsNoTracking().Where(a => a.Id == f.Account.Id).Select(a => a.Name).SingleAsync());
        Assert.Equal("Test item", await f.Db.Items.AsNoTracking().Where(i => i.Id == f.Item.Id).Select(i => i.Name).SingleAsync());
        Assert.Equal(2, await f.Db.Suppliers.CountAsync()); Assert.Equal(2, await f.Db.Accounts.CountAsync()); Assert.Equal(1, await f.Db.Items.CountAsync());
        var invalid = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        invalid[bill ? "billNo" : "referenceNo"] = "";
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(path, invalid)).StatusCode);
        invalid[bill ? "billNo" : "referenceNo"] = "INVALID"; invalid["journalEntries"] = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(path, invalid)).StatusCode);
        invalid["journalEntries"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode)null);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(path, invalid)).StatusCode);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public ApiHost Host; private IServiceScope scope; public negosuiteContext Db;
        public Config Company, Other; public Supplier Supplier, ForeignSupplier; public Account Account, ForeignAccount; public Item Item; public PaymentMode Mode; public PaymentTerm Term;
        public static async Task<Fixture> Start()
        {
            var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
            Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
            connection.Database = "negosuite_billpayments_" + Guid.NewGuid().ToString("N");
            var f = new Fixture { Host = new ApiHost(connection.ConnectionString) };
            f.scope = f.Host.Server.Services.CreateScope(); f.Db = f.scope.ServiceProvider.GetRequiredService<negosuiteContext>();
            try
            {
                await f.Db.Database.EnsureCreatedAsync();
                await f.Db.Database.ExecuteSqlRawAsync("CREATE VIEW inventorytransaction AS SELECT b.UserConfigId, d.ItemId, i.Name AS ItemName, i.Cost AS ItemCost, i.AverageCost, i.ReorderPoint AS ItemReorderPoint, d.Quantity AS QuantityIn, CAST(0 AS DECIMAL(20,4)) AS QuantityOut, b.Status FROM bill b JOIN billdetail d ON d.BillId = b.Id JOIN item i ON i.Id = d.ItemId");
                f.Company = new Config { CompanyName = "Collections fixture", Uuid = Guid.NewGuid().ToString() }; f.Other = new Config { CompanyName = "Other fixture", Uuid = Guid.NewGuid().ToString() };
                f.Db.Configs.AddRange(f.Company, f.Other); await f.Db.SaveChangesAsync();
                f.Supplier = new Supplier { UserConfigId = f.Company.Id, Name = "Fixture vendor", Tin = "TIN-123", Status = true };
                f.ForeignSupplier = new Supplier { UserConfigId = f.Other.Id, Name = "Foreign vendor", Status = true };
                f.Account = new Account { UserConfigId = f.Company.Id, Name = "Receivables", Code = "1200", Category = new AccountCategory { UserConfigId = f.Company.Id, Name = "Assets", Type = "A" } };
                f.ForeignAccount = new Account { UserConfigId = f.Other.Id, Name = "Foreign account", Code = "foreign", Category = new AccountCategory { UserConfigId = f.Other.Id, Name = "Assets", Type = "A" } };
                f.Item = new Item { UserConfigId = f.Company.Id, Name = "Test item", Status = true }; f.Mode = new PaymentMode { Name = "Cash", IsActive = true };
                f.Term = new PaymentTerm { Code = "N30", Name = "Net 30", Days = 30, IsActive = true }; f.Db.PaymentTerms.Add(f.Term);
                f.Db.Suppliers.AddRange(f.Supplier, f.ForeignSupplier); f.Db.Accounts.AddRange(f.Account, f.ForeignAccount); f.Db.Items.Add(f.Item); f.Db.PaymentModes.Add(f.Mode); await f.Db.SaveChangesAsync();
                f.Company.APTradeAccountId = f.Account.Id; await f.Db.SaveChangesAsync();
                f.Host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ApiHost.MemberTokenAsync(f.Db, f.Company.Id)); f.Host.Client.DefaultRequestHeaders.Add("configUuid", f.Company.Uuid);
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { try { await Db.Database.EnsureDeletedAsync(); } finally { scope.Dispose(); Host.Dispose(); } }
    }
}
