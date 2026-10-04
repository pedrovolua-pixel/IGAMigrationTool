# Azure development application package

Status: Local bootstrap package preparation; no Azure activation
Owner: Platform engineer / technical owner
Governing scope: [application package cycle](../../plans/active/azure-application-package-cycle.md), [approved platform](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md), [identity/session design](../security/health-assessment-identity-session-design.md), and [G1 implementation plan](../../specs/003-health-assessment/implementation-plan.md).

## What is runnable

`src/server/hosts/AzureDevelopmentBootstrap` is a framework-only .NET development diagnostic executable with two explicit roles:

- `--azure-development-bootstrap --role web`: binds port 8080; exact `GET /health/live` returns a static bootstrap liveness response, exact `GET /health/ready` returns 503 / `product_not_enabled`, every other request is refused. No login, UI, API, session, customer or source operation is enabled.
- `--azure-development-bootstrap --role worker`: remains inert until graceful shutdown. It does not receive/acknowledge messages, touch SQL, execute assessment work or report completed work.

Missing, duplicate or unknown startup arguments are refused. Configuration/log providers are cleared; request bodies, headers and environment configuration are not exported. Liveness establishes only that this process is running. Readiness deliberately does not claim product or downstream-service health. This is a container/hosting preparation package, not the implemented BFF or production worker. The existing consultant demo keeps its loopback-only checks and is not part of this image.

## Stored sources

| Source | Purpose |
|---|---|
| [`AzureDevelopmentBootstrap`](../../src/server/hosts/AzureDevelopmentBootstrap/) | Locked framework-only bootstrap host. |
| [`azure-development-bootstrap.Dockerfile`](../../infra/containers/azure-development-bootstrap.Dockerfile) | Approved SDK/runtime versions plus mandatory immutable base digests; non-root UID 1654, exec entrypoint. |
| [`azure-development-bootstrap.Dockerfile.dockerignore`](../../infra/containers/azure-development-bootstrap.Dockerfile.dockerignore) | Docker-recognized adjacent context allowlist; excludes customer material, credentials, local demo and unrelated repo files. |
| [`azure-development-bootstrap.images.lock.json`](../../infra/containers/azure-development-bootstrap.images.lock.json) | Actual public MCR manifest-list digests and target platform; not an application image digest. |
| [`build-development-bootstrap.py`](../../infra/containers/build-development-bootstrap.py) | Builds from validated lock into a bounded local tag; no registry push. |
| [`pilot-development-bootstrap-apps.bicep`](../../infra/bicep/pilot-development-bootstrap-apps.bicep) / [module](../../infra/bicep/modules/pilot-development-bootstrap-apps.bicep) | Private test hosting, existing environment/registry/identities, digest image binding, no access grants or credentials. |
| [`pilot-dev-bootstrap.parameters.example.json`](../../infra/bicep/environments/pilot-dev-bootstrap.parameters.example.json) | Intentionally unresolved deployment example; zero image digest is refused. |
| [`azure-application-package.yml`](../../.github/workflows/azure-application-package.yml) | Partial CI build/process/container/private-template checks; no Azure credentials or deployment. |

The SDK `10.0.401-noble` and ASP.NET runtime `10.0.12-noble-chiseled-extra` manifest-list bytes were fetched from Microsoft Container Registry on 2026-10-02 UTC; their SHA-256 matched the registry response digest and both include Linux AMD64. Build targets `linux/amd64`; base inputs are immutable. Source/framework locks alone do not prove image safety or a signed release.

## Build and local verification

Use pinned SDK 10.0.401. No additional NuGet packages are required.

```sh
dotnet restore src/server/hosts/AzureDevelopmentBootstrap/AzureDevelopmentBootstrap.csproj --locked-mode
dotnet format src/server/hosts/AzureDevelopmentBootstrap/AzureDevelopmentBootstrap.csproj --verify-no-changes --no-restore
dotnet publish src/server/hosts/AzureDevelopmentBootstrap/AzureDevelopmentBootstrap.csproj -c Release --no-restore --no-self-contained -o /tmp/iga-bootstrap /p:UseAppHost=false
python3 tests/integration/AzureDevelopmentBootstrap/verify.py --dotnet "$(command -v dotnet)" --assembly /tmp/iga-bootstrap/AzureDevelopmentBootstrap.dll
python3 infra/containers/build-development-bootstrap.py --check-inputs-only
```

On a Docker-capable Linux AMD64 runner:

```sh
python3 infra/containers/build-development-bootstrap.py
python3 tests/integration/AzureDevelopmentBootstrap/verify.py --docker-image iga-azure-development-bootstrap:local
```

The process test reserves port 8080 for its own short-lived children and refuses to use an already occupied port. Container checks use a local-only published port with read-only filesystem, dropped capabilities and no-new-privileges; the worker has no published port. Neither test prints protected configuration. The GitHub workflow supplies the container engine unavailable on this developer Mac. An unexecuted workflow or image scan is not a passing result.

## Private hosting and later deployment preflight

The template binds one digest to the approved `iga/azure-development-bootstrap` repository on an existing private Premium registry. It checks East US 2/internal Consumption environment and closed registry settings; requires distinct existing workload identities and app names. Web ingress is internal-only with insecure ingress disabled. Worker has no ingress. Each role is bounded to one replica with 0.25 CPU / 0.5 GiB solely for a priced development window. No role assignment, secret/env payload, federation, output identifier, telemetry export or database migration is added. ARM checks do not prove network routing, DNS or effective authorization.

Before deploying:

1. Reconcile delayed Azure charges and prepare a fresh owner-approved priced window with exact cleanup. Recheck region capacity; recreate the corrected foundation and handle the recoverable vault name without purge.
2. Supply real existing private environment/registry and distinct identity names. Review and configure only the required image-pull permission per identity; verify registry ARM-audience token acceptance and the private pull path. Existing queue grants do not grant registry access.
3. Build from the recorded source and base locks, run container/runtime checks plus required image vulnerability/license/SBOM/provenance and release-integrity checks, and preserve attributable results. No image is eligible for promotion merely because CI builds it.
4. Publish through an approved builder/network path into the private ACR. The ordinary GitHub runner in this workflow is only a build/test runner; it is not permitted through the private registry boundary. Do not open public access or add broad IP rules to make pushing convenient.
5. Obtain the actual **published manifest digest** and replace every example input. A local Docker config digest/image ID is not the registry manifest digest. The zero digest example must remain undeployable.
6. Validate/preview exact changes, deploy only under the new session approval, prove private pull/startup and intended network boundaries, preserve sanitized evidence and remove the exact disposable apps/resources on time.

The module's configuration checks are not a reviewed authorization decision, proof of registry pulls or a replay of the foundation. Its referenced private environment/registry/identities currently do not exist after cleanup. There are no live workload grants or cloud applications created by this preparation.

## Product activation dependencies

Requested from technical/security owners and identity administrator: implement/review the actual BFF routes, supported authentication library, dedicated federated BFF identity, authoritative server-side sessions, CSRF/revocation and per-customer data-plane authorization; configure actual HTTPS endpoints, licensed pilot users and reviewed Conditional Access scope. Existing [local demo integration review](local-consultant-demo.md#production-integration-review) records the production contract dependency. Add real durable worker/image and tested broker/database/blob permissions under the approved contracts.

Done when the real hosted application passes the approved sign-in/trust/customer-denial/recovery tests and is reviewed before activation. This bootstrap cannot close that task or G1. Collector enrollment/delivery, AI and renderer retain their separate disabled gates. No new human identity, spending or retention choice is silently selected by this package.

## Primary implementation guidance

[Microsoft .NET non-root containers](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/containers), [Container Apps image pull with managed identity](https://learn.microsoft.com/en-us/azure/container-apps/managed-identity-image-pull), [stable Container Apps template schema](https://learn.microsoft.com/en-us/azure/templates/microsoft.app/2026-01-01/containerapps), [Bicep fail function](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-functions-flow-control), [Dockerfile-specific ignore files](https://docs.docker.com/build/building/context/#filename-and-location), and [GitHub hosted runner images](https://github.com/actions/runner-images).

## Executed package result

[Partial package CI run 36953664310](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36953664310) passed actual Linux AMD64 Docker build and protected web/worker runtime smoke on source `ae785af008f527b0396c8291d50f69743bf8575d`. This adds executed image/runtime evidence to the local build/format/publish/process and 44 unsafe-hosting checks. [Sanitized evidence](evidence/azure-application-package-20261001.json) records the real base locks and local image config digest. The test image is not published to ACR or retained as a release artifact; rebuild from the stored recipe before supply-chain checks and approved private publication. No SBOM, license/image vulnerability scan, release provenance/signature, private Azure pull, real BFF/worker or G1 acceptance follows from this partial run.
