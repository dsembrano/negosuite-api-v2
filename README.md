# negosuite-api

The API now targets .NET 10. Use the SDK selected by `global.json`.

```powershell
dotnet restore negosuite-api.csproj
dotnet build negosuite-api.sln -c Release --no-restore
```

Migration progress and verification: [Phase 1 baseline](docs/migration-baseline/README.md) and [Phase 2 upgrade](docs/migration-phase2/README.md).

[Phase 3 startup fixes and compatibility tests](docs/migration-phase3/README.md) now cover JWT/CORS/localization, EF model/query generation, and synthetic MySQL sign-in/refresh/customer/session flows. Run `dotnet test negosuite-api.sln -c Release` for the database-independent suite; use `scripts/Test-Phase3MySql.ps1` for the isolated MySQL case.

[Phase 4 runtime and deployment preparation](docs/migration-phase4/README.md) provides the .NET 10 container, Kubernetes port/probe updates, shared debugger configuration, and CI checks. Supply deployment settings externally; published artifacts exclude local appsettings files. Phase 5 local database acceptance is recorded below; V2 deployment-target configuration and staging acceptance remain release gates.

```powershell
docker build --pull -t negosuite-api-v2:phase4 .
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Container.ps1
```

[Phase 5 database verification](docs/migration-phase5/README.md) adds opt-in read-only checks against the configured localhost database and comparisons with the .NET 6 baseline. Customer/journal checks and seven populated report samples match across runtimes. All required procedures are present; four user-accepted schema gaps are excluded. Phase 5 is complete based on automated parity checks and user acceptance of local transaction testing; independent forced-failure rollback coverage remains limited. Run `scripts/Test-DevelopmentDatabase.ps1 -CompareNet6` to refresh the evidence.

# How to Enable Push-to-Deploy on DigitalOcean Kubernetes Using GitHub Actions
https://docs.digitalocean.com/products/kubernetes/how-to/deploy-using-github-actions/

Customer API changes: [refactoring, compatibility, and opt-in pagination](docs/customers-refactoring.md).

Temporary local migration setting: [parallel web sessions and reactivation instructions](docs/temporary-web-sessions.md). Re-enable single-web-session enforcement after Angular side-by-side testing.

Supplier API changes: [refactoring, compatibility, and opt-in pagination](docs/suppliers-refactoring.md).

Items API changes: [refactoring, compatibility, filtering, sorting, and pagination](docs/items-refactoring.md).

Item Categories API changes: [refactoring, compatibility, filtering, sorting, and pagination](docs/item-categories-refactoring.md).
