# Local pilot parallel cycle 01

Status: Engineering packets verified; coordinator publication in progress
Owner: Coordinator / technical owner
Approval: Repository owner approved the coordinator-plus-three-workers model on 2026-10-01. Packet scopes below refine existing approved local implementation.
Baseline: `f6e37b937dc2cf266ce24dee8a98d735c9c161b7`
Last updated: 2026-10-01

Governing records: [local build](one-identity-local-pilot-build.md), [parallel workflow](../../docs/development/parallel-agent-workflow.md), feature [001 product](../../specs/001-data-ingestion/product-spec.md), [001 technical](../../specs/001-data-ingestion/technical-spec.md), [001 implementation](../../specs/001-data-ingestion/implementation-plan.md), [001 tests](../../specs/001-data-ingestion/test-plan.md), feature [003 product](../../specs/003-health-assessment/product-spec.md), [003 technical](../../specs/003-health-assessment/technical-spec.md), [003 implementation](../../specs/003-health-assessment/implementation-plan.md), and [003 tests](../../specs/003-health-assessment/test-plan.md).

## Objective

Exercise the approved coordination workflow with three isolated workers. Deliver an idempotent collector checkpoint retry, a synthetic composition of capability locking and coverage projections, and independent integration verification. This cycle advances the runnable synthetic assessment foundation; a baseline reader, trusted inventory planner, durable run, authorization, application UI and full end-to-end pilot remain separate work.

## Work packets

| Packet | Worker / paths | Traceability | State |
|---|---|---|---|
| C1 | Collector; `src/collector/CollectorHost/EncryptedCheckpointStore.cs`, `tests/unit/CollectorHost.Tests/CheckpointStoreChecks.cs` | ING-PILOT-002/003; FR-ING-12/16/26; AC-ING-4; TP-ING-004 recovery subset | VERIFIED |
| A1 | Assessment; new `src/server/modules/AssessmentOrchestration/CapabilityBoundCoverageProjector.cs`, existing assessment unit test host | FR-HAS-1/2/8/18/23/47; TP-HAS-001/007 composition subset | VERIFIED |
| V1 | Verification; new `tests/integration/SyntheticPilotFlow.Tests/` project, host and lock file | FR-HAS-2/8/18/23; AC-HAS-1/7; TP-HAS-001/007 composition subset; IP-HAS-005 | VERIFIED |
| O1 | Coordinator; project/solution configuration, canonical docs, integration and private site | Local build completion accounting and AGENTS.md evidence/site obligations | RUNNING |

### C1 acceptance

An authenticated, context-valid checkpoint save with exactly the existing ordered ledger validates all existing run-directory bytes and returns without rewriting ciphertext or creating a temporary file. Identical retry at the exact existing byte cap succeeds. A narrowed cap and unrelated bytes over the cap still reject. Appends continue to reserve transient replacement bytes; changed pages, incompatible context/key and corrupted data remain rejected. No schema, retention policy, encryption profile or live source path changes.

### A1 acceptance

Compose the existing capability start guard and coverage projectors using a trusted capability/baseline descriptor and caller-supplied trusted, already-authorized plan/results. Capability denial returns no projection. Invalid or incomplete coverage returns no combined projection. Successful output includes the exact version lock, completion classification, terminal counts, executable numerator/denominator and gap limitations. Cases cover mixed pass/finding/not-applicable, explicit gaps, version/suspension mismatch, empty/missing/duplicate/unexpected/malformed/unexplained input, zero applicable units and 100,000 planned units. Do not infer baseline eligibility, applicability, authorization, persistence or run transitions. The coordinator owns the existing module's project reference to AssessmentCoverage.

### V1 acceptance

Provide independently expected synthetic integration cases at the capability/coverage boundary, then review C1/A1 patches against their acceptance cases and specification boundaries. Record exact executed evidence. No fixture outcome may silently redefine product semantics, and no partial synthetic check completes TP-HAS-001/007 or a gate.

