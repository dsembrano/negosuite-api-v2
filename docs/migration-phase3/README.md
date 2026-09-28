# Phase 3: startup and compatibility verification

Implemented 2026-09-28. Startup fixes and the focused compatibility suite are complete. Full production-database and business-workflow acceptance remains open.

## Application changes

- Kept `Program.cs` and `Startup.cs` and the existing Generic Host.
- Removed the redundant `AddMvc()` registration; kept `AddControllers()` and explicitly registered authorization.
- Removed the second endpoint-mapping block. Controllers and `/api/health` are mapped once.
- Ordered request handling as HTTPS redirection, routing, request localization, CORS, authentication, authorization, endpoints. Localization now runs before controller execution and after route selection.
- Removed the unused commented WebSocket block and unused imports.
- Explicitly mapped `Customer.CreditLimit` to `decimal(20,4)`, matching the exported table DDL. The synthetic EF-generated schema previously used provider-default scale 2 and returned 1234.57 for 1234.5678. The corrected mapping passes the four-decimal round-trip test. This was an inherited mapping omission exposed by the migration tests, not a proven new .NET 10 regression. No existing database column was altered.
- Added an isolated test project to the solution. Excluded its sources/content from the API build, and excluded documentation JSON from web content output.

Middleware ordering follows [Microsoft's guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/middleware?view=aspnetcore-10.0).

## Verification results

Final test run: **12 passed, 0 failed, 0 skipped** on .NET 10. Four existing application compiler warnings remain.

| Area | Coverage |
| --- | --- |
| JWT validation | Valid token accepted; missing, expired, wrong-issuer, wrong-audience and wrong-signature tokens rejected |
| CORS | Unauthenticated preflight to a protected API route accepts the frontend's authorization/config/session headers |
| Localization | Test-configured French request culture is visible inside the controller |
| JSON | Camel-case fields, nulls, date-time format and four-decimal JSON numbers remain stable under French culture |
| Config filter | Valid JWT does not bypass a missing config UUID; unknown UUID rejected in MySQL test |
| Problem Details | Empty refresh request returns 400, application/problem+json, status/title/traceId fields |
| EF Core model | Complete application model initializes with Oracle's EF Core 10 provider |
| SQL generation | Config and latest-session LINQ queries translate; parameterized GetAccountRecap CALL can be generated without executing it |
| Real MySQL fixture | Model-generated schema created on MySQL 8.0.46; synthetic records saved and queried |
| Sign-in | Wrong password rejected; valid credentials produce access/refresh tokens and a session record |
| Issued JWTs | Both sign-in and refresh access tokens accepted by the real JWT middleware |
| Refresh | Valid token accepted; unknown, revoked and expired refresh tokens rejected; existing refresh-token reuse contract retained |
| Session filtering | Current web session accepted; old session rejected after a newer log is inserted |
| Customer query | Actual customer endpoint executes against MySQL; criteria select one of two synthetic tenants; decimal credit limit retains four places |
| Date persistence | datetime(6) value round-trips through the actual provider with microseconds preserved |

The application HTTP capture also passed all four existing checks: health 200, Swagger 200, anonymous customers 401, and empty refresh 400. OpenAPI comparison with the phase 2 capture found **236 operations, 59 schemas, zero metadata leaf differences**. Comparison excludes empty-container and property-order differences, as documented by the comparison script.

The RFC 9110 Problem Details documentation URI introduced by .NET 10 is retained. A read-only search of the current sibling web source found no RFC/type URI dependency; the authentication interceptor handles HTTP status 401. No live browser test was performed.

## Isolation and repeatability

```powershell
dotnet restore negosuite-api.sln
# Runs 11 database-independent cases; explicitly skips the MySQL case without its environment variable.
dotnet test negosuite-api.sln -c Release --no-restore

# Starts the dedicated local MySQL instance, runs all 12 cases, then stops it.
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Phase3MySql.ps1

powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Capture-MigrationBaseline.ps1 -OutputDirectory bin/phase3-verification -NoRestore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Compare-MigrationContract.ps1 -Baseline bin/phase2-verification/openapi.json -Candidate bin/phase3-verification/openapi.json -OutputPath bin/phase3-verification/contract-comparison.json
```

The MySQL runner requires the sandbox prepared by `New-MigrationTestDatabase.ps1`. It checks the sandbox path/address and requires unused loopback port 33316. It passes credentials through a process environment variable, restores that variable afterward, and uses a generated `negosuite_phase3_<guid>` schema. The test deletes only that generated schema in its cleanup; the runner shuts down its owned MySQL process. No application connection string is read by the tests. Test-only controllers are loaded solely in TestServer and do not appear in the shipped Swagger document.

The initial sandboxed MySQL attempt encountered Windows TLS credential-provider restrictions. The final test ran with authorized execution outside that restriction, using `SslMode=Required`. No application TLS setting was weakened. An initial synthetic-connection experiment without TLS could not perform MySQL authentication and was replaced with the TLS-required setting.

Raw TRX and HTTP evidence are in ignored `bin/phase3-verification`; the committed `verification.json` and `contract-comparison.json` summarize the successful results.

## Remaining acceptance work

- The MySQL fixture is generated from the current EF model, not restored from the production DDL. It establishes provider/auth/query behavior, not full deployed-schema compatibility.
- No stored procedure was executed, invented or replaced. The complete schema with routine definitions and representative transactions is still required for report totals, journal/payment posting, inventory movements, and rollback comparisons.
- These tests preserve and exercise existing config/session checks; they are not proof of tenant authorization. Existing access tokens contain no user/tenant claims, config UUIDs are checked for existence, and `X-UserLog` is optional and supplied by the client. Binding identity to tenant/session is a separate inherited authorization concern; it was not silently redesigned during the framework migration. The customer test confirms filtering by supplied criteria, not rejection of another tenant's criteria.
- External integrations, production credentials, browser behavior and deployment remain unverified. Docker/deployment work is still phase 4.
