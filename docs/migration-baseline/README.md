# Negosuite API V2: phase 1 migration baseline

Captured on 2026-09-25 from commit `ba95f06c6fad0f51072a185eac234fe6f4743e54`.

**Status: build, dependency inventory, API contract and database-independent HTTP baseline complete. Database preparation is partial; representative report results and deployed database version remain unverified.** No application source, package references, target framework, production configuration, or existing database contents were changed.

## Build and environment

| Item | Observed baseline |
| --- | --- |
| Project | `negosuite-api.csproj`, one web project, `net6.0` |
| Installed SDKs | 6.0.428 and 10.0.203 |
| Default SDK without global.json | 10.0.203 |
| Runtime used for HTTP capture | ASP.NET Core / .NET 6.0.36 |
| Explicit .NET 6 SDK Release rebuild | Passed: 0 errors, 4 warnings |
| Initial default SDK Release build | Passed; additionally reports NETSDK1138 for unsupported net6.0 |
| Existing automated test projects | None found |

The initial command used solution-level `-o` and also produced NETSDK1194. That warning was caused by the invocation, not the application; the repeatable script builds the project instead. Build used the existing package cache/restore state; this is not proof of a clean-machine restore.

Existing compiler warnings:

`build.txt` preserves the successful explicit SDK 6 rebuild output; `evidence.json` summarizes the measured results.

| Code | Location | Finding |
| --- | --- | --- |
| CS0168 | Controllers/DiscountTypesController.cs:129 | Unused exception variable `ex` |
| CS0168 | Controllers/TaxRatesController.cs:130 | Unused exception variable `ex` |
| CS0414 | Controllers/PayableReportsController.cs:21 | Unused `AGING_BY_BILL_DUE_DATE` field |
| CS0414 | Controllers/ReceivableReportsController.cs:20 | Unused `AGING_BY_INVOICE_DUE_DATE` field |

## Dependencies

`dependencies.json` records all 9 direct and 189 transitive resolved packages. `vulnerabilities.json` records the successful NuGet advisory query, including advisory URLs and severities. An initial restricted-network query failed; the subsequent authorized network query succeeded.

The audit flags 12 transitive packages:

| Package | Resolved version | Highest reported severity |
| --- | --- | --- |
| MessagePack | 2.1.152 | High |
| Microsoft.Data.SqlClient | 2.1.4 | High |
| Microsoft.Extensions.Caching.Memory | 6.0.1 | High |
| Microsoft.IdentityModel.JsonWebTokens | 6.10.0 | Moderate |
| NuGet.Common | 6.3.1 | High |
| NuGet.Packaging | 6.3.1 | Critical |
| NuGet.Protocol | 6.3.1 | High |
| System.Formats.Asn1 | 5.0.0 | High |
| System.IdentityModel.Tokens.Jwt | 6.10.0 | Moderate |
| System.Net.Http | 4.3.0 | High |
| System.Text.Json | 6.0.0 | High |
| System.Text.RegularExpressions | 4.3.0 | High |

These are package advisory matches, not proof that every vulnerable API is reachable in this application. Runtime/shared-framework resolution and development-only dependency paths need review during phase 2. No dependencies were upgraded in phase 1.

## API contract and HTTP behavior

`openapi.json` is the Swagger document downloaded from the running .NET 6 application: **135 paths, 236 operations, 59 schemas**. `api-routes.md` is its readable route inventory. Route versioning remains as copied from V1.

`http-baseline.json` records actual responses from four checks:

| Request | Observed result |
| --- | --- |
| GET /api/health | 200, `text/plain`, body `Healthy` |
| GET /swagger/v1/swagger.json | 200, `application/json; charset=utf-8` |
| GET /api/customers without credentials | 401, empty body |
| POST /api/auth/refresh-access-token with `{}` | 400, `application/problem+json; charset=utf-8`; type/title/status/traceId fields |

The capture overrides database access with a deliberately unavailable loopback connection and uses a test-only JWT key/issuer/audience. It does not call integrations or write application data. `traceId` changes per request and must be ignored when comparing responses. Health is application liveness only: Startup registers no database health check.

Windows EventLog writes were denied in this restricted session, initially preventing successful HTTP checks. The capture script disables only that logging provider for its child process. The HTTP-only loopback host also logs that no HTTPS redirect port is available. These environment adjustments are documented here rather than applied to application code.

Swagger metadata is incomplete for anonymous response objects and generic `ActionResult` endpoints. It does not capture every runtime response shape, error, or authentication requirement. Successful sign-in, refresh, tenant/session checks, business writes and report responses are **not yet verified**.

## Database findings and isolation

A read-only metadata query against the active connection in `appsettings.json` succeeded:

- The configured host is loopback; server version is **8.0.46**.
- The connected schema exposes **67 tables/views and 0 routines** to the configured account. See `database-metadata.tsv`. Routine metadata is subject to account visibility, so this alone cannot prove that no privileged account can see routines.
- `required-routines.txt` inventories **26 distinct CALL targets** in controller source after excluding block comments. Several reports require these routines; they cannot be recreated faithfully from their call signatures.
- Repository SQL dumps identify historical servers 8.0.28 and 8.0.29. No CREATE PROCEDURE/FUNCTION definitions were found in `Db`. These dumps are not an authoritative current test fixture.
- The deployed/production database version has **not** been established. The local version and old dump headers must not be reported as the deployed version.

Docker's engine pipe is unavailable. Instead, `New-MigrationTestDatabase.ps1` successfully initialized a separate native MySQL 8.0.46 data directory at `bin/phase1-mysql/data`, created `negosuite_baseline`, assigned a random local root password and shut it down cleanly. It used loopback port 33316 and `--no-defaults`, so it did not reuse or change the installed MySQL service's configuration/data. The existing `MySQL80` service remains running.

