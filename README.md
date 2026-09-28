# negosuite-api

The API now targets .NET 10. Use the SDK selected by `global.json`.

```powershell
dotnet restore negosuite-api.csproj
dotnet build negosuite-api.sln -c Release --no-restore
```

Migration progress and verification: [Phase 1 baseline](docs/migration-baseline/README.md) and [Phase 2 upgrade](docs/migration-phase2/README.md).

[Phase 3 startup fixes and compatibility tests](docs/migration-phase3/README.md) now cover JWT/CORS/localization, EF model/query generation, and synthetic MySQL sign-in/refresh/customer/session flows. Run `dotnet test negosuite-api.sln -c Release` for the database-independent suite; use `scripts/Test-Phase3MySql.ps1` for the isolated MySQL case.

Docker and deployment configuration still target .NET 6 and require the planned phase 4 update before deployment. Database compatibility checks remain pending the complete schema and test fixture.

# How to Enable Push-to-Deploy on DigitalOcean Kubernetes Using GitHub Actions
https://docs.digitalocean.com/products/kubernetes/how-to/deploy-using-github-actions/
