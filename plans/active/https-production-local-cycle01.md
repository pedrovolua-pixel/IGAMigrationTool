# HTTPS production local cycle 01

Status: VERIFIED — bounded local response projection and artifact audit; production dependencies remain blocked
Date: 2026-10-03 UTC
Owner: Coordinator
Approved product: [Health assessment](../../specs/003-health-assessment/product-spec.md)
Approved local technical basis: [production proposal](../../docs/development/https-production-contract-proposal.md), [provider projection](../../docs/development/https-production-provider-contract-proposal.md), [key strategy](../../docs/development/https-production-key-contract-proposal.md)
Test basis: [production test packet](../../docs/development/https-production-test-packet.md)
Approval: Repository owner replied “Approved” after the five PC-D01–PC-D05 decisions and exact packet commit `d518d25274956fbaf19f8cdff916bf0236394fc6` were presented. This accepts the proposed recommended local design and supporting tests. It does not supply evidence/quantities that were explicitly unspecified or authorize a live session, grants, publishing-helper bypass or production release.

## Decision receipt and retained dependencies

| Decision | Accepted local scope | Dependency still requiring evidence or an exact addendum |
|---|---|---|
| PC-D01 | Initial direct-role internal organizational reader; strict selected-property/envelope/time/continuation rules; external admission denied pending independent home proof | Credential/consent/binding, manifest consistency, refresh/retry/deadline contract and actual Graph property/paging proof |
| PC-D02 | Preferred supported Microsoft providers plus narrow lifecycle guards, not a custom repository or read-only writer architecture | Exact artifact/source locks/audit, supported interception proof, monotonic inventory/witness authority, lifecycle and actual roles |
| PC-D03 | ADR-0012 Option A for bounded local synthetic protocol work; typed anonymous failure receipt/producer and source-occurrence linked lifecycle interpretation as proposed | No total-outage exception accepted; exact witness/capacity/deadline/grant enforcement and live lifecycle proof remain open |
| PC-D04 | Preserve current exact-peer contract and require actual benign/adversarial ingress proof | No permissive ingress amendment or deployment session accepted |
| PC-D05 | Existing-type server-owned resource resolution and exact-session/action CA context verifier, with verified protecting-policy strength | Sidecar lifecycle, exact step-up/public outage response, abuse/deadline and real policy/resource bindings remain unspecified; dependent host/routes blocked |

ADR-0012 is accepted only for the recommended local Option A prototype. Production compliance during total outage is unsatisfied; safe refusal does not preserve a missing audit event. No live policy exception or retention lock is approved.

## This cycle's scoped implementation and tests

LP01 implements a pure bounded Graph response projector in the existing IdentityAuthority module, with no HTTP client, credential, scheduling, Graph SDK or production-host registration. It validates selected user and direct-role page JSON, removes only optional bounded `@odata.context`, normalizes the approved zero-to-seven UTC fractional digits without rounding, preserves validated nextLink unchanged, and produces only the existing closed reader documents. Existing synthetic reader remains explicitly synthetic; a future transport/composition consumes this projector only after its additional contracts are resolved. Projected bytes are not authority, complete pagination, MFA, a manifest attestation or admission.

Do not add new observation/domain/public schemas. Use the existing source byte/depth/row and binding constraints. Reject duplicates, unknown fields/annotations, missing/invalid selected values, wrong immutable subject/resource/role, malformed UTF-8/time, noncanonical IDs and hostile/incompatible continuation. Metadata is bounded by the existing whole-document 65536-byte limit and never dereferenced. A per-page result cannot claim completeness across pages; existing reader/production composition must separately detect repeated assignments/roles/cycles and all-page bounds. No reuse of raw Graph data as a synthetic payload without this explicit validation/projection stage.

Trace: LP01 contributes to HTTPS-PROV-T03/05/07/08 and AC-HAS-9, TP-HAS-009, IP-HAS-003 and NFR-SEC-1/3/5/6. These are partial local parser cases, not execution or acceptance of all20 provider cases.