The isolated database is **empty and stopped**. Its credentials and state are in ignored `bin/phase1-mysql/client.cnf` and `state.json`; do not commit them. Current schema, routines and sanitized representative transactions are required before it is a usable business-test database. No historical or production records were imported, and no substitute routines were invented.

## Representative report capture still required

Use one fixed sanitized fixture for both .NET 6 and the migrated application. Record the fixture checksum, actual server version, routine definitions, tenant/config, test principal, report criteria, response status/content type, full JSON output, row count, and calculated totals. Encode the criteria JSON in the `criteria` query parameter; use a valid test `configUuid` and bearer token. Keep credentials and tokens out of committed artifacts.

| Report route under /api | Fixture coverage | Comparison | Status |
| --- | --- | --- | --- |
| financial-reports/trial-balance | Posted debit/credit entries and opening balances | Per-account debit/credit and total balance | Not run; routine/fixture required |
| financial-reports/gl-summary | Fixed accounting period with posted entries | Account totals and closing balances | Not run; routine/fixture required |
| receivable-reports/aging-summary | Unpaid and partially paid invoices spanning aging buckets | Bucket totals and customer balances | Not run; fixture required |
| payable-reports/aging-summary | Unpaid and partially paid bills spanning aging buckets | Bucket totals and supplier balances | Not run; fixture required |
| inventory-reports/stock-summary | Receipts, sales, adjustments and transfers | Quantity/cost/value per item and location | Not run; routine/fixture required |

Include empty-result criteria, decimal values, period boundary dates, and a second tenant. Capture actual ordering and property casing; compare monetary values without reducing stored precision. Baseline existing behavior separately from any business defect discovered. Do not infer a correct financial baseline from a successful build or an empty JSON result.

## Reproduce completed checks

From the repository root in PowerShell:

```powershell
# Explicit SDK 6 baseline; application source still targets net6.0.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Capture-MigrationBaseline.ps1 -SdkPath 'C:\Program Files\dotnet\sdk\6.0.428\dotnet.dll'

# Capture resolved packages without triggering a second restore.
dotnet list negosuite-api.csproj package --include-transitive --no-restore --format json
dotnet list negosuite-api.csproj package --vulnerable --include-transitive --no-restore --format json

# Read-only metadata; honors ConnectionString__negosuite if explicitly set.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Read-DatabaseBaseline.ps1

# One-time isolated initialization; refuses to overwrite an existing sandbox.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/New-MigrationTestDatabase.ps1
```

The HTTP script rebuilds into `bin/phase1-baseline/app`, launches only a temporary API process, records results, and stops that process in `finally`. Review new captures before replacing these committed baseline artifacts. Package audit requires NuGet network access. Database metadata requires `mysql` on PATH and appropriate read permissions. Scripts were executed with Windows PowerShell 5.1.

To resume the prepared MySQL instance after a current sanitized fixture is available:

```powershell
$sandbox = Join-Path (Get-Location) 'bin/phase1-mysql'
$state = Get-Content "$sandbox/state.json" -Raw | ConvertFrom-Json
$mysqlProcess = Start-Process -FilePath $state.serverExecutable -ArgumentList @('--no-defaults', ('--datadir="' + $state.dataDirectory + '"'), '--bind-address=127.0.0.1', "--port=$($state.port)", '--mysqlx=OFF', '--skip-log-bin', ('--log-error="' + "$sandbox/server.log" + '"')) -WindowStyle Hidden -PassThru
# Once ready, use this connection only for fixture import and verification.
mysql "--defaults-extra-file=$sandbox/client.cnf" --execute='SELECT VERSION();'
# Stop the dedicated instance when finished.
mysql "--defaults-extra-file=$sandbox/client.cnf" --execute='SHUTDOWN;'
```

Inspect any supplied dump for database-qualified names, CREATE DATABASE/USE statements and DEFINER clauses before importing. Use only the dedicated instance for restoration; do not run the repository's cleanup scripts against an existing database.

## Remaining phase 1 completion gates

Follow-up DDL export: `table-schema.sql` now contains the 67 table definitions exported read-only from the configured local database, with no row data and the connection identity removed from the dump header. It is a reference file, not a build input or automatic migration; it includes destructive DROP TABLE statements and must only be restored into an isolated test instance.

A complete export requested routines, triggers and events but failed with MySQL error 1044 on `SHOW EVENTS`. The current account therefore cannot establish a complete DDL baseline. The successful fallback explicitly excludes routines, triggers and events. Obtain a complete schema-only export from an account authorized to read all those definitions; do not interpret the metadata's zero visible routines as proof of their absence. DDL alone also does not provide the representative test transactions needed for report comparisons.

Reproduce the export with `scripts/Read-DatabaseBaseline.ps1 -ExportSchema` (complete scope, requires adequate privileges), or `-ExportSchema -ExportScope TablesOnly` (explicitly incomplete fallback). Raw exports stay under ignored `bin/phase1-baseline`; review them before adding reference files to source control. No `.csproj` inclusion is required.

1. Identify the actual deployed database environment and confirm its version read-only.
2. Obtain and restore the current sanitized schema, required routines and representative data into the isolated instance; verify all required routines and mapped objects are present.
3. Capture successful authentication, representative report responses and agreed business totals under .NET 6 using that fixture.

The runtime upgrade has not started. The artifacts above establish a reviewable baseline for the parts that could be exercised, but the full phase 1 acceptance criteria remain open until these database gates are met.
