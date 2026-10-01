# Local pilot parallel cycle 02

Status: Complete — second parallel cycle verified and privately published 2026-10-01
Owner: Coordinator / technical owner
Approval: Repository owner requested the second cycle on 2026-10-01, following the proposed assessment, collector-contract and verification packets.
Baseline: `0c75d4690dc660384850f00412da6064a8de403f`
Last updated: 2026-10-01

Governing records: [local build](../active/one-identity-local-pilot-build.md), [workflow](../../docs/development/parallel-agent-workflow.md), health [product](../../specs/003-health-assessment/product-spec.md), [technical](../../specs/003-health-assessment/technical-spec.md), [implementation](../../specs/003-health-assessment/implementation-plan.md), [tests](../../specs/003-health-assessment/test-plan.md), [evidence contract](../../specs/003-health-assessment/database-evidence-contract.md), collector [technical](../../specs/001-data-ingestion/technical-spec.md), [tests](../../specs/001-data-ingestion/test-plan.md) and [delivery proposal](../../specs/001-data-ingestion/collector-delivery-and-installer-contract-proposal.md).

## Objective and boundaries

Connect versioned synthetic baseline inventory to an explicit coverage plan and existing progress/terminal summaries in an executable local test host. This internal synthetic projection is not an ingestion wire format, signature verifier, eligibility grant, authenticated baseline adapter, complete applicability engine, durable run or consultant UI. Trusted callers provide already-authorized scope and approved category applicability metadata; unknown product semantics are not inferred. Prepare exact candidate collector delivery/receiving contracts for required review without enabling an adapter. No customer evidence, source access, provider, cloud deployment or gate promotion is authorized.

## Work packets and ownership

| Packet | Owner and permitted paths | Traceability | State |
|---|---|---|---|
| A2 | Assessment; new `src/server/modules/AssessmentOrchestration/SyntheticBaselineInventoryPlanner.cs` and new `tests/unit/AssessmentOrchestration.Tests/SyntheticBaselineInventoryCases.cs` | IP-HAS-004/005; FR-HAS-1/2/5/29; TP-HAS-001/007/015/016 local synthetic subset | VERIFIED |
| C2 | Collector; new `specs/001-data-ingestion/collector-offline-receiving-contract-draft.md` | ING-PILOT-003/004; FR-ING-23–27; C0/C3; TP-ING contract-negative subset | VERIFIED draft packet only |
| V2 | Verification; `tests/integration/SyntheticPilotFlow.Tests/` only | Independent expected synthetic baseline/plan/progress/completion fixtures; TP-HAS-001/007 subset; review A2/C2 | VERIFIED |
| O2 | Coordinator; shared entry points/project/workflow configuration, canonical records, reviews, integration, private site | Local build and evidence/site obligations | VERIFIED |

### A2 acceptance

Use an explicitly versioned, value-free internal synthetic fixture descriptor, already-authorized opaque scope, baseline-local objects, existing exact-version compatibility and trusted category/applicability metadata. Refuse malformed/unknown versions, missing identifiers, wrong scope, blocked permission descriptor, duplicate object IDs and malformed module/category/applicability metadata. Produce a deterministic, immutable, deduplicated inventory/category plan only after capability lock succeeds. Preserve explicit unassessed/unsupported/not-applicable intent when supplied by trusted applicability metadata; never infer a pass/finding or semantic rule. Do not silently merge duplicate native identities or erase gaps. Empty applicable inventory cannot be presented as perfect coverage. Unit cases include input reordering, mutation after planning and 100,000 keys. Agent proposes exact routine internal types to the coordinator before dependent V2 edits; consequential ambiguity is escalated.

### C2 acceptance

Produce a clearly unapproved, disabled candidate offline header/manifest/receiving state contract, concrete negative-case matrix, trust/bootstrap and replay-store decision points, and an installation decision checklist. Separate existing fixed requirements from proposed exact serialization, signer, limits, lifetime, clock and installer choices. Do not claim new security/operations approval, select a production signer/toolchain, implement encryption/import/package routes, or change accepted policy. Requested reviewer roles, unresolved choices and completion conditions must be explicit and suitable for a linked human task.

### V2 acceptance

Read governing specs independently. Add stable versioned fixtures with independently expected keys, states/counts and limitations using A2's actual API once settled. Exercise compatible, partial, unsupported/mismatched, wrong-scope, duplicate/malformed and empty inventory inputs; partial progress and missing terminal results must not falsely complete. Include reordered inputs and 100,000 keys. Execute the fixture host and review A2/C2 against specification boundaries. Report only executed evidence; these cases do not complete feature/E2E gates.

## Completion

