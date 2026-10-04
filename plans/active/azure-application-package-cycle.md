# Azure application package preparation

Status: VERIFIED — bounded bootstrap preparation; product integration and G1 NOT VERIFIED
Owner: Azure setup coordinator
Date: 2026-10-01
Source base: `75e654d`

## Authorized outcome

The owner requested the next Azure step: prepare deployable application images/templates. This cycle implements a bounded platform bootstrap package under approved ADR-0004, Milestone 1 and the platform/supply-chain slice of the local build. The only existing consultant executable is deliberately loopback-only and synthetic; it must not be relaxed or deployed as an authenticated BFF.

Provide a separate development-only bootstrap executable, pinned container recipe and private Container Apps hosting template. The executable has a web-probe role (process liveness plus explicit product-not-ready response; all other requests refused) and an inert worker role (no queue, SQL, customer or product processing). It requires an explicit development bootstrap argument; neither role is the completed product BFF/worker. No product HTTP contract, sign-in, consent, federation, public ingress, new data grants, secret material, SQL migration or telemetry export is introduced. Later product hosts replace this bootstrap only under their existing reviewed contracts and gates.

## Bounded parallel packets

| Packet | Owner | Paths | Completion |
|---|---|---|---|
| PKG-01 | Bootstrap host / packaging worker | `src/server/hosts/AzureDevelopmentBootstrap/`, `infra/containers/azure-development-bootstrap.Dockerfile`, `infra/containers/azure-development-bootstrap.Dockerfile.dockerignore` | Explicit-start inert web/worker roles; locked build/publish; pinned approved base versions with required immutable digest build inputs; non-root runtime; minimal allowlisted context. No changes to local demo. |
| PKG-02 | Hosting worker | `infra/bicep/modules/pilot-development-bootstrap-apps.bicep`, `infra/bicep/pilot-development-bootstrap-apps.bicep`, `infra/bicep/environments/pilot-dev-bootstrap.parameters.example.json` | Stable approved Container Apps API; existing internal Consumption environment/private registry; digest image only; distinct existing identities; web ingress internal only; worker no ingress; bounded replicas; no grants/secrets/outputs/collection. Build/lint and focused negative policy checks. |
| PKG-03 | Independent verification worker | `tests/infrastructure/AzureApplicationPackage/`, `tests/integration/AzureDevelopmentBootstrap/` | Independently authored process refusal/route/worker checks and compiled-hosting drift checks; review other packets; no shared or canonical edits. |
| PKG-04 | Coordinator | Shared workflow/solution only if needed; docs/canonical records/private site | Integrate reviewed work, run applicable locked restore/format/build/process/Bicep/secret checks; prepare image build/smoke workflow if no local container engine; disclose unrun image/runtime/scan gates. |

Each worker uses a distinct worktree/branch from the same base. Bootstrap test paths are internal development diagnostics, not product routes. No SDK or NuGet dependency is needed beyond the approved .NET framework.

## Verification and limits

Reject absent/unknown startup modes. Web liveness indicates only process health; readiness must not imply working auth/data/service dependencies. Refuse non-probe routes and methods without logging request bodies/headers or protected configuration. Worker termination must be graceful; it never reports product work completion.

Check immutable image binding, disabled public/worker ingress, no secret/env payloads, distinct identities, Consumption profile, stable API, replica/resource bounds and no added role grant. Build/publish standalone host with pinned SDK, check formatting and packages; run actual child-process HTTP/shutdown/refusal integration tests. Execute pinned Bicep build/lint and independent template drift suite. Image build/run/scan, SBOM and deployed private image pulls remain NOT VERIFIED until actually executed by a container-capable runner. No Docker engine is installed on this developer machine; do not claim container success from `dotnet publish`.

## Deferred next execution

Before paid recreation: reconcile delayed charges, fresh allowance/capacity check, exact priced session and cleanup scope; resolve vault name reservation without purge. Before application activation: approved BFF routes/session/federation and per-customer authorization, actual pilot users/Conditional Access scope, effective identity/registry/data grants, full corrected foundation replay and live G1 tests. This package cannot satisfy those dependencies.

