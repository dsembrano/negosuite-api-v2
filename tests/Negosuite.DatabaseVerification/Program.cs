using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using negosuite_api.Models;
using negosuite_api.Controllers;

var root = Path.GetFullPath(args.Length > 0 ? args[0] : Directory.GetCurrentDirectory());
var output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "bin", "phase5-verification");
Directory.CreateDirectory(output);
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{environment}.json", optional: true).AddEnvironmentVariables().Build();
var settings = new MySqlConnectionStringBuilder(config["ConnectionString:negosuite"]);
if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server, StringComparer.OrdinalIgnoreCase))
    throw new InvalidOperationException("Verification is restricted to the configured localhost development database.");
settings.ConnectionTimeout = 10;
settings.Pooling = false;
await using var connection = new MySqlConnection(settings.ConnectionString);
await connection.OpenAsync();
// Every subsequent explicit or implicit transaction on this session is read-only.
await new MySqlCommand("SET SESSION TRANSACTION READ ONLY", connection).ExecuteNonQueryAsync();
var report = new Dictionary<string, object>
{
    ["CapturedUtc"] = DateTime.UtcNow, ["HostScope"] = "configured localhost database",
    ["Runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    ["ConfigurationEnvironment"] = environment, ["ServerVersion"] = connection.ServerVersion,
    ["Mode"] = "read-only session; allowlisted report CALLs in read-only transactions; no write workflows"
};
async Task<List<string[]>> Read(string sql, params (string Name, object Value)[] parameters)
{
    using var command = new MySqlCommand(sql, connection) { CommandTimeout = 15 };
    foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
    using var reader = await command.ExecuteReaderAsync();
    var rows = new List<string[]>();
    while (await reader.ReadAsync())
        rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture)).ToArray());
    return rows;
}
var grants = await Read("SHOW GRANTS");
// Never persist raw grants: they contain account and host identities.
report["Privileges"] = new {
    HasExecuteGrant = grants.Any(r => r[0].Contains("EXECUTE", StringComparison.OrdinalIgnoreCase) || r[0].Contains("ALL PRIVILEGES", StringComparison.OrdinalIgnoreCase)),
    HasShowRoutineGrant = grants.Any(r => r[0].Contains("SHOW_ROUTINE", StringComparison.OrdinalIgnoreCase) || r[0].StartsWith("GRANT ALL PRIVILEGES ON *.*", StringComparison.OrdinalIgnoreCase)),
    Note = "Grant-text indicators only; exact per-object access is checked below. No raw account grants are recorded."
};
var columns = await Read("SELECT TABLE_NAME,COLUMN_NAME,COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME,ORDINAL_POSITION");
var routines = await Read("SELECT ROUTINE_NAME,ROUTINE_TYPE FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE() ORDER BY ROUTINE_NAME");
report["TableCount"] = columns.Select(r => r[0]).Distinct(StringComparer.OrdinalIgnoreCase).Count();
report["VisibleRoutines"] = routines.Select(r => new { Name = r[0], Type = r[1] }).ToArray();
var routineChecks = new List<object>();
var missingRoutines = 0;
foreach (var name in File.ReadAllLines(Path.Combine(root, "docs/migration-baseline/required-routines.txt")).Where(n => !string.IsNullOrWhiteSpace(n)))
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]+$")) throw new InvalidOperationException("Invalid routine identifier.");
    try
    {
        var definition = await Read($"SHOW CREATE PROCEDURE `{name}`");
        var visible = definition.Count > 0 && definition[0].Length > 2 && !string.IsNullOrEmpty(definition[0][2]);
        var present = routines.Any(r => r[0].Equals(name, StringComparison.OrdinalIgnoreCase) && r[1] == "PROCEDURE");
        if (!present) missingRoutines++;
        routineChecks.Add(new { Name = name, Present = present, DefinitionVisible = visible, Status = visible ? "definition-visible" : "definition-not-visible", ErrorCode = 0 });
    }
    catch (MySqlException ex)
    {
        var present = routines.Any(r => r[0].Equals(name, StringComparison.OrdinalIgnoreCase) && r[1] == "PROCEDURE");
        if (!present) missingRoutines++;
        routineChecks.Add(new { Name = name, Present = present, DefinitionVisible = false, Status = "definition-metadata-error", ErrorCode = ex.Number });
    }
}
report["RequiredRoutineChecks"] = routineChecks;
await using var db = new negosuiteContext(new DbContextOptionsBuilder<negosuiteContext>().UseMySQL(connection).Options);
var schemaGaps = new List<object>();
foreach (var entity in db.Model.GetEntityTypes().Where(e => e.FindPrimaryKey() != null || e.GetViewName() != null))
{
    var view = entity.GetViewName();
    var table = view ?? entity.GetTableName();
    if (table == null) continue;
    var tableColumns = columns.Where(r => r[0].Equals(table, StringComparison.OrdinalIgnoreCase)).ToArray();
    if (tableColumns.Length == 0) { schemaGaps.Add(new { Table = table, Column = "*", Issue = view == null ? "missing-table" : "missing-view" }); continue; }
    var identifier = view == null ? StoreObjectIdentifier.Table(table, entity.GetSchema()) : StoreObjectIdentifier.View(view, entity.GetViewSchema());
    foreach (var property in entity.GetProperties())
    {
        var column = property.GetColumnName(identifier);
        if (column != null && !tableColumns.Any(r => r[1].Equals(column, StringComparison.OrdinalIgnoreCase)))
            schemaGaps.Add(new { Table = table, Column = column, Issue = "missing-column" });
    }
}
report["MappedSchemaGaps"] = schemaGaps;
var acceptedGapKeys = new[] { "arpaymentdetail.*", "debtor.SLType", "debtortype.SLType", "vcitymunicipality.*" };
var blockingGaps = schemaGaps.Where(g => {
    var json = JsonSerializer.SerializeToElement(g);
    return !acceptedGapKeys.Contains(json.GetProperty("Table").GetString() + "." + json.GetProperty("Column").GetString(), StringComparer.OrdinalIgnoreCase);
}).ToArray();
report["AcceptedSchemaGapKeys"] = acceptedGapKeys;
report["BlockingSchemaGaps"] = blockingGaps;
var counts = new Dictionary<string, long>();
foreach (var table in new[] { "config", "user", "customer", "supplier", "salesinvoice", "salesreceipt", "bill", "payment", "journalentry", "inventorytransaction" })
    if (columns.Any(r => r[0].Equals(table, StringComparison.OrdinalIgnoreCase)))
        counts[table] = long.Parse((await Read($"SELECT COUNT(*) FROM `{table}`"))[0][0]);
