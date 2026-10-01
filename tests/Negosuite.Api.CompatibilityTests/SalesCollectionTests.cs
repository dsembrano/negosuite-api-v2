using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.SalesReceipts;
using negosuite_api.Contracts.SalesInvoicePayments;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SalesCollectionTests
{
    public static readonly string[] ReceiptSorts = { "receiptNo", "receiptDate", "customerName", "customerTIN", "billingAddress", "billingContactName", "billingContactEmail", "shippingAddress", "shippingContactName", "shippingContactEmail", "amount", "balance", "paymentModeName", "notes", "status", "createdDate", "statusName" };
    public static readonly string[] PaymentSorts = { "referenceNo", "referenceDate", "customerName", "paymentModeName", "depositToAccountName", "amount", "balance", "notes", "status", "statusName" };
    public static IEnumerable<object[]> SortCases() => from receipt in new[] { true, false }
        from field in receipt ? ReceiptSorts : PaymentSorts from direction in new[] { "asc", "desc" } select new object[] { receipt, field, direction };

    [Theory, MemberData(nameof(SortCases))]
    public void All_sorts_search_and_paging_translate_to_sql(bool receipt, string field, string direction)
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var sql = receipt
            ? SalesReceiptQuery.Sort(SalesReceiptQuery.Search(new SalesReceiptService(db).Query(1, new(), "[1,2]", null), "needle"), field, direction).Skip(10).Take(10).ToQueryString()
            : SalesInvoicePaymentQuery.Sort(SalesInvoicePaymentQuery.Search(new SalesInvoicePaymentService(db).Query(1, new(), "[1,2]", null), "needle"), field, direction).Skip(10).Take(10).ToQueryString();
        Assert.Contains("JSON_CONTAINS", sql); Assert.Contains("ORDER BY", sql); Assert.Contains("LIMIT", sql); Assert.Contains("OFFSET", sql);
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    [PagePreferenceTests.MySqlTheory, InlineData(true), InlineData(false)]
    public async Task Lists_preserve_contracts_and_filter_before_sorting_and_paging(bool receipt)
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
            if (receipt) db.SalesReceipts.Add(new() { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, ReceiptNo = names[i], ReceiptDate = i == 5 ? today.AddHours(12) : today,
                Amount = amount, Balance = balance, Status = state, PaymentModeId = f.Mode.Id, ResponsibilityCenterEntry = centers, IsPOS = i < 2, CreatedDate = i == 0 ? today : null,
                BillingContactEmail = i == 0 ? "billing@example.test" : null, Notes = i == 0 ? "Special delivery" : null });
            else db.SalesInvoicePayments.Add(new() { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, ReferenceNo = names[i], ReferenceDate = i == 5 ? today.AddHours(12) : today,
                Amount = amount, Balance = balance, Status = state, PaymentModeId = f.Mode.Id, DepositToAccountId = f.Account.Id, ResponsibilityCenterEntry = centers, Notes = i == 0 ? "Special delivery" : null });
        }
        if (receipt) db.SalesReceipts.Add(new() { UserConfigId = f.Other.Id, CustomerId = f.ForeignCustomer.Id, ReceiptNo = "Foreign", ReceiptDate = today, Status = 1 });
        else db.SalesInvoicePayments.Add(new() { UserConfigId = f.Other.Id, CustomerId = f.ForeignCustomer.Id, ReferenceNo = "Foreign", ReferenceDate = today, Status = 1 });
        await db.SaveChangesAsync();
        var path = receipt ? "/api/sales-receipts" : "/api/sales-invoice-payments";
        string Url(object criteria) => path + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(criteria));
        var url = Url(new { userConfigId = f.Company.Id });
        var rows = await f.Host.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(4, rows.GetArrayLength()); Assert.Equal(receipt ? 21 : 15, rows[0].EnumerateObject().Count());
        var number = receipt ? "receiptNo" : "referenceNo";
        Assert.Equal(new[] { "A", "B", "Z-%_&", "E" }, rows.EnumerateArray().Select(i => i.GetProperty(number).GetString()));
        var firstId = rows[2].GetProperty("id").GetInt32();
        if (receipt) Assert.All(rows.EnumerateArray(), i => Assert.Equal("Posted", i.GetProperty("statusName").GetString()));
        else Assert.Equal(new[] { "Fully applied", "Partially applied", "Unapplied", "Unapplied" }, rows.EnumerateArray().Select(i => i.GetProperty("statusName").GetString()));
        Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, showDeleted = true, status = false }))).GetArrayLength());
        Assert.Equal(3, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, periodStart = today, periodEnd = today }))).GetArrayLength());
        Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, periodStart = today.AddYears(1) }))).GetArrayLength());
        Assert.Equal("Draft", (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&status=0"))[0].GetProperty("statusName").GetString());
        Assert.Equal("Deleted", (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&status=-1"))[0].GetProperty("statusName").GetString());
        Assert.Equal(1, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, referenceNo = "Z-%_&" }))).GetArrayLength());
        Assert.Equal(1, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1, 2" }))).GetArrayLength());
        Assert.Equal(2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1" }))).GetArrayLength());
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, customerId = f.ForeignCustomer.Id }))).EnumerateArray());
        if (receipt)
        {
            Assert.Equal(2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, isPOS = true }))).GetArrayLength());
            Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, isPOS = false }))).GetArrayLength());
        }
        foreach (var term in new[] { "%_&", "Special delivery" }) Assert.Equal(firstId, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=" + Uri.EscapeDataString(" " + term + " ")))[0].GetProperty("id").GetInt32());
        foreach (var term in new[] { "Fixture buyer", "Cash" }) Assert.Equal(4, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=" + Uri.EscapeDataString(term))).GetArrayLength());
        var combined = await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1" }) + "&search=Special&pageNumber=1&pageSize=1");
        Assert.Equal(1, combined.GetProperty("totalCount").GetInt32()); Assert.Equal(firstId, combined.GetProperty("items")[0].GetProperty("id").GetInt32());
        var page = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=2&sortBy=amount&sortDirection=asc");
        Assert.Equal(4, page.GetProperty("totalCount").GetInt32()); Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(20m, page.GetProperty("items")[0].GetProperty("amount").GetDecimal());
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=50")).GetProperty("items").EnumerateArray());
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var field in receipt ? ReceiptSorts : PaymentSorts)
        foreach (var direction in new[] { "asc", "desc" })
        {
            var sorted = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + $"&sortBy={field}&sortDirection={direction}");
            var expected = receipt ? SalesReceiptQuery.Sort(rows.Deserialize<SalesReceiptListItemDto[]>(options).AsQueryable(), field, direction).Select(i => i.Id)
                : SalesInvoicePaymentQuery.Sort(rows.Deserialize<SalesInvoicePaymentListItemDto[]>(options).AsQueryable(), field, direction).Select(i => i.Id);
            Assert.Equal(expected, sorted.EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
        }
        foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=2147483647&pageSize=200", "&sortBy=unknown", "&sortDirection=up", "&status=2" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(url + suffix)).StatusCode);
        foreach (var criteria in new[] { "", "?criteria=null", "?criteria=%7B%7D", "?criteria=%7Bbroken" }) Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(path + criteria)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(Url(new { userConfigId = f.Company.Id, arrayString = "1); SELECT 1" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync(Url(new { userConfigId = f.Other.Id }))).StatusCode);
        var foreignId = receipt ? await db.SalesReceipts.Where(r => r.UserConfigId == f.Other.Id).Select(r => r.Id).SingleAsync() : await db.SalesInvoicePayments.Where(p => p.UserConfigId == f.Other.Id).Select(p => p.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.DeleteAsync($"{path}/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Host.Client.GetAsync($"{path}/{firstId}")).StatusCode);
        for (var i = 0; i < 205; i++)
            if (receipt) db.SalesReceipts.Add(new() { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, ReceiptNo = "BULK-" + i, ReceiptDate = today, Status = 1 });
            else db.SalesInvoicePayments.Add(new() { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, ReferenceNo = "BULK-" + i, ReferenceDate = today, Status = 1 });
        await db.SaveChangesAsync();
        Assert.Equal(209, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url)).GetArrayLength());
    }

    [MySqlFact]
    public async Task Receipts_preserve_writes_child_deletion_and_separate_sr_pos_numbering()
    {
        await using var f = await Fixture.Start();
        const string path = "/api/sales-receipts";
        SalesReceipt Receipt(string number) => new() { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, ReceiptNo = number, ReceiptDate = DateTime.Today,
            PaymentModeId = f.Mode.Id, DepositToAccountId = f.Account.Id, Amount = 100, Balance = 0, Status = 1,
            SalesReceiptDetails = new List<SalesReceiptDetail> { new() { ItemId = f.Item.Id, Quantity = 1, Rate = 100, Amount = 100, Status = 1 } },
            JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, Amount = 100.125m, Balance = 100.125m, Nature = "C", Source = "SR", Status = 1 } } };
        var response = await f.Host.Client.PostAsJsonAsync(path, Receipt("SR-1"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); Assert.NotNull(response.Headers.Location);
        var saved = await response.Content.ReadFromJsonAsync<SalesReceipt>();
        Assert.Equal(100.12m, Assert.Single(saved.JournalEntries).Amount); Assert.Equal(saved.ReceiptNo, Assert.Single(saved.JournalEntries).ReferenceNo);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(path, Receipt("SR-1"))).StatusCode);
        var other = await (await f.Host.Client.PostAsJsonAsync(path, Receipt("SR-2"))).Content.ReadFromJsonAsync<SalesReceipt>();
        var ownDetail = Assert.Single(saved.SalesReceiptDetails); var ownJournal = Assert.Single(saved.JournalEntries);
        saved.SalesReceiptDetails = other.SalesReceiptDetails;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.SalesReceiptDetails = new List<SalesReceiptDetail> { ownDetail }; saved.JournalEntries = other.JournalEntries;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.JournalEntries = new List<JournalEntry> { ownJournal }; saved.CustomerId = f.ForeignCustomer.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.CustomerId = f.Customer.Id; saved.UserConfigId = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        saved.UserConfigId = f.Company.Id; ownDetail.Deleted = true; ownJournal.Deleted = true;
        saved.SalesReceiptDetails.Add(new() { ItemId = f.Item.Id, Quantity = 2, Rate = 50, Amount = 100, Status = 1 });
        saved.JournalEntries.Add(new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = saved.ReceiptNo, Amount = 50.555m, Balance = 50.555m, Nature = "C", Source = "SR", Status = 1 });
        var update = await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved);
        Assert.True(update.StatusCode == HttpStatusCode.NoContent, await update.Content.ReadAsStringAsync());
        var detail = await f.Host.Client.GetFromJsonAsync<SalesReceipt>($"{path}/{saved.Id}");
        Assert.Equal(2m, Assert.Single(detail.SalesReceiptDetails).Quantity); Assert.Equal(50.56m, Assert.Single(detail.JournalEntries).Amount);
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        Assert.False(await f.Db.SalesReceiptDetails.AnyAsync(d => d.SalesReceiptId == saved.Id)); Assert.False(await f.Db.JournalEntries.AnyAsync(j => j.SalesReceiptId == saved.Id));
        f.Company.AutoReferenceNoConfig = "{\"autoSRReferenceNo\":true,\"autoSRReferenceNoFormat\":\"########\",\"autoSRReferenceNoPrefix\":\"SR-\",\"autoPOSReferenceNoFormat\":\"########\",\"autoPOSReferenceNoPrefix\":\"POS-\"}";
        await f.Db.SaveChangesAsync();
        var automatic = await (await f.Host.Client.PostAsJsonAsync(path, Receipt("ignored"))).Content.ReadFromJsonAsync<SalesReceipt>();
        Assert.Equal("SR-00000001", automatic.ReceiptNo);
        var pos = Receipt("ignored-pos"); pos.IsPOS = true;
        var autoPos = await (await f.Host.Client.PostAsJsonAsync(path, pos)).Content.ReadFromJsonAsync<SalesReceipt>();
        Assert.Equal("POS-00000001", autoPos.ReceiptNo);
        Assert.Equal(2, await f.Db.TransactionSequences.CountAsync(s => s.UserConfigId == f.Company.Id && s.LastSequence == 1));
    }

    [MySqlFact]
    public async Task Payments_apply_reverse_nested_allocations_and_reject_foreign_targets_atomically()
    {
        await using var f = await Fixture.Start();
        var invoice = new SalesInvoice { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, InvoiceNo = "SI-TARGET", InvoiceDate = DateTime.Today, DueDate = DateTime.Today, Amount = 100, Balance = 100, Status = 1 };
        var invoiceJournal = new JournalEntry { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = invoice.InvoiceNo, JournalDate = DateTime.Today, Nature = "D", Source = "SI", Amount = 100, Balance = 100, Status = 1 };
        invoice.JournalEntries.Add(invoiceJournal); f.Db.SalesInvoices.Add(invoice);
        var foreignTarget = new JournalEntry { UserConfigId = f.Other.Id, AccountId = f.ForeignAccount.Id, ReferenceNo = "FOREIGN", JournalDate = DateTime.Today, Nature = "D", Source = "GJ", Amount = 100, Balance = 100, Status = 1 };
        f.Db.JournalEntries.Add(foreignTarget); await f.Db.SaveChangesAsync();
        const string path = "/api/sales-invoice-payments";
        JournalEntry Allocation(decimal amount, int target) => new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = "PAY", JournalDate = DateTime.Today,
            Amount = amount, Balance = amount, Nature = "C", Source = "SIP", Status = 1, PaymentToJournalEntryId = target };
        SalesInvoicePayment Payment(string number) => new() { UserConfigId = f.Company.Id, CustomerId = f.Customer.Id, ReferenceNo = number, ReferenceDate = DateTime.Today,
            Amount = 100, Balance = 60, Status = 1, PaymentModeId = f.Mode.Id, DepositToAccountId = f.Account.Id,
            JournalEntries = new List<JournalEntry> { Allocation(40, invoiceJournal.Id) } };
        var invalid = Payment("Invalid"); invalid.JournalEntries.Add(Allocation(10, foreignTarget.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync(path, invalid)).StatusCode);
        Assert.Equal(100m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.False(await f.Db.SalesInvoicePayments.AnyAsync(p => p.ReferenceNo == "Invalid"));
        var response = await f.Host.Client.PostAsJsonAsync(path, Payment("PAY-1"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); Assert.NotNull(response.Headers.Location);
        var saved = await response.Content.ReadFromJsonAsync<SalesInvoicePayment>();
        Assert.Equal(60m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(path, Payment("PAY-1"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        var journal = Assert.Single(saved.JournalEntries); journal.Deleted = true;
        saved.JournalEntries.Add(Allocation(25.555m, invoiceJournal.Id)); saved.Balance = 74.44m;
        var updated = await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved);
        Assert.True(updated.StatusCode == HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
        Assert.Equal(74.44m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(74.44m, await f.Db.JournalEntries.AsNoTracking().Where(j => j.Id == invoiceJournal.Id).Select(j => j.Balance).SingleAsync());
        // Fetch includes the linked invoice journal; a normal write submits scalar fields only.
        saved = await f.Host.Client.GetFromJsonAsync<SalesInvoicePayment>($"{path}/{saved.Id}");
        saved.Customer = null; saved.PaymentMode = null; saved.DepositToAccount = null;
        journal = Assert.Single(saved.JournalEntries); journal.Account = null; journal.PaymentToJournalEntry = null;
        journal.PaymentToJournalEntryId = foreignTarget.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved)).StatusCode);
        journal.PaymentToJournalEntryId = invoiceJournal.Id; journal.Deleted = true; saved.Balance = 100;
        var reversed = await f.Host.Client.PutAsJsonAsync($"{path}/{saved.Id}", saved);
        Assert.True(reversed.StatusCode == HttpStatusCode.NoContent, await reversed.Content.ReadAsStringAsync());
        Assert.Equal(100m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync($"{path}/{saved.Id}")).StatusCode);
        Assert.False(await f.Db.JournalEntries.AnyAsync(j => j.SalesInvoicePaymentId == saved.Id));
        Assert.Equal(100m, await f.Db.JournalEntries.AsNoTracking().Where(j => j.Id == foreignTarget.Id).Select(j => j.Balance).SingleAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApiHost Host; private IServiceScope scope; public negosuiteContext Db;
        public Config Company, Other; public Customer Customer, ForeignCustomer; public Account Account, ForeignAccount; public Item Item; public PaymentMode Mode;
        public static async Task<Fixture> Start()
        {
            var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
            Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
            connection.Database = "negosuite_collections_" + Guid.NewGuid().ToString("N");
            var f = new Fixture { Host = new ApiHost(connection.ConnectionString) };
            f.scope = f.Host.Server.Services.CreateScope(); f.Db = f.scope.ServiceProvider.GetRequiredService<negosuiteContext>();
            try
            {
                await f.Db.Database.EnsureCreatedAsync();
                f.Company = new Config { CompanyName = "Collections fixture", Uuid = Guid.NewGuid().ToString() }; f.Other = new Config { CompanyName = "Other fixture", Uuid = Guid.NewGuid().ToString() };
                f.Db.Configs.AddRange(f.Company, f.Other); await f.Db.SaveChangesAsync();
                f.Customer = new Customer { UserConfigId = f.Company.Id, Name = "Fixture buyer", Tin = "TIN-123", Status = true };
                f.ForeignCustomer = new Customer { UserConfigId = f.Other.Id, Name = "Foreign buyer", Status = true };
                f.Account = new Account { UserConfigId = f.Company.Id, Name = "Receivables", Code = "1200", Category = new AccountCategory { UserConfigId = f.Company.Id, Name = "Assets", Type = "A" } };
                f.ForeignAccount = new Account { UserConfigId = f.Other.Id, Name = "Foreign account", Code = "foreign", Category = new AccountCategory { UserConfigId = f.Other.Id, Name = "Assets", Type = "A" } };
                f.Item = new Item { UserConfigId = f.Company.Id, Name = "Test item", Status = true }; f.Mode = new PaymentMode { Name = "Cash", IsActive = true };
                f.Db.Customers.AddRange(f.Customer, f.ForeignCustomer); f.Db.Accounts.AddRange(f.Account, f.ForeignAccount); f.Db.Items.Add(f.Item); f.Db.PaymentModes.Add(f.Mode); await f.Db.SaveChangesAsync();
                f.Company.ARTradeAccountId = f.Account.Id; await f.Db.SaveChangesAsync();
                f.Host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.Token()); f.Host.Client.DefaultRequestHeaders.Add("configUuid", f.Company.Uuid);
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { try { await Db.Database.EnsureDeletedAsync(); } finally { scope.Dispose(); Host.Dispose(); } }
    }
}
