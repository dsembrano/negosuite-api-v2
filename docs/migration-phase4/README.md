# Phase 4: runtime and deployment preparation

Updated 2026-09-28. No registry push, namespace creation, secret update, ingress change, or cluster deployment was performed.

Verified: Release publish passed; 13 compatibility cases passed, with the opt-in MySQL case skipped; final Docker build passed; all production container smoke checks passed on .NET/ASP.NET Core 10.0.12 with UID 1654. The four pre-existing compiler warnings remain. Offline Kubernetes rendering and port/probe/namespace checks passed. `verification.json` records the verification boundaries.

## Implemented

- Multi-stage .NET 10 Linux Dockerfile; Release publish; non-root runtime user; explicit HTTP binding on port 8080.
- SDK selection rolls forward within stable .NET 10 feature bands so the current `sdk:10.0` image and CI can use serviced SDKs. It does not permit .NET 11 or preview SDKs. Local minimum remains 10.0.203.
- Docker context uses an allowlist. Local settings, secret manifests, database dumps, test data, Git history and existing build outputs are excluded. Normal Release publishing also excludes `appsettings*.json`; local `dotnet run` still reads the workspace settings.
- Removed compiled storage credentials from the APK controller. Downloads now require `Storage__AccessKey`, `Storage__SecretKey`, `Storage__Region`, and `Storage__Bucket`. Configure these for local APK testing as well as deployment; old hardcoded values are not used as fallbacks.
- Added trusted-forwarded-header handling before HTTPS redirection. `ReverseProxy__KnownProxies__0` / `ReverseProxy__KnownNetworks__0` accept explicit proxy IPs/CIDRs. Unknown remote proxies remain untrusted. No cluster CIDR was guessed and no trust-all switch was enabled. See [Microsoft proxy guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
- Kubernetes container port is named `http` at 8080. ClusterIP service retains port 80 and targets `http`. Startup, readiness and liveness probes call `/api/health`. The container disallows privilege escalation and drops Linux capabilities.
- VS Code launch path now uses `net10.0`. Shared launch/tasks files are no longer ignored; other personal VS Code settings remain ignored.
- Added GitHub Actions build/test/publish/container-smoke validation on push, pull request and manual dispatch. It has read-only repository permissions and does not publish an image or deploy.
- Added a manifest preparation script requiring an immutable registry digest and explicit dedicated V2 namespace. It writes a reviewable file without contacting a cluster. The stale reference to a missing production workflow was removed from the project.

The shared `DoConfig/ingress.yml` still describes existing production services, including other applications. It was not changed or applied. V2 hostname, namespace, registry and TLS configuration must be selected before rollout.

## Runtime configuration

Use `DoConfig/runtime.env.example` as the configuration-key reference, not as a ready-to-use secret. Supply actual values through the selected deployment secret store. Kubernetes expects `negosuite-api-secret` in the chosen V2 namespace.

The API image has no database/JWT/integration credentials. Set the connection string and JWT key/issuer/audience for API operations. Supply SMTP, storage, Metabase and DeepSeek values for their respective features. Existing application settings and the copied secret manifest have not been altered; do not automatically reuse V1 deployment credentials or apply its manifest to the V2 target.

TLS must terminate at the chosen ingress/load balancer, which must enforce HTTPS externally. Configure its actual trusted proxy addresses so the application sees the correct original scheme. The container exposes HTTP only and has no server certificate. Health probes use internal HTTP; the existing health endpoint measures application liveness, not database/routine readiness. Cluster HTTPS routing and database readiness remain staging checks.

## Local verification commands

```powershell
dotnet publish negosuite-api.csproj -c Release -o bin/phase4-publish /p:UseAppHost=false
dotnet test negosuite-api.sln -c Release
docker build --pull -t negosuite-api-v2:phase4 .
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Container.ps1
```

Container smoke tests use dummy JWT settings and an intentionally unreachable database, bind the host port to loopback only, and stop the owned container afterward. They check health 200, anonymous customers 401, empty refresh 400, production Swagger 404, non-root execution and excluded configuration files. They do not prove business/database/integration behavior.

The ordinary compatibility run skips the opt-in isolated MySQL test unless its environment is configured; phase 3 documents the full synthetic MySQL run. VS Code configuration was inspected, but the interactive debugger was not launched. The workflow file was prepared locally; no GitHub Actions run has been triggered.

## Deployment handoff

1. Select V2 namespace, hostname, image registry and TLS termination. Create a V2-specific secret with the required settings through the normal secret-management process.
2. Complete phase 5 database/business acceptance and resolve the inherited tenant/session authorization concern before production release.
3. Build and publish the reviewed image to the chosen registry when release is authorized; record its digest. Moving `10.0` base tags receive servicing updates when rebuilt with `--pull`, so retain the resulting image digest for deployment and rollback.
4. Prepare the workload manifest using the real values:

   ```powershell
   ./scripts/Prepare-KubernetesDeployment.ps1 -Namespace YOUR_V2_NAMESPACE -Image 'REGISTRY/IMAGE@sha256:YOUR_DIGEST'
   ```

5. Review `bin/phase4-verification/deployment.yml`, perform server-side dry-run against the explicitly selected cluster/context, and configure a separate V2 ingress pointing to service port 80 in that namespace. Do not apply the copied shared ingress wholesale.
6. On authorized deployment, verify rollout health, external HTTPS, authentication and database operations. Keep the previous immutable image digest/manifest for rollback. No schema migration is included in this deployment change.

Local test results are summarized in `verification.json`. Offline Kubernetes rendering checks YAML/resource transformation only; live API-server validation, scheduling, secret resolution, registry pull permissions and ingress routing are not verified locally.