The standalone integration host has no new external dependencies. Versioned fixture cases cover matching fixture-verified exact tuples, deterministic locks under module reordering, partial progress, all ten terminal states (total 10; executable 2/9; seven gap groups), complete healthy coverage, and missing/duplicate/unexpected/unexplained coverage plus suspended/unsupported/version-mismatch denials. The coordinator registers it in the solution and partial bootstrap CI. The worker may author the new project and lock file within its exclusive directory; existing shared configuration stays coordinator-owned.

The harness also checks the actual A1 combined projection against independent expected counts, limits and completion states, and checks that capability or coverage denial exposes no combined summary. Temporary A1 dependency copies in the verification checkout are excluded from its delivered packet.

## Integration and completion

- [x] Workers confirm isolated paths/branches and read governing documents.
- [x] Coordinator approves exact packets before edits and records any scope amendment.
- [x] Patches receive independent review and integrate without ownership conflicts.
- [x] Applicable pinned locked restore, formatting, Release build, unit/integration/architecture, secret/dependency/package checks run; unavailable checks are named and remain NOT VERIFIED.
- [x] Feature plans/status/test descriptions and evidence index record the actual local results and remaining gaps.
- [ ] Existing private pilot site and human board are updated from canonical records; publication confirmed.
- [x] Clean temporary worker worktrees are removed after preserving/integrating their changes.

No new cloud deployment, source access, provider enablement, customer installation, production release or G1–G9 pass is authorized by this cycle.

## Execution evidence

Gitleaks 8.30.1's Darwin arm64 release was verified against its published archive SHA-256. The redacted directory scan of a 236-file snapshot of tracked/non-ignored checkout files completed with no findings. Ignored/private files, Git history, deployed resources and provider secret stores were outside this local scan.

On 2026-10-01 the coordinator executed SDK 10.0.401 locked solution restore with NuGet Audit, solution formatting, Release build (zero warnings/errors), all five current unit hosts, the integration host, architecture host and self-contained win-x64 publish. All completed successfully. The assessment host passed 13 guard and 25 composition cases, including 100,000 units. The integration host passed six versioned fixtures and 78 assertions. The collector checkpoint host passed 26 cases (seven added retry assertions).

The verification worker independently reviewed and executed C1/A1; the collector worker independently reviewed A1/V1. Neither review found an actionable issue within the packets. Worker changes were preserved on branches at `499ea7e` (C1), `b24cd22` (A1) and `6e43943` (V1 plus copied test dependencies), then integrated as scoped patches. Their clean temporary worktrees were removed.

The tested source SHA-256 is `7fc2b5ec23c00d616961fb53a6f83157686155a1379e0b37f4862e840fa4d59d`: sorted repository-relative paths for 124 source/test .cs, .csproj and package-lock files plus solution, Directory.Build.props, global.json and bootstrap workflow, each path and file body separated by NUL. Generated bin/obj are excluded. The completed local verification transcript SHA-256 is `2a253f14bb58703011a261be924bd99077bae10b7a27eb637bfbad95e79136fa`. These metadata are developer evidence; no signed gate bundle or restricted-store artifact exists.

Initial sandbox package-audit/IPC attempts were superseded by successful checks with authorized network/IPC access and isolated build settings; no audit, safety or pin was disabled. [partial bootstrap run 36935964103](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36935964103) passed on `ed3a77b` across Linux and Windows 2022/2025: audited locked restore, format/build/unit/integration/architecture, pinned secret scan, Bicep compile/lint/policies, Windows protected-key/ACL checks, cross-publish and blocked-service smoke all completed successfully. This cycle changes no infrastructure source. Manual accessibility, deployed isolation/restore, customer source tests, license acceptance, signed MSI/OCI provenance and full feature/E2E gates remain NOT VERIFIED; no corresponding product path is enabled here. No migrations, environment configuration changes or external package additions occurred.
