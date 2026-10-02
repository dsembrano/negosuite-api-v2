using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using negosuite_api.Contracts.SalesInvoices;
using negosuite_api.Models;
using negosuite_api.Services;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SalesInvoiceTests
{
    public static IEnumerable<object[]> SortCases() => from field in new[] { "invoiceNo", "invoiceDate", "dueDate", "purchaseOrderNo", "customerName", "customerTIN", "billingAddress", "billingContactName", "billingContactEmail", "shippingAddress", "shippingContactName", "shippingContactEmail", "amount", "balance", "paymentTermName", "notes", "status", "statusName" }
        from direction in new[] { "asc", "desc" } select new object[] { field, direction };

    [Theory, MemberData(nameof(SortCases))]
    public void Sort_search_and_paging_are_composed_in_sql(string field, string direction)
    {
        using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL("server=127.0.0.1;database=translation;user=unused;password=unused").Options);
        var query = new SalesInvoiceService(db).Query(16, new(), "[1,2]", null, new DateTime(2026, 9, 30));
        var sql = SalesInvoiceQuery.Sort(SalesInvoiceQuery.Search(query, "needle"), field, direction).Skip(10).Take(10).ToQueryString();
        Assert.Contains("JSON_CONTAINS", sql); Assert.Contains("ORDER BY", sql); Assert.Contains("LIMIT", sql); Assert.Contains("OFFSET", sql);
        if (direction == "desc") Assert.Contains("DESC", sql);
    }

    [Theory]
    [InlineData(null, true)] [InlineData("", true)] [InlineData(" ", true)] [InlineData("1, 2", true)]
    [InlineData("[]", false)] [InlineData("-1", false)] [InlineData("1.2", false)] [InlineData("\"1\"", false)]
    [InlineData("1); DROP TABLE salesinvoice", false)] [InlineData("9999999999999999999999999", false)]
    public void Responsibility_center_values_are_validated_as_integer_ids(string input, bool valid) =>
        Assert.Equal(valid, SalesInvoiceQuery.TryParseCenters(input, out _));

    [MySqlFact]
    public async Task Invoice_lists_preserve_filters_status_labels_and_support_sql_pages()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_invoice_lists_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var company = new Config { CompanyName = "Invoice fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other invoice fixture", Uuid = Guid.NewGuid().ToString() };
            db.Configs.AddRange(company, other); await db.SaveChangesAsync();
            var customer = new Customer { UserConfigId = company.Id, Name = "Invoice buyer", Tin = "TIN-123", Status = true };
            var foreignCustomer = new Customer { UserConfigId = other.Id, Name = "Foreign buyer", Status = true };
            var supplier = new Supplier { UserConfigId = company.Id, Name = "Invoice supplier", Status = true };
            var term = new PaymentTerm { Name = "Thirty days", Code = "N30" };
            db.Customers.AddRange(customer, foreignCustomer); db.Suppliers.Add(supplier); db.PaymentTerms.Add(term); await db.SaveChangesAsync();
            var today = DateTime.Today;
            SalesInvoice Invoice(string number, decimal amount, decimal balance, int dueDays = 0) => new()
            { UserConfigId = company.Id, CustomerId = customer.Id, PaymentTermId = term.Id, InvoiceNo = number, InvoiceDate = today, DueDate = today.AddDays(dueDays), Amount = amount, Balance = balance, Status = 1 };
            var first = Invoice("Z-%_&", 100, 100); first.SupplierId = supplier.Id;
            first.ResponsibilityCenterEntry = "[{\"id\":1},{\"id\":2}]"; first.PurchaseOrderNo = "PO-123";
            first.BillingContactEmail = "billing@example.test"; first.ShippingContactName = "Warehouse manager"; first.Notes = "special delivery";
            var partial = Invoice("A", 20, 10); partial.ResponsibilityCenterEntry = "[{\"id\":1}]";
            var paid = Invoice("B", 10, 0);
            var zero = Invoice("C", 0, 0, 2); // Preserve the old status precedence for a zero-value invoice.
            var overdue = Invoice("D", 100, 100, -3);
            var future = Invoice("E", 100, 100, 2);
            var midday = Invoice("F", 100, 100); midday.InvoiceDate = today.AddHours(12);
            var draft = Invoice("Draft", 100, 100); draft.Status = 0;
            var deleted = Invoice("Deleted", 100, 100); deleted.Status = -1;
            var foreign = Invoice("Foreign", 100, 100); foreign.UserConfigId = other.Id; foreign.CustomerId = foreignCustomer.Id;
            db.SalesInvoices.AddRange(first, partial, paid, zero, overdue, future, midday, draft, deleted, foreign); await db.SaveChangesAsync();
            const string path = "/api/sales-invoices";
            string Url(object criteria) => path + "?criteria=" + Uri.EscapeDataString(JsonSerializer.Serialize(criteria));
            var url = Url(new { userConfigId = company.Id });
            Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync(url)).StatusCode);
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ApiHost.MemberTokenAsync(db, company.Id)); host.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
            var rows = await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url);
            Assert.Equal(7, rows.Length);
            Assert.Equal(new[] { partial.Id, paid.Id, zero.Id, overdue.Id, future.Id, first.Id, midday.Id }, rows.Select(i => i.Id));
            Assert.Equal("Due today", rows.Single(i => i.Id == first.Id).StatusName);
            Assert.Equal("Partially paid", rows.Single(i => i.Id == partial.Id).StatusName);
            Assert.Equal("Paid", rows.Single(i => i.Id == paid.Id).StatusName);
            Assert.Equal("Due in 2 days", rows.Single(i => i.Id == zero.Id).StatusName);
            Assert.Equal("3 days overdue", rows.Single(i => i.Id == overdue.Id).StatusName);
            Assert.Equal(22, (await host.Client.GetFromJsonAsync<JsonElement>(url))[0].EnumerateObject().Count());
            Assert.Equal(7, (await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, showDeleted = true, status = false }))).Length);
            Assert.Equal("Draft", Assert.Single(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url + "&status=0")).StatusName);
            Assert.Equal("Deleted", Assert.Single(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url + "&status=-1")).StatusName);
            Assert.Equal(6, (await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, periodStart = today, periodEnd = today }))).Length);
            Assert.Equal(7, (await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, periodStart = today.AddYears(1) }))).Length);
            Assert.Single(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, referenceNo = first.InvoiceNo })));
            Assert.Single(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, supplierId = supplier.Id })));
            Assert.Empty(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, customerId = foreignCustomer.Id })));
            Assert.Equal(first.Id, Assert.Single(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, arrayString = "1,2" }))).Id);
            Assert.Equal(2, (await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(Url(new { userConfigId = company.Id, arrayString = "1" }))).Length);
            foreach (var search in new[] { "%_&", "PO-123", "billing@example.test", "Warehouse manager", "special delivery" })
                Assert.Equal(first.Id, Assert.Single(await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url + "&search=" + Uri.EscapeDataString(" " + search + " "))).Id);
            foreach (var search in new[] { "Invoice buyer", "TIN-123", "Thirty days" })
                Assert.Equal(7, (await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url + "&search=" + Uri.EscapeDataString(search))).Length);
            var page = await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=2&pageSize=2&sortBy=amount&sortDirection=asc");
            Assert.Equal(7, page.GetProperty("totalCount").GetInt32()); Assert.Equal(4, page.GetProperty("totalPages").GetInt32());
            Assert.Equal(partial.Id, page.GetProperty("items")[0].GetProperty("id").GetInt32());
            foreach (var sort in SortCases())
            {
                var sorted = await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url + $"&sortBy={sort[0]}&sortDirection={sort[1]}");
                var expectedIds = SalesInvoiceQuery.Sort(rows.AsQueryable(), (string)sort[0], (string)sort[1]).Select(i => i.Id);
                Assert.Equal(expectedIds, sorted.Select(i => i.Id));
            }
            var combined = await host.Client.GetFromJsonAsync<JsonElement>(Url(new { userConfigId = company.Id, arrayString = "1" }) + "&search=Partially%20paid&pageNumber=1&pageSize=1");
            Assert.Equal(1, combined.GetProperty("totalCount").GetInt32());
            Assert.Equal(partial.Id, combined.GetProperty("items")[0].GetProperty("id").GetInt32());
            Assert.Empty((await host.Client.GetFromJsonAsync<JsonElement>(url + "&pageNumber=99&pageSize=50")).GetProperty("items").EnumerateArray());
            foreach (var suffix in new[] { "&pageSize=1", "&pageNumber=1", "&pageNumber=0&pageSize=1", "&pageNumber=1&pageSize=201", "&pageNumber=2147483647&pageSize=200", "&sortBy=unknown", "&sortDirection=up", "&status=2" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(url + suffix)).StatusCode);
            foreach (var criteria in new[] { "", "?criteria=null", "?criteria=%7B%7D", "?criteria=%7Bbroken" })
                Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(path + criteria)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.GetAsync(Url(new { userConfigId = company.Id, arrayString = "1); DROP TABLE salesinvoice" }))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync(Url(new { userConfigId = other.Id }))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"{path}/{foreign.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.DeleteAsync($"{path}/{foreign.Id}")).StatusCode);
            var detail = await host.Client.GetFromJsonAsync<SalesInvoice>($"{path}/{first.Id}");
            Assert.Equal(first.Amount, detail.Amount); Assert.Equal(customer.Name, detail.Customer.Name);
            db.SalesInvoices.AddRange(Enumerable.Range(1, 205).Select(i => Invoice("BULK-" + i, 1, 1))); await db.SaveChangesAsync();
            Assert.Equal(212, (await host.Client.GetFromJsonAsync<SalesInvoiceListItemDto[]>(url)).Length);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [MySqlFact]
    public async Task Invoice_writes_preserve_numbering_rounding_payment_guards_and_child_ownership()
    {
        var connection = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NEGOSUITE_PHASE3_MYSQL"));
        Assert.Equal("127.0.0.1", connection.Server); Assert.Equal(33316u, connection.Port);
        connection.Database = "negosuite_invoice_writes_" + Guid.NewGuid().ToString("N");
        using var host = new ApiHost(connection.ConnectionString);
        using var scope = host.Server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<negosuiteContext>();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var company = new Config { CompanyName = "Invoice write fixture", Uuid = Guid.NewGuid().ToString() };
            var other = new Config { CompanyName = "Other invoice write fixture", Uuid = Guid.NewGuid().ToString() };
            db.Configs.AddRange(company, other); await db.SaveChangesAsync();
            var customer = new Customer { UserConfigId = company.Id, Name = "Buyer", Status = true };
            var foreignCustomer = new Customer { UserConfigId = other.Id, Name = "Other buyer", Status = true };
            var item = new Item { UserConfigId = company.Id, Name = "Item", Status = true };
            var category = new AccountCategory { UserConfigId = company.Id, Name = "Sales", Type = "I" };
            var account = new Account { UserConfigId = company.Id, Name = "Sales", Code = "4000", Category = category };
            db.Customers.AddRange(customer, foreignCustomer); db.Items.Add(item); db.Accounts.Add(account); await db.SaveChangesAsync();
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ApiHost.MemberTokenAsync(db, company.Id)); host.Client.DefaultRequestHeaders.Add("configUuid", company.Uuid);
            const string path = "/api/sales-invoices";
            SalesInvoice Invoice(string number) => new() { UserConfigId = company.Id, CustomerId = customer.Id, InvoiceNo = number, InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30), Amount = 100, Balance = 100, Status = 1,
                SalesInvoiceDetails = new List<SalesInvoiceDetail> { new() { ItemId = item.Id, Quantity = 1, Rate = 100, Amount = 100, Status = 1 } },
                JournalEntries = new List<JournalEntry> { new() { UserConfigId = company.Id, AccountId = account.Id, Amount = 100.125m, Balance = 100.125m, Nature = "C", Source = "SI", Status = 1 } } };
            var input = Invoice("SI-ONE");
            var response = await host.Client.PostAsJsonAsync(path, input);
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); Assert.NotNull(response.Headers.Location);
            var created = await response.Content.ReadFromJsonAsync<SalesInvoice>();
            Assert.Equal("SI-ONE", created.InvoiceNo); Assert.Equal(100.12m, Assert.Single(created.JournalEntries).Amount);
            Assert.Equal(created.InvoiceDate, Assert.Single(created.JournalEntries).JournalDate);
            Assert.Equal(created.InvoiceNo, Assert.Single(created.JournalEntries).ReferenceNo);
            Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PostAsJsonAsync(path, Invoice("SI-ONE"))).StatusCode);
            input = Invoice("SI-TWO");
            var secondResponse = await host.Client.PostAsJsonAsync(path, input);
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
            var second = await secondResponse.Content.ReadFromJsonAsync<SalesInvoice>();
            var update = created;
            update.Balance = -1;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            update.Balance = 100; update.InvoiceNo = second.InvoiceNo;
            Assert.Equal(HttpStatusCode.Conflict, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            update.InvoiceNo = "SI-ONE";
            var ownDetail = Assert.Single(update.SalesInvoiceDetails);
            update.SalesInvoiceDetails = second.SalesInvoiceDetails;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            update.SalesInvoiceDetails = new List<SalesInvoiceDetail> { ownDetail };
            var ownJournal = Assert.Single(update.JournalEntries);
            update.JournalEntries = second.JournalEntries;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            update.JournalEntries = new List<JournalEntry> { ownJournal };
            update.CustomerId = foreignCustomer.Id;
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            update.CustomerId = customer.Id; update.UserConfigId = other.Id;
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            update.UserConfigId = company.Id; update.Balance = 50;
            ownDetail.Deleted = true; ownJournal.Deleted = true;
            update.SalesInvoiceDetails.Add(new() { ItemId = item.Id, Quantity = 2, Rate = 50, Amount = 100, Status = 1 });
            update.JournalEntries.Add(new() { UserConfigId = company.Id, ReferenceNo = update.InvoiceNo, AccountId = account.Id, Amount = 50.555m, Balance = 50.555m, Nature = "C", Source = "SI", Status = 1 });
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PutAsJsonAsync($"{path}/{created.Id}", update)).StatusCode);
            var detail = await host.Client.GetFromJsonAsync<SalesInvoice>($"{path}/{created.Id}");
            Assert.Equal(50m, detail.Balance); Assert.Equal(2m, Assert.Single(detail.SalesInvoiceDetails).Quantity);
            Assert.Equal(50.56m, Assert.Single(detail.JournalEntries).Amount);
            Assert.Equal(HttpStatusCode.BadRequest, (await host.Client.DeleteAsync($"{path}/{created.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await host.Client.DeleteAsync($"{path}/{second.Id}")).StatusCode);
            Assert.False(await db.SalesInvoiceDetails.AnyAsync(d => d.SalesInvoiceId == second.Id));
            Assert.False(await db.JournalEntries.AnyAsync(j => j.SalesInvoiceId == second.Id));
            company.AutoReferenceNoConfig = "{\"autoSIReferenceNo\":true,\"autoSIReferenceNoFormat\":\"########\",\"autoSIReferenceNoPrefix\":\"AUTO-\"}";
            await db.SaveChangesAsync();
            var autoResponse = await host.Client.PostAsJsonAsync(path, Invoice("Ignored"));
            Assert.Equal(HttpStatusCode.Created, autoResponse.StatusCode);
            var auto = await autoResponse.Content.ReadFromJsonAsync<SalesInvoice>();
            Assert.Equal("AUTO-00000001", auto.InvoiceNo);
            Assert.Equal(auto.InvoiceNo, Assert.Single(auto.JournalEntries).ReferenceNo);
            Assert.Equal(1, await db.TransactionSequences.Where(s => s.UserConfigId == company.Id && s.Source == "SI").Select(s => s.LastSequence).SingleAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