- [x] Isolated worktrees, exact packet ownership and internal API handoff confirmed.
- [x] A2/C2/V2 independently reviewed and integrated; acceptance gaps explicit.
- [x] Applicable locked restore, format, build, unit/integration/architecture, security/dependency and package checks executed and recorded.
- [x] Canonical plans/status/test descriptions/evidence updated with exact scope and remaining work.
- [x] Existing private site and human board updated; deployment confirmed.
- [x] Worker changes preserved and clean temporary worktrees removed.

## Execution evidence

The three writing workers use `/private/tmp/iga-cycle02-assessment`, `/private/tmp/iga-cycle02-contracts` and `/private/tmp/iga-cycle02-verification` on separate `codex/parallel-*-cycle02` branches from the baseline above. The coordinator approved A2's routine internal API before V2 dependent edits: `synthetic-baseline-inventory-v1`, explicit scope/permission/compatibility, baseline-local objects with optional native identities and caller-supplied category applicability. Exact duplicate category descriptors deduplicate; conflicts and duplicate identities return visible typed issues. Permission warnings remain visible. No full manifest, payload integrity, authorization or audit behavior is claimed.

On 2026-10-01 the coordinator's SDK 10.0.401 audited locked solution restore, solution formatting, Release build (zero warnings/errors), all five current unit hosts, the integration and architecture hosts, and self-contained win-x64 publish passed. Assessment checks include 13 existing lock, 25 existing composition and 62 new planner cases; integration includes the six existing fixtures / 78 assertions and eleven new baseline fixtures / 65 assertions (17 / 143 total). New fixture digests bind actual mutated descriptors and full 100,000-object/result inputs, not summary labels. Missing terminal results remain outstanding and cannot complete. An initial formatter attempt found four whitespace positions; corrected source passed the final full check run. No check, audit, version pin or safety boundary was disabled.

The tested source SHA-256 is `21a79d08370411a7236c7f5617e0a3c7cfa4aabb60a0da90d75f0379db01d0b7`: 127 sorted files comprising source/test .cs/.csproj/locks, solution, Directory.Build.props, global.json and bootstrap workflow, each path/body NUL-separated; bin/obj excluded. Final local check transcript SHA-256: `ca43ba910bddb4a7e242256a58182d19bc901c01b6d76d6d42068ffaf89a6f63`.

The assessment and contract packets are preserved at `d498420` and `97e3cc6` on their isolated branches. V2 is preserved at `ce6755f`; copied test dependencies are excluded. All three clean temporary worktrees were removed; concurrent Azure worktrees were left to their owners. Verification independently reviewed A2/C2, and the contract worker reviewed A2/V2. Neither found a remaining actionable issue. The review tightened the draft's collection replay identity to bind substantive manifest metadata, and the verifier confirmed the final rule. Twenty-one draft references/anchors resolve; OFF-D01–08, OFF-N01–24 and INST-D01–09 remain proposals/planned vectors, with no runtime crypto/import/installer checks claimed.

All G1–G9 remain NOT VERIFIED. Full feature/E2E, manual accessibility, customer baseline/source, deployed authorization/isolation/restore and signed release/installer evidence remain unverified. No migrations, environment configuration, external package additions or live activation occurred in this cycle. The concurrently completed Azure configuration/cleanup work is preserved separately and is not a cycle-02 gate pass. Private-site publication and final cleanup are confirmed below.

Gitleaks 8.30.1 (the previously vendor-checksummed binary) scanned the final 242-file tracked/non-ignored snapshot with redaction enabled and found no secrets. The scan excludes Git history, ignored files, private files and deployed provider stores. Ninety-six affected-document local links resolved. Desktop 1440px and mobile 390px rendering/navigation/no-overflow checks passed, and all eight human cards retain role/completion/source links.

[Partial bootstrap run 36940634611](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/36940634611) passed on `918d48ef47a4c3eaf8563674d3fca50ae116c9b2`: Linux audited restore, format/build/unit/integration/architecture, pinned secret/Bicep compile/lint/policy checks, and Windows 2022/2025 protected-key/ACL, collector packaging and service/disabled-CLI smoke all succeeded. The workflow is a partial engineering gate, not pilot acceptance. No infrastructure source changed in cycle 02.

The cycle snapshot was published to the existing owner-private site with native status `succeeded` at 2026-10-01T23:29:10Z: Site source `d4f4beeff34be01e4ea152d56f3f60803a1da032`, canonical snapshot `125db5c160c1da08012891f5ff1e5c25ab339f9d`, deployment `appgdep_6abeecc04df4819184888fe51ed887d5`. Twenty-nine repository links and internal anchors resolve; the eight human tasks retain requested roles, completion conditions and source links, including the exact offline review draft. The record is archived after confirmed publication, then the site snapshot is refreshed to include closure. The broad local pilot build is still incomplete, and completed workers do not continue unattended.