report["RowCounts"] = counts;
var checks = new List<object>();
var failedChecks = 0;
async Task Check(string name, Func<Task> action)
{
    try { await action(); checks.Add(new { Name = name, Status = "passed", Error = "" }); }
    catch (Exception ex) { failedChecks++; checks.Add(new { Name = name, Status = "failed", Error = ex is MySqlException mysql ? $"MySQL {mysql.Number}" : ex.GetType().Name }); }
}
// SELECT-only materialization checks catch mismatches that count-only queries miss.
await Check("config-materialization", async () => { _ = await db.Configs.AsNoTracking().Take(1).ToListAsync(); });
await Check("user-materialization", async () => { _ = await db.Users.AsNoTracking().Take(1).ToListAsync(); });
await Check("customer-materialization", async () => { _ = await db.Customers.AsNoTracking().Take(1).ToListAsync(); });
await Check("invoice-materialization", async () => { _ = await db.SalesInvoices.AsNoTracking().Take(1).ToListAsync(); });
await Check("bill-materialization", async () => { _ = await db.Bills.AsNoTracking().Take(1).ToListAsync(); });
await Check("journal-materialization", async () => { _ = await db.JournalEntries.AsNoTracking().Take(1).ToListAsync(); });
await Check("inventory-materialization", async () => { _ = await db.InventoryTransactions.AsNoTracking().Take(1).ToListAsync(); });
var parity = new List<object>();
string SortedFingerprint(JsonElement rows)
{
    var sorted = rows.EnumerateArray().OrderBy(r => r.GetProperty("id").GetInt32()).ToArray();
    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sorted))));
}
string NormalizedFingerprint(JsonElement rows)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        void Write(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                writer.WriteStartObject();
                foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(p.Value); }
                writer.WriteEndObject();
            }
            else if (value.ValueKind == JsonValueKind.Array)
            { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) Write(item); writer.WriteEndArray(); }
            else if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                writer.WriteRawValue(number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));
            else value.WriteTo(writer);
        }
        Write(JsonSerializer.SerializeToElement(rows.EnumerateArray().OrderBy(r => r.GetProperty("id").GetInt32()).ToArray()));
    }
    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
}
var selectedConfig = await db.Configs.AsNoTracking().Where(c => c.Uuid != null && db.JournalEntries.Any(j => j.UserConfigId == c.Id)).OrderBy(c => c.Id).FirstOrDefaultAsync();
if (selectedConfig != null)
{
    var todayEnd = DateTime.Today.AddDays(1).AddTicks(-10);
    var sampleFloor = new DateTime(2000, 1, 1);
    report["FutureDatedJournalCount"] = await db.JournalEntries.CountAsync(j => j.JournalDate > todayEnd);
    var lastDate = await db.JournalEntries.Where(j => j.UserConfigId == selectedConfig.Id && j.Status == 1 && j.JournalDate >= sampleFloor && j.JournalDate <= todayEnd).MaxAsync(j => (DateTime?)j.JournalDate);
    if (lastDate.HasValue)
    {
        var end = lastDate.Value.Date.AddDays(1).AddTicks(-10);
        var start = lastDate.Value.Date.AddDays(-6);
        var criteria = new SelectCriteria { UserConfigId = selectedConfig.Id, PeriodStart = start, PeriodEnd = end };
        report["ReadOnlySample"] = new { TenantId = selectedConfig.Id, PeriodStart = start, PeriodEnd = end, Selection = "first configured tenant with journals; latest seven posted-journal days between 2000-01-01 and today; future dates excluded" };
        await Check("journal-controller-vs-sql", async () =>
        {
            var expected = (await Read("SELECT COUNT(*), COALESCE(SUM(CASE WHEN Nature='D' THEN Amount ELSE 0 END),0), COALESCE(SUM(CASE WHEN Nature='C' THEN Amount ELSE 0 END),0) FROM journalentry WHERE UserConfigId=@tenant AND Status=1 AND JournalDate>=@start AND JournalDate<=@end", ("@tenant", selectedConfig.Id), ("@start", start), ("@end", end)))[0];
            var result = await new FinancialReportsController(db).GetJournalTransactions(Newtonsoft.Json.JsonConvert.SerializeObject(criteria));
            if (result is not OkObjectResult ok) throw new InvalidOperationException("Report did not return OK.");
            var rows = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var debit = rows.EnumerateArray().Sum(r => r.GetProperty("debit").GetDecimal());
            var credit = rows.EnumerateArray().Sum(r => r.GetProperty("credit").GetDecimal());
            var matches = rows.GetArrayLength() == long.Parse(expected[0]) && debit == decimal.Parse(expected[1], System.Globalization.CultureInfo.InvariantCulture) && credit == decimal.Parse(expected[2], System.Globalization.CultureInfo.InvariantCulture);
            parity.Add(new { Report = "journal-transactions", MatchesSql = matches, Rows = rows.GetArrayLength(), Debit = debit, Credit = credit,
                FieldFingerprints = rows.GetArrayLength() == 0 ? new Dictionary<string, string>() : rows[0].EnumerateObject().ToDictionary(p => p.Name, p => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|", rows.EnumerateArray().OrderBy(r => r.GetProperty("id").GetInt32()).Select(r => r.GetProperty(p.Name).GetRawText())))))),
                NormalizedRowsSha256 = NormalizedFingerprint(rows), SortedResponseSha256 = SortedFingerprint(rows),
                ResponseSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rows.GetRawText()))) });
            if (!matches) throw new InvalidOperationException("Journal totals differ from SQL.");
        });
        await Check("customer-controller-vs-sql", async () =>
        {
            var expected = long.Parse((await Read("SELECT COUNT(*) FROM customer WHERE UserConfigId=@tenant AND Status=1", ("@tenant", selectedConfig.Id)))[0][0]);
            var controller = new CustomersController(db) { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
            // Direct-controller harness supplies the company normally resolved by ConfigUuidFilter.
            // Literal key keeps this same harness compilable against the historical .NET 6 API.
            controller.HttpContext.Items["Negosuite.ValidatedCompanyId"] = selectedConfig.Id;
            var result = await controller.GetCustomers(Newtonsoft.Json.JsonConvert.SerializeObject(criteria));
            if (result is not OkObjectResult ok) throw new InvalidOperationException("Customer query did not return OK.");
            var rows = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var matches = rows.GetArrayLength() == expected;
            parity.Add(new { Report = "customers", MatchesSql = matches, Rows = rows.GetArrayLength(),
                NormalizedRowsSha256 = NormalizedFingerprint(rows), SortedResponseSha256 = SortedFingerprint(rows),
                ResponseSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rows.GetRawText()))) });
            if (!matches) throw new InvalidOperationException("Customer count differs from SQL.");
        });
    }
}
var reportChecks = new List<object>();
if (selectedConfig != null && report.ContainsKey("ReadOnlySample"))
{
    var sample = JsonSerializer.SerializeToElement(report["ReadOnlySample"]);
    var end = sample.GetProperty("PeriodEnd").GetDateTime();
    var start = new DateTime(end.Year, 1, 1);
    var ar = selectedConfig.ARTradeAccountId;
    var ap = selectedConfig.APTradeAccountId;
    async Task ReportCheck(string name, int? account, Func<string, Task<ActionResult>> run)
    {
        var criteria = new SelectCriteria { UserConfigId = selectedConfig.Id, PeriodStart = start, PeriodEnd = end, AccountId = account, ArrayString = "" };
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await run(Newtonsoft.Json.JsonConvert.SerializeObject(criteria));
            if (result is not OkObjectResult ok) throw new InvalidOperationException("Expected OK report result.");
            var rows = JsonSerializer.SerializeToElement(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var hashes = rows.EnumerateArray().Select(r => NormalizedFingerprint(JsonSerializer.SerializeToElement(new[] { new { id = 0, value = r } }))).OrderBy(h => h, StringComparer.Ordinal);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", hashes))));
            reportChecks.Add(new { Name = name, Status = rows.GetArrayLength() > 0 ? "passed" : "empty-sample", Rows = rows.GetArrayLength(), NormalizedRowsSha256 = hash, PeriodStart = start, PeriodEnd = end, AccountId = account, Error = "" });
            if (rows.GetArrayLength() == 0) failedChecks++;
            await transaction.RollbackAsync();
        }
        catch (Exception ex)
        {
            failedChecks++;
            reportChecks.Add(new { Name = name, Status = "failed", Rows = 0, NormalizedRowsSha256 = "", PeriodStart = start, PeriodEnd = end, AccountId = account, Error = ex is MySqlException mysql ? $"MySQL {mysql.Number}" : ex.GetType().Name });
        }
    }
    // Fixed GET allowlist; creation/posting/view-generation routines are never invoked.
    await ReportCheck("sales", null, new SalesReportsController(db).GetSalesTransactionsRC);
    var billsController = new BillsController(db) { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
    billsController.HttpContext.Items["Negosuite.ValidatedCompanyId"] = selectedConfig.Id;
    await ReportCheck("purchases", null, criteria => billsController.GetBills(criteria));
    await ReportCheck("receivables", ar, new ReceivableReportsController(db).GetCustomerBalancesRC);
    await ReportCheck("payables", ap, new PayableReportsController(db).GetCustomerBalancesRC);
    await ReportCheck("inventory", null, new InventoryReportsController(db).GetInventoryTransactions);
    await ReportCheck("trial-balance", null, new FinancialReportsController(db).GetAccountRecap);
    await ReportCheck("general-ledger", null, new FinancialReportsController(db).GetGeneralLedgerSummary);
}
report["ReportChecks"] = reportChecks;
report["ControllerSqlComparisons"] = parity;
report["ReadChecks"] = checks;
report["MissingRequiredRoutineCount"] = missingRoutines;
var ready = missingRoutines == 0 && blockingGaps.Length == 0 && failedChecks == 0 && parity.Count == 2 && reportChecks.Count == 7;
report["ReadyForBusinessRegression"] = ready;
report["Limitations"] = "SELECT counts/materialization do not establish financial correctness, routine execution, ownership checks or .NET 6 equivalence. No row contents or credentials are saved.";
await File.WriteAllTextAsync(Path.Combine(output, "database-readiness.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Local database verified: MySQL {connection.ServerVersion}; {report["TableCount"]} tables; {routines.Count} visible routines; {missingRoutines} required routines missing; {schemaGaps.Count} mapped schema gaps.");
Console.WriteLine($"Read-only report saved under {output}");
return ready ? 0 : 2;