| Packet | Owner / permitted write paths | Checks and state |
|---|---|---|
| LP01 | Isolated provider worker: `src/server/modules/IdentityAuthority/GraphProviderResponseProjection.cs`, `tests/unit/IdentityAuthority.Tests/GraphProviderProjectionChecks.cs`, and one call in existing `tests/unit/IdentityAuthority.Tests/Program.cs` | Existing authority suite plus independent hostile corpus, actual .NET10.0.401 locked restore/build/format and secret/whitespace; VERIFIED — local parser and independent review only |
| LP02 | Isolated platform worker: only `docs/development/https-key-provider-artifact-audit.md` | Exact stable Microsoft package metadata, immutable source/artifact hashes and dependency/license/vulnerability evidence; no dependency install into repo, no production selection by assumption; VERIFIED — documentary evidence only; integration remains BLOCKED |
| LP03 | Coordinator: canonical docs, configuration only if necessary, integration and full applicable checks | Non-author review of immutable candidates before integration; VERIFIED — combined local checks and evidence |

## Verification, configuration and rollback

Coordinator runs locked full-solution restore with audit, full formatting and Release build, applicable existing IdentityAuthority and BFF/identity/session/policy/hosting/architecture local regression suites and affected actual PostgreSQL authority/audit/BFF composition cases using a new owned disposable cluster. Scan combined source for secrets and whitespace, check changed-document links, and preserve exact executed evidence. No frontend, infrastructure or dependency changes are planned; Windows/service, container, live browser/provider/network/RBAC/MFA/recovery/gates remain NOT VERIFIED for this cycle and require their applicable future packets. Do not claim full configured CI or new public release proof from local evidence.

Independent review must confirm no production wiring, new permission, schema, lifecycle job or fallback. Rollback removes the additive unused projector and test call; it has no persisted state or migration. No protected SQL, customer evidence or credentials enter Git or the site.

## Human tasks and status board

Owner design-review tasks close only for the local scopes above. Keep exact bindings, home-account proof, SDK/inventory/lifecycle details, failure-outage/witness/limits, step-up/CA/resource/ingress and priced deployment tasks open. The private BFF/HTTPS board is stale: the separately rejected credential/network publisher helper cannot be retried by treating this design approval as proxy-bypass authorization. Preserve unrelated confirmed Cycle14 publication; prepared board delta follows these linked rows and executed local evidence.

## Completion record

LP01 author `a865cf8` plus formatting-only `9d34ec5`, integrated as `b21574b`/`5143403`, passed non-author `package_hosting` review. The initial candidate failed unfiltered test-project formatting; the formatting-only correction passed that exact gate. Reviewer reran212 authority assertions and29 independent hostile cases against the unchanged projector blob; no semantic/security blocker.

LP02 author `e887f41`/`d008c8b`, integrated as `689a2bf`/`a3f9eb9`, passed independent `production_identity_proposal` review:32 raw archive hashes/manifests, five evidence digests, tags/catalogs/source dispatch, dated vulnerability ranges and signature results. Only source-supported feasibility is established. Five candidate-minimum signature checks failed in the recorded macOS verifier; no package tampering claim, trust bypass or installed host graph follows.

Coordinator: full-solution locked audited restore, unfiltered full-solution formatting and Release build with zero warnings/errors passed. All12 applicable core suites passed, including212 authority,69 session/audit unit,2546 policy,188 foundation,308 public authentication HTTPS,60 hosting,11 architecture,120 PostgreSQL authority,81 audit,644 composition,126 shared-session and84 HTTPS/session-flow checks. These are scoped regressions, not all configured CI or live acceptance. A mistaken nonexistent standalone audit-unit path and two composition database-name guard refusals were corrected in the temporary runner without product changes; all actual final suites passed. Sandbox-only restore was stopped and PostgreSQL shared-memory initialization was retried with required runtime access.

No new dependency, lock, SQL/schema, public contract, host wiring, permission, lifecycle job, infrastructure or Azure resource change. No migration/configuration step required. Rollback removes the unused additive projector/test call; existing stores and authority are unchanged. Full configured CI, frontend/browser accessibility, Windows/container, actual Graph/CA/RBAC/key/ingress/recovery and G1–G9 remain NOT VERIFIED for this cycle. Remaining anonymous audit persistence, SDK signature/actual graph/guards, inventory/lifecycle, exact resource/privileged/ingress/response inputs and live bindings are explicit next dependencies; owner design approval is not requested again.

[Execution evidence](../../docs/development/evidence/https-production-local-cycle01-20261003.json) owns exact source and checks. Private site publication remains unavailable under the separate publisher block; six linked human task rows are updated in the acceptance packet. No worker is left running unattended.
