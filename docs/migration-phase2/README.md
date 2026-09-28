# Phase 2: .NET 10 framework and dependency upgrade

Implemented and verified on 2026-09-25. This completes the framework/dependency step, not database regression testing or deployment readiness.

## Changes

- The API targets `net10.0`. `global.json` selects installed SDK `10.0.203`, permits newer patches within that SDK feature band, and rejects preview SDKs.
- The existing `Program`/`Startup` hosting structure and controller/business code remain in place.
- Updated the OpenAPI namespace in `Startup.cs` to compile against Swashbuckle 10 / Microsoft.OpenApi 2.
- Removed legacy ASP.NET Core Routing/WebSockets package references; these APIs are supplied by the shared framework.
- Removed the unused SQL Server provider. Startup uses Oracle's MySQL provider, and no SQL Server API usages were found in application source.
- Removed the unused ASP.NET controller scaffolding package. Retained EF tooling with `PrivateAssets=all` for the existing database development workflow.
- Added an explicit Newtonsoft.Json dependency because application source directly uses it; MVC still uses its existing serializer configuration.
- Updated AWS S3 within its existing 3.x major version to avoid coupling this migration to an AWS SDK 4 API migration. External storage behavior remains to be tested.

| Direct package | Version |
| --- | --- |
| AWSSDK.S3 | 3.7.511.8 |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 |
| Microsoft.EntityFrameworkCore.Tools | 10.0.12 |
| Microsoft.EntityFrameworkCore.Relational | 10.0.12 |
| MySql.EntityFrameworkCore | 10.0.9 |
| Newtonsoft.Json | 13.0.4 |
| Swashbuckle.AspNetCore | 10.2.3 |

All resolved EF Core assemblies are on 10.0.12. Oracle's provider resolves MySql.Data 26.7.0. Versions were checked against the live NuGet feed before restore. See the provider's [package dependency metadata](https://www.nuget.org/packages/MySql.EntityFrameworkCore/10.0.9) and the [Swashbuckle migration guide](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md).

## Verification

| Check | Result |
| --- | --- |
| Restore updated dependencies | Passed |
| Release rebuild with SDK 10.0.203 | Passed: 0 errors, same 4 existing compiler warnings |
| Start application on locally installed .NET/ASP.NET Core 10.0.7 | Passed |
| HTTP baseline: health, Swagger, anonymous protected request, empty refresh request | 4/4 passed |
| API route/method comparison with phase 1 | Same 236 operations across 135 paths; none added or removed |
| OpenAPI schema inventory | Same 59 schemas; no schema leaf differences |
| Direct/transitive dependency inventory | 7 direct, 47 transitive packages (previously 9/189) |
| NuGet audit, including transitive dependencies | No known vulnerable package matches reported (previously 12 packages) |

Evidence: `build.txt`, `http-baseline.json`, `dependencies.json`, `vulnerabilities.json`, and `contract-comparison.json`. The audit covers NuGet packages, not the servicing status of the installed shared runtime, operating system, or future advisories. The local 10.0.7 runtime differs from the 10.0.12 package servicing level; phase 4 must align the deployment with current supported runtime/container patches.

Observed compatibility differences:

- Swagger emits OpenAPI `3.0.4` instead of `3.0.1`, changes 236 success descriptions from `Success` to `OK`, and adds 45 top-level controller tags. These account for all 282 compared metadata leaf differences. The comparison ignores empty containers and property ordering; it is not a complete proof of wire compatibility.
- The empty refresh request still returns HTTP 400 and the same Problem Details fields. Its `type` URI now references RFC 9110 section 15.5.1 instead of RFC 7231 section 6.5.1. `traceId` varies per request as before. Consumers should not treat the documentation URI as a business error code; this observed difference is retained for phase 3 review.
- No successful database-backed authentication, EF model/query execution, business posting, reports, or external integrations were exercised by these database-independent checks.

## Repeat verification

```powershell
dotnet restore negosuite-api.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Capture-MigrationBaseline.ps1 -OutputDirectory bin/phase2-verification -NoRestore
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Compare-MigrationContract.ps1
dotnet list negosuite-api.csproj package --include-transitive --no-restore --format json
dotnet list negosuite-api.csproj package --vulnerable --include-transitive --no-restore --format json
```

The capture script now defaults to `bin/migration-current` so later runs do not overwrite the phase 1 working capture. Committed phase 1 evidence is unchanged. To rebuild the historical .NET 6 baseline, use a separate checkout of its recorded source commit; selecting SDK 6 cannot build this now-upgraded project.

## Next steps and limits

- Phase 3: exercise EF Core model creation and query translation, JWT issuance/validation/refresh, tenant/session controls, serialization and middleware behavior. Review the observed Problem Details URI change with frontend usage.
- Complete the database baseline using a current full DDL export with routine/trigger/event visibility and sanitized representative transactions. The existing table-only export cannot verify stored-procedure behavior or financial totals.
- Phase 4: update Docker images, port bindings/Kubernetes targets, debugger paths and deployment automation. The existing Dockerfile still references .NET 6 and cannot build this upgraded project; do not use it for deployment until that phase is implemented.
- No database writes, migrations, deployment, or changes to the user's `appsettings.json` were made in this phase.