Results and independent review will be recorded before this cycle closes. Canonical status and existing owner-private board must be published in the same work cycle.

## Integrated local preparation evidence

PKG-01 source `c8afd85` integrated as `539be16`; PKG-02 `0cb2563` integrated as `cfd8434`; independent PKG-03 `1f3ecb8` integrated as `39e4e2a`. The verifier found encoded-path alias acceptance; the host author corrected raw-target checks before the passing process suite. Host/template authors and verifier kept explicit path ownership. The host worker independently reviewed the coordinator build helper, real MCR input lock and partial workflow with no material remaining source defect. Agent engineering review is not human gate acceptance.

Coordinator executed SDK 10.0.401 locked restore, full 25-project Release build (zero warnings/errors), full-solution format verification, standalone publish, seven architecture policy/four scan cases, framework-only NuGet vulnerability inventory, Bicep 0.47.16 build/lint root/module, 44 unsafe hosting mutations, actual process suite (eight startup refusals, two probes, 31 method/path refusals, web/worker SIGTERM and no worker HTTP/protected logs), locked-input preflight, documentation links/whitespace and whole working tree redacted Gitleaks. The formatter initially failed sandbox IPC; rerun with required local pipe access passed. No product route or customer access was enabled.

The package is locally reviewable; the separate partial GitHub container workflow passed as recorded below. Image vulnerability/license scans, SBOM, release provenance/signatures, registry publication/pulls, ARM runtime fail-guard execution, full corrected foundation replay and all live G1 evidence remain NOT VERIFIED until actually run. No paid Azure deployment or new permissions were applied. The feature remains IMPLEMENTING.

## Executed container CI

[Azure package run 36953664310](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36953664310) passed on source `ae785af008f527b0396c8291d50f69743bf8575d`. Logs confirm an actual Linux AMD64 image build from the locked MCR bases and passing web/worker container execution: non-root UID 1654, read-only filesystem, all capabilities dropped, no-new-privileges, bounded resources, loopback-only web publication, isolated inert worker and graceful shutdown. Process refusal tests and all 44 compiled-hosting mutations also passed. This supersedes the initially queued image build/runtime result; image vulnerability/license scans, SBOM, release provenance/signatures and cloud pulls remain unverified. The local image config digest is metadata only, not a registry manifest digest or deployable promotion proof.

[Existing partial bootstrap run 36953664189](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36953664189) passed all configured Linux and Windows 2022/2025 jobs on the same tested code source. No paid resources were created. The exact source recipes/templates and sanitized evidence are stored in the repo; independently reviewed workers are finished.

The three clean temporary worker worktrees were removed after integration/review; commits remain recoverable. The bounded package preparation is verified, with the product/activation/supply-chain dependencies above still open. Private board publication is confirmed below for this canonical snapshot.

## Confirmed private board publication

The existing owner-private board source `0decc48488a55b3a51b0835a47dd3cc32f5fab66` was pushed/packaged from the checked source, with repository snapshot `7013a07de4e1ae91319be565927f0c9bb2411351`. Native owner-private deployment `appgdep_6abf126ec7a88191b41311220f01b539` succeeded at 2026-10-02T02:09:56.977779+00:00. The board now summarizes the passing bootstrap container checks and keeps actual BFF/worker, supply-chain, private-pull and live gate tasks open with requested roles/source links/completion conditions. The concurrent cycle-five proposal and explicit owner dependency were preserved.

Desktop 1920px/mobile 390px preview checks found no page/card overflow or broken internal anchors; relative repository links were verified against the exact snapshot, and screenshots were visually inspected. The source/archive contains no protected identifiers or payloads and its redacted secret scan passed. Native private publication confirms the unchanged audience; live browser navigation was not repeated merely to finish publication. This closes the bounded preparation work cycle without changing G1–G9, customer authority or product activation.
