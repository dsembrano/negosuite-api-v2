using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Xunit;
using Fixture = Negosuite.Api.CompatibilityTests.BillPaymentTests.Fixture;

namespace Negosuite.Api.CompatibilityTests;

public class OtherTransactionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static IEnumerable<object[]> Modules() => new[] { "general-journals", "receiving-reports", "stock-issuances", "stock-transfers", "inventory-adjustments" }.Select(p => new object[] { p });

    private static async Task<(InventoryLocation Location, Customer Customer, Account Receivable)> Seed(Fixture f)
    {
        var location = new InventoryLocation { UserConfigId = f.Company.Id, Name = "Warehouse", Code = "WH", Status = true };
        var customer = new Customer { UserConfigId = f.Company.Id, Name = "Buyer", Status = true };
        var account = new Account { UserConfigId = f.Company.Id, Code = "AR", Name = "AR", CategoryId = f.Account.CategoryId };
        f.Db.InventoryLocations.Add(location); f.Db.Customers.Add(customer); f.Db.Accounts.Add(account);
        await f.Db.SaveChangesAsync(); f.Company.ARTradeAccountId = account.Id; await f.Db.SaveChangesAsync();
        return (location, customer, account);
    }

    [MySqlFact]
    public async Task General_journals_apply_and_reverse_both_ar_and_ap_and_reject_foreign_targets_atomically()
    {
        await using var f = await Fixture.Start(); var seed = await Seed(f);
        var invoice = new SalesInvoice { UserConfigId = f.Company.Id, CustomerId = seed.Customer.Id, InvoiceNo = "TARGET-SI", InvoiceDate = DateTime.Today, DueDate = DateTime.Today, Amount = 100, Balance = 100, Status = 1 };
        var invoiceEntry = new JournalEntry { UserConfigId = f.Company.Id, AccountId = seed.Receivable.Id, ReferenceNo = "TARGET-SI", JournalDate = DateTime.Today, Source = "SI", Nature = "D", Amount = 100, Balance = 100, Status = 1 };
        invoice.JournalEntries.Add(invoiceEntry); f.Db.SalesInvoices.Add(invoice);
        var bill = new Bill { UserConfigId = f.Company.Id, SupplierId = f.Supplier.Id, BillNo = "TARGET-PU", BillDate = DateTime.Today, DueDate = DateTime.Today, Amount = 100, Balance = 100, Status = 1 };
        var billEntry = new JournalEntry { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = "TARGET-PU", JournalDate = DateTime.Today, Source = "PU", Nature = "C", Amount = 100, Balance = 100, Status = 1 };
        bill.JournalEntries.Add(billEntry); f.Db.Bills.Add(bill);
        var foreign = new JournalEntry { UserConfigId = f.Other.Id, AccountId = f.ForeignAccount.Id, ReferenceNo = "FOREIGN", JournalDate = DateTime.Today, Source = "GJ", Nature = "D", Amount = 100, Balance = 100, Status = 1 };
        f.Db.JournalEntries.Add(foreign); await f.Db.SaveChangesAsync();
        GeneralJournal NewJournal(string number) => new() { UserConfigId = f.Company.Id, ReferenceNo = number, ReferenceDate = DateTime.Today, Status = 1,
            JournalEntries = new List<JournalEntry> {
                new() { UserConfigId = f.Company.Id, AccountId = seed.Receivable.Id, ReferenceNo = number, Nature = "C", Source = "GJ", Amount = 40, Status = 1, PaymentToJournalEntryId = invoiceEntry.Id },
                new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = number, Nature = "D", Source = "GJ", Amount = 30, Status = 1, PaymentToJournalEntryId = billEntry.Id } } };
        var invalid = NewJournal("INVALID"); invalid.JournalEntries.Last().PaymentToJournalEntryId = foreign.Id;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PostAsJsonAsync("/api/general-journals", invalid)).StatusCode);
        Assert.Equal(100m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.False(await f.Db.GeneralJournals.AnyAsync());
        var response = await f.Host.Client.PostAsJsonAsync("/api/general-journals", NewJournal("ALLOCATE"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var saved = await response.Content.ReadFromJsonAsync<GeneralJournal>();
        Assert.Equal(60m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(70m, await f.Db.Bills.AsNoTracking().Where(b => b.Id == bill.Id).Select(b => b.Balance).SingleAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await f.Host.Client.DeleteAsync("/api/general-journals/" + saved.Id)).StatusCode);
        Assert.Equal(100m, await f.Db.SalesInvoices.AsNoTracking().Where(i => i.Id == invoice.Id).Select(i => i.Balance).SingleAsync());
        Assert.Equal(100m, await f.Db.Bills.AsNoTracking().Where(b => b.Id == bill.Id).Select(b => b.Balance).SingleAsync());
        var protectedJournal = NewJournal("PROTECTED"); protectedJournal.JournalEntries = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = seed.Receivable.Id, ReferenceNo = "PROTECTED", Nature = "D", Source = "GJ", Amount = 100, Balance = 60, Status = 1 } };
        saved = await (await f.Host.Client.PostAsJsonAsync("/api/general-journals", protectedJournal)).Content.ReadFromJsonAsync<GeneralJournal>();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.DeleteAsync("/api/general-journals/" + saved.Id)).StatusCode);
    }

    [MySqlFact]
    public async Task Journal_lookups_keep_shapes_and_scope_filters_paging_and_authentication()
    {
        await using var f = await Fixture.Start(); var seed = await Seed(f);
        foreach (var (reference, nature, account, source) in new[] { ("INV", "D", seed.Receivable.Id, "SI"), ("BILL", "C", f.Account.Id, "PU"), ("CREDIT", "C", seed.Receivable.Id, "PR") })
            f.Db.JournalEntries.Add(new() { UserConfigId = f.Company.Id, AccountId = account, CustomerId = seed.Customer.Id, SupplierId = f.Supplier.Id,
                ReferenceNo = reference, JournalDate = DateTime.Today, DueDate = DateTime.Today.AddDays(30), Source = source, Nature = nature, Amount = 100, Balance = 30, Status = 1 });
        var foreign = new JournalEntry { UserConfigId = f.Other.Id, AccountId = seed.Receivable.Id, ReferenceNo = "Foreign", JournalDate = DateTime.Today, Source = "PR", Nature = "C", Amount = 100, Balance = 100, Status = 1 };
        f.Db.JournalEntries.Add(foreign); await f.Db.SaveChangesAsync();
        foreach (var kind in new[] { "unpaid-invoices", "unpaid-bills", "unapplied-ar-credits" })
        {
            var criteria = Uri.EscapeDataString(JsonSerializer.Serialize(new { accountId = kind == "unpaid-bills" ? f.Account.Id : seed.Receivable.Id, customerId = seed.Customer.Id, supplierId = f.Supplier.Id }));
            var url = "/api/journal-entries/" + kind + "?criteria=" + criteria;
            var rows = await f.Host.Client.GetFromJsonAsync<JsonElement>(url); Assert.Single(rows.EnumerateArray());
            Assert.Equal(kind == "unapplied-ar-credits" ? 10 : 6, rows[0].EnumerateObject().Count());
            var page = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&sortBy=balance&sortDirection=desc");
            Assert.Equal(1, page.GetProperty("totalCount").GetInt32()); Assert.Single(page.GetProperty("items").EnumerateArray());
            var fields = kind == "unpaid-invoices" ? new[] { "invoiceNo", "invoiceDate", "dueDate", "amount", "balance" } : kind == "unpaid-bills" ? new[] { "billNo", "billDate", "dueDate", "amount", "balance" } : new[] { "referenceNo", "referenceDate", "dueDate", "amount", "balance", "customerName", "source", "sourceName" };
            foreach (var field in fields) foreach (var direction in new[] { "asc", "desc" })
                Assert.Single((await f.Host.Client.GetFromJsonAsync<JsonElement>(url + $"&sortBy={field}&sortDirection={direction}")).EnumerateArray());
            Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=missing")).EnumerateArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(url + "&sortBy=accountId")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(url + "&pageSize=1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync("/api/journal-entries/" + kind + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(new { userConfigId = f.Other.Id })))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync("/api/journal-entries/unapplied-ar-credits/" + foreign.Id)).StatusCode);
        var credit = await f.Db.JournalEntries.Where(j => j.ReferenceNo == "CREDIT").Select(j => j.Id).SingleAsync();
        var detail = await f.Host.Client.GetFromJsonAsync<JsonElement>("/api/journal-entries/unapplied-ar-credits/" + credit);
        Assert.Equal("Buyer", detail.GetProperty("customer").GetProperty("name").GetString()); Assert.Equal("Payment Received", detail.GetProperty("sourceName").GetString());
        f.Host.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync("/api/journal-entries/unapplied-ar-credits?criteria=%7B%7D")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Host.Client.GetAsync("/api/journal-entries/unapplied-ar-credits/" + credit)).StatusCode);
    }

    private static object Document(string module, Fixture f, int location, string reference, short status = 1)
    {
        var journals = new List<JournalEntry> { new() { UserConfigId = f.Company.Id, AccountId = f.Account.Id, ReferenceNo = reference,
            JournalDate = DateTime.Today, Nature = "D", Source = "TEST", Amount = 10.125m, Balance = 10.125m, Status = status } };
        return module switch
        {
            "general-journals" => new GeneralJournal { UserConfigId = f.Company.Id, ReferenceNo = reference, ReferenceDate = DateTime.Today, Status = status, JournalEntries = journals },
            "receiving-reports" => new ReceivingReport { UserConfigId = f.Company.Id, ReferenceNo = reference, ReferenceDate = DateTime.Today, Status = status,
                SupplierId = f.Supplier.Id, CreditAccountId = f.Account.Id, InventoryLocationId = location, Amount = 10, Balance = 10, JournalEntries = journals,
                ReceivingReportDetails = new List<ReceivingReportDetail> { new() { ItemId = f.Item.Id, Quantity = 1, Rate = 10, Amount = 10, Status = status } } },
            "stock-issuances" => new StockIssuance { UserConfigId = f.Company.Id, ReferenceNo = reference, ReferenceDate = DateTime.Today, Status = status,
                InventoryLocationId = location, JournalEntries = journals, StockIssuanceDetails = new List<StockIssuanceDetail> { new() { ItemId = f.Item.Id, Quantity = 1, Cost = 10, Status = status } } },
            "stock-transfers" => new StockTransfer { UserConfigId = f.Company.Id, ReferenceNo = reference, ReferenceDate = DateTime.Today, Status = status,
                FromInventoryLocationId = location, ToInventoryLocationId = location, StockTransferDetails = new List<StockTransferDetail> { new() { ItemId = f.Item.Id, Quantity = 1, Status = status } } },
            _ => new InventoryAdjustment { UserConfigId = f.Company.Id, ReferenceNo = reference, ReferenceDate = DateTime.Today, Status = status,
                InventoryLocationId = location, AdjustmentAccountId = f.Account.Id, JournalEntries = journals,
                InventoryAdjustmentDetails = new List<InventoryAdjustmentDetail> { new() { ItemId = f.Item.Id, Quantity = 1, Rate = 10, Amount = 10, Status = status } } }
        };
    }

    [PagePreferenceTests.MySqlTheory, MemberData(nameof(Modules))]
    public async Task Lists_preserve_defaults_and_apply_sql_filters_paging_and_all_sort_fields(string module)
    {
        await using var f = await Fixture.Start(); var seed = await Seed(f);
        foreach (var (number, state) in new[] { ("Z-%_&", (short)1), ("A", (short)1), ("Draft", (short)0), ("Deleted", (short)-1) })
        {
            var model = Document(module, f, seed.Location.Id, number, state);
            f.Db.Add(model);
            f.Db.Entry(model).Property("ResponsibilityCenterEntry").CurrentValue = number == "Z-%_&" ? "[{\"id\":1},{\"id\":2}]" : "[{\"id\":1}]";
            await f.Db.SaveChangesAsync();
        }
        var foreign = Document(module, f, seed.Location.Id, "Foreign"); f.Db.Entry(foreign).Property("UserConfigId").CurrentValue = f.Other.Id; f.Db.Add(foreign); await f.Db.SaveChangesAsync();
        var path = "/api/" + module;
        string Url(object filter) => path + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(filter));
        var url = Url(new { userConfigId = f.Company.Id }); var expectedCount = module == "general-journals" ? 3 : 2;
        var rows = await f.Host.Client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(expectedCount, rows.GetArrayLength()); Assert.Equal("A", rows[0].GetProperty("referenceNo").GetString());
        Assert.Equal(module switch { "general-journals" => 7, "receiving-reports" => 14, "stock-transfers" => 9, _ => 10 }, rows[0].EnumerateObject().Count());
        Assert.Equal(module == "general-journals" ? 4 : 2, (await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, showDeleted = true }))).GetArrayLength());
        Assert.Equal("Draft", (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&status=0"))[0].GetProperty("statusName").GetString());
        Assert.Equal("Deleted", (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&status=-1"))[0].GetProperty("statusName").GetString());
        Assert.Single((await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, arrayString = "1,2" }))).EnumerateArray());
        Assert.Single((await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&search=%25_%26")).EnumerateArray());
        Assert.Single((await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, referenceNo = "Z-%_&" }))).EnumerateArray());
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = f.Company.Id, periodStart = DateTime.Today.AddDays(1), periodEnd = DateTime.Today.AddDays(2) }))).EnumerateArray());
        var page = await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=1&pageSize=1&sortBy=referenceNo&sortDirection=desc");
        Assert.Equal(expectedCount, page.GetProperty("totalCount").GetInt32()); Assert.Equal("Z-%_&", page.GetProperty("items")[0].GetProperty("referenceNo").GetString());
        Assert.Empty((await f.Host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=1")).GetProperty("items").EnumerateArray());
        foreach (var property in rows[0].EnumerateObject().Where(p => p.Name != "id" && !p.Name.EndsWith("Id") && p.Name != "responsibilityCenterEntry"))
        foreach (var direction in new[] { "asc", "desc" })
            Assert.Equal(expectedCount, (await f.Host.Client.GetFromJsonAsync<JsonElement>(url + $"&sortBy={property.Name}&sortDirection={direction}")).GetArrayLength());
        foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=2147483647&pageSize=200", "&sortBy=unknown", "&sortDirection=UP", "&status=9" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(url + suffix)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.GetAsync(path + "?criteria=broken")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.GetAsync(Url(new { userConfigId = f.Other.Id }))).StatusCode);
        int foreignId = (int)f.Db.Entry(foreign).Property("Id").CurrentValue;
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{foreignId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.DeleteAsync($"{path}/{foreignId}")).StatusCode);
    }

    [PagePreferenceTests.MySqlTheory, MemberData(nameof(Modules))]
    public async Task Writes_preserve_children_and_reject_foreign_or_other_parent_entries(string module)
    {
        await using var f = await Fixture.Start(); var seed = await Seed(f); var path = "/api/" + module;
        var body = JsonSerializer.SerializeToNode(Document(module, f, seed.Location.Id, "WRITE-1"), Json);
        // Read-only navigation data must not create or update master records.
        body["supplier"] = new JsonObject { ["name"] = "Injected" };
        if (module != "stock-transfers") body["journalEntries"][0]["account"] = new JsonObject { ["name"] = "Injected" };
        var response = await f.Host.Client.PostAsJsonAsync(path, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var saved = JsonNode.Parse(await response.Content.ReadAsStringAsync()); var id = saved["id"].GetValue<int>();
        Assert.Equal(HttpStatusCode.Conflict, (await f.Host.Client.PostAsJsonAsync(path, body)).StatusCode);
        var otherResponse = await f.Host.Client.PostAsJsonAsync(path, Document(module, f, seed.Location.Id, "WRITE-2"));
        Assert.True(otherResponse.StatusCode == HttpStatusCode.Created, await otherResponse.Content.ReadAsStringAsync());
        var other = JsonNode.Parse(await otherResponse.Content.ReadAsStringAsync());
        if (module != "stock-transfers")
        {
            var original = saved["journalEntries"].DeepClone(); saved["journalEntries"] = other["journalEntries"].DeepClone();
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{id}", saved)).StatusCode);
            saved["journalEntries"] = original;
            saved["journalEntries"][0]["accountId"] = f.ForeignAccount.Id;
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{id}", saved)).StatusCode);
            saved["journalEntries"][0]["accountId"] = f.Account.Id;
        }
        var lineKey = module switch { "receiving-reports" => "receivingReportDetails", "stock-transfers" => "stockTransferDetails", "stock-issuances" => "stockIssuanceDetails", "inventory-adjustments" => "inventoryAdjustmentDetails", _ => null };
        if (lineKey != null)
        {
            var original = saved[lineKey].DeepClone(); saved[lineKey] = other[lineKey].DeepClone();
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Host.Client.PutAsJsonAsync($"{path}/{id}", saved)).StatusCode); saved[lineKey] = original;
        }
        saved["userConfigId"] = f.Other.Id;
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Host.Client.PutAsJsonAsync($"{path}/{id}", saved)).StatusCode); saved["userConfigId"] = f.Company.Id;
        var detail = JsonNode.Parse(await f.Host.Client.GetStringAsync($"{path}/{id}"));
        detail["notes"] = "Updated";
        if (lineKey != null)
        {
            var replacement = detail[lineKey][0].DeepClone(); replacement["id"] = 0; replacement["quantity"] = 2;
            detail[lineKey][0]["deleted"] = true; detail[lineKey].AsArray().Add(replacement);
        }
        if (module != "stock-transfers")
        {
            var replacement = detail["journalEntries"][0].DeepClone(); replacement["id"] = 0; replacement["amount"] = 20;
            detail["journalEntries"][0]["deleted"] = true; detail["journalEntries"].AsArray().Add(replacement);
        }
        var updated = await f.Host.Client.PutAsJsonAsync($"{path}/{id}", detail);
        Assert.True(updated.StatusCode == HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
        var result = await f.Host.Client.GetFromJsonAsync<JsonElement>($"{path}/{id}"); Assert.Equal("Updated", result.GetProperty("notes").GetString());
        if (lineKey != null) Assert.Equal(2m, Assert.Single(result.GetProperty(lineKey).EnumerateArray()).GetProperty("quantity").GetDecimal());
        if (module != "stock-transfers") Assert.Equal(20m, Assert.Single(result.GetProperty("journalEntries").EnumerateArray()).GetProperty("amount").GetDecimal());
        var deleted = await f.Host.Client.DeleteAsync($"{path}/{id}"); Assert.True(deleted.StatusCode == HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Host.Client.GetAsync($"{path}/{id}")).StatusCode);
        Assert.Equal(2, await f.Db.Suppliers.CountAsync()); Assert.Equal(3, await f.Db.Accounts.CountAsync()); Assert.Equal(1, await f.Db.Items.CountAsync());
    }
}
