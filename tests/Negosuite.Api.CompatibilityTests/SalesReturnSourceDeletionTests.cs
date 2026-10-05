using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SalesReturnSourceDeletionTests
{
    [PagePreferenceTests.MySqlTheory]
    [InlineData(false, "draft", false)] [InlineData(true, "draft", false)]
    [InlineData(false, "posted", false)] [InlineData(true, "posted", false)]
    [InlineData(false, "voided", false)] [InlineData(true, "voided", false)]
    [InlineData(false, "deleted", false)] [InlineData(true, "deleted", false)]
    [InlineData(false, "draft", true)] [InlineData(true, "draft", true)]
    [InlineData(false, "posted", true)] [InlineData(true, "posted", true)]
    [InlineData(false, "voided", true)] [InlineData(true, "voided", true)]
    [InlineData(false, "deleted", true)] [InlineData(true, "deleted", true)]
    public async Task Source_deletion_blocks_active_returns_and_preserves_deleted_return_history(bool cash, string state, bool removeLine)
    {
        await using var f = await SalesReturnTests.Fixture.Start(cash);
        var write = f.Write(1);
        if (!cash) write.Applications.Add(new() { JournalEntryId = f.Target, Amount = 100 });
        var created = await f.Send(HttpMethod.Post, "/api/sales-returns", write);
        var returnId = created.GetProperty("id").GetInt32();
        if (state is "posted" or "voided")
            await f.Send(HttpMethod.Post, $"/api/sales-returns/{returnId}/post", new { version = 1 });
        if (state == "voided")
            await f.Send(HttpMethod.Post, $"/api/sales-returns/{returnId}/void", new { version = 2, reason = "Test reversal" });
        if (state == "deleted")
        {
            using var deletion = await f.Base.Host.Client.DeleteAsync($"/api/sales-returns/{returnId}?version=1");
            Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        }
        var path = $"/api/{(cash ? "sales-receipts" : "sales-invoices")}/{f.SourceId}";
        var before = await f.Send(HttpMethod.Get, path);
        var history = await f.Send(HttpMethod.Get, $"/api/sales-returns/{returnId}");
        var snapshot = await f.Db.SalesReturns.AsNoTracking().Where(r => r.Id == returnId).Select(r => r.SourceSnapshotJson).SingleAsync();
        var stock = await f.Stock();
        var balance = await f.TargetBalance();
        var journalCount = await f.Db.JournalEntries.CountAsync();
        using var request = new HttpRequestMessage(removeLine ? HttpMethod.Put : HttpMethod.Delete, path);
        if (removeLine)
        {
            var body = JsonNode.Parse(before.GetRawText())!;
            var lines = body[cash ? "salesReceiptDetails" : "salesInvoiceDetails"]!.AsArray();
            var replacement = lines[0]!.DeepClone();
            replacement["id"] = 0;
            lines[0]!["deleted"] = true;
            lines.Add(replacement);
            request.Content = JsonContent.Create(body);
        }
        using var response = await f.Base.Host.Client.SendAsync(request);
        var error = await response.Content.ReadAsStringAsync();
        if (state is "draft" or "posted")
        {
            Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{response.StatusCode}: {error}");
            Assert.Contains("draft or posted Sales Return", error);
            var after = await f.Send(HttpMethod.Get, path);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before.GetRawText()), JsonNode.Parse(after.GetRawText())));
            Assert.Equal(balance, await f.TargetBalance());
            Assert.Equal(journalCount, await f.Db.JournalEntries.CountAsync());
        }
        else
        {
            Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{response.StatusCode}: {error}");
            if (removeLine)
            {
                var after = await f.Send(HttpMethod.Get, path);
                var lines = after.GetProperty(cash ? "salesReceiptDetails" : "salesInvoiceDetails");
                Assert.Equal(1, lines.GetArrayLength());
                Assert.NotEqual(f.LineId, lines[0].GetProperty("id").GetInt32());
                Assert.Equal(balance, await f.TargetBalance());
            }
            else await f.Reject(HttpMethod.Get, path, null, HttpStatusCode.NotFound);
            var retained = await f.Db.SalesReturns.AsNoTracking().SingleAsync(r => r.Id == returnId);
            Assert.Equal(-1, retained.Status);
            Assert.Equal(snapshot, retained.SourceSnapshotJson);
            Assert.Equal(removeLine, retained.SalesInvoiceId.HasValue || retained.SalesReceiptId.HasValue);
            var line = await f.Db.SalesReturnDetails.AsNoTracking().SingleAsync(d => d.SalesReturnId == returnId);
            Assert.Null(line.SalesInvoiceDetailId); Assert.Null(line.SalesReceiptDetailId);
            Assert.Equal(f.LineId, line.OriginalSourceDetailId);
            var preserved = await f.Send(HttpMethod.Get, $"/api/sales-returns/{returnId}");
            Assert.Equal(f.SourceId, preserved.GetProperty("sourceId").GetInt32());
            Assert.Equal(f.LineId, preserved.GetProperty("lines")[0].GetProperty("sourceDetailId").GetInt32());
            Assert.Equal(history.GetProperty("amount").GetDecimal(), preserved.GetProperty("amount").GetDecimal());
            Assert.Equal(history.GetProperty("applications").GetArrayLength(), preserved.GetProperty("applications").GetArrayLength());
            var list = await f.Send(HttpMethod.Get, "/api/sales-returns?status=-1&pageNumber=1&pageSize=25");
            Assert.Equal(cash ? "SR" : "SI", list.GetProperty("items")[0].GetProperty("source").GetString());
            Assert.Equal(cash ? "CASH-1" : "CHARGE-1", list.GetProperty("items")[0].GetProperty("sourceNo").GetString());
            await f.Migrate(); // Migration is repeatable even after records have been detached.
        }
        Assert.Equal(stock, await f.Stock());
        Assert.Equal(1, await f.Db.SalesReturnDetails.CountAsync(d => d.SalesReturnId == returnId));
    }

    [PagePreferenceTests.MySqlTheory, InlineData(false), InlineData(true)]
    public async Task Invoice_without_returns_can_still_be_deleted(bool cash)
    {
        await using var f = await SalesReturnTests.Fixture.Start(cash);
        var path = $"/api/{(cash ? "sales-receipts" : "sales-invoices")}/{f.SourceId}";
        using var response = await f.Base.Host.Client.DeleteAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        await f.Reject(HttpMethod.Get, path, null, HttpStatusCode.NotFound);
        Assert.False(await f.Db.JournalEntries.AnyAsync(j => cash ? j.SalesReceiptId == f.SourceId : j.SalesInvoiceId == f.SourceId));
        if (cash) Assert.False(await f.Db.SalesReceiptDetails.AnyAsync(d => d.SalesReceiptId == f.SourceId));
        else Assert.False(await f.Db.SalesInvoiceDetails.AnyAsync(d => d.SalesInvoiceId == f.SourceId));
    }
}
