using System.Net;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Xunit;

namespace Negosuite.Api.CompatibilityTests;

public class SalesReturnDetachmentSafetyTests
{
    [PagePreferenceTests.MySqlTheory, InlineData(false), InlineData(true)]
    public async Task Later_delete_failure_restores_detached_history_links(bool cash)
    {
        await using var f = await SalesReturnTests.Fixture.Start(cash);
        var write = f.Write(1);
        if (!cash) write.Applications.Add(new() { JournalEntryId = f.Target, Amount = 100 });
        var created = await f.Send(HttpMethod.Post, "/api/sales-returns", write);
        var id = created.GetProperty("id").GetInt32();
        await f.Send(HttpMethod.Post, $"/api/sales-returns/{id}/post", new { version = 1 });
        await f.Send(HttpMethod.Post, $"/api/sales-returns/{id}/void", new { version = 2, reason = "Reverse" });
        await f.Db.Database.ExecuteSqlRawAsync(cash
            ? "CREATE TRIGGER reject_source_delete BEFORE DELETE ON salesreceipt FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected delete failure'"
            : "CREATE TRIGGER reject_source_delete BEFORE DELETE ON salesinvoice FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected delete failure'");
        var path = $"/api/{(cash ? "sales-receipts" : "sales-invoices")}/{f.SourceId}";
        HttpResponseMessage response = null; Exception failure = null;
        try { response = await f.Base.Host.Client.DeleteAsync(path); } catch (Exception ex) { failure = ex; }
        using (response) Assert.True(failure != null || response?.IsSuccessStatusCode == false);
        var retained = await f.Db.SalesReturns.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(f.SourceId, cash ? retained.SalesReceiptId : retained.SalesInvoiceId);
        Assert.Null(retained.ApplicationsJson);
        var line = await f.Db.SalesReturnDetails.AsNoTracking().SingleAsync(d => d.SalesReturnId == id);
        Assert.Equal(f.LineId, cash ? line.SalesReceiptDetailId : line.SalesInvoiceDetailId);
        Assert.Null(line.OriginalSourceDetailId);
        Assert.Equal(1008m, await f.TargetBalance());
        Assert.Equal(0m, await f.Stock());
        await f.Send(HttpMethod.Get, path);
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_source_delete");
        using var retry = await f.Base.Host.Client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
    }

    [PagePreferenceTests.MySqlTheory, InlineData(false), InlineData(true)]
    public async Task Mixed_active_and_deleted_returns_do_not_release_any_links(bool cash)
    {
        await using var f = await SalesReturnTests.Fixture.Start(cash);
        var deleted = await f.Send(HttpMethod.Post, "/api/sales-returns", f.Write(1));
        var id = deleted.GetProperty("id").GetInt32();
        using var removed = await f.Base.Host.Client.DeleteAsync($"/api/sales-returns/{id}?version=1");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await f.Send(HttpMethod.Post, "/api/sales-returns", f.Write(1));
        await f.Reject(HttpMethod.Delete, $"/api/{(cash ? "sales-receipts" : "sales-invoices")}/{f.SourceId}", null, HttpStatusCode.Conflict);
        Assert.Equal(2, await f.Db.SalesReturns.CountAsync(r => cash ? r.SalesReceiptId == f.SourceId : r.SalesInvoiceId == f.SourceId));
        Assert.Equal(2, await f.Db.SalesReturnDetails.CountAsync(d => cash ? d.SalesReceiptDetailId == f.LineId : d.SalesInvoiceDetailId == f.LineId));
    }

    [MySqlFact]
    public async Task Incomplete_snapshot_blocks_deletion_without_losing_history()
    {
        await using var f = await SalesReturnTests.Fixture.Start();
        var created = await f.Send(HttpMethod.Post, "/api/sales-returns", f.Write(1));
        var id = created.GetProperty("id").GetInt32();
        using var removed = await f.Base.Host.Client.DeleteAsync($"/api/sales-returns/{id}?version=1");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE salesreturn SET SourceSnapshotJson='{{}}' WHERE Id={id}");
        using var response = await f.Base.Host.Client.DeleteAsync($"/api/sales-invoices/{f.SourceId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("incomplete invoice history", await response.Content.ReadAsStringAsync());
        Assert.True(await f.Db.SalesReturnDetails.AnyAsync(d => d.SalesInvoiceDetailId == f.LineId));
        Assert.True(await f.Db.SalesInvoices.AnyAsync(i => i.Id == f.SourceId));
    }

    [MySqlFact]
    public async Task Database_guards_prevent_detaching_active_lines_and_reactivating_detached_returns()
    {
        await using var f = await SalesReturnTests.Fixture.Start();
        var created = await f.Send(HttpMethod.Post, "/api/sales-returns", f.Write(1));
        var id = created.GetProperty("id").GetInt32();
        await Assert.ThrowsAsync<MySqlException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE salesreturndetail SET OriginalSourceDetailId=SalesInvoiceDetailId,SalesInvoiceDetailId=NULL WHERE SalesReturnId={id}"));
        await Assert.ThrowsAsync<MySqlException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE salesreturn SET SalesInvoiceId=NULL WHERE Id={id}"));
        using var removed = await f.Base.Host.Client.DeleteAsync($"/api/sales-returns/{id}?version=1");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE salesreturndetail SET OriginalSourceDetailId=SalesInvoiceDetailId,SalesInvoiceDetailId=NULL WHERE SalesReturnId={id}");
        await Assert.ThrowsAsync<MySqlException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE salesreturn SET Status=0 WHERE Id={id}"));
    }
}
