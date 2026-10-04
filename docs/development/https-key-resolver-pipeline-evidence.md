# Actual Key Vault resolver pipeline diagnostic

Status: VERIFIED — bounded local RP01–08 only
Date: 2026-10-04 UTC
Authority: owner “approved, next step”, accepted PC-D02 and the independently reviewed [RP plan](../../plans/active/https-key-resolver-pipeline-diagnostic.md), including the supported synthetic challenge clarification.

## Inputs and executed checks

The [previous Linux signature prerequisite](https-key-provider-linux-run-result.md) and [27-case dispatch checkpoint](https-key-provider-dispatch-evidence.md) remain retained. This test-only extension uses the same exact37 experimental archives and74 selected compile/runtime entries, assets SHA256432f6c7796b7302a655eb65d0e3baa1bb1cb7c656c9fc05ec8ff16b7a8874a1f and lock SHA2562d726d936d5cb3b5d647ef77a3f62d56e16fb0a70b49738f483ab3c7b5eebe9f. No new package graph or production dependency promotion.

Author candidatefb6e837d715ce2534eba4bd71e027f9c4b57cdb9 passed independent review and was integrated as35a317529a28ca0b39f667e9519b9ae39582203e with identical four-file hashes. Author, independent reviewer and coordinator each executed45 cases:27 preserved dispatch cases plus18 new resolver/diagnostics cases. Each verified37 archives/74 selected entries, SDK10.0.401/runtime10.0.12, empty-source framework restore, Release build0 warnings/errors and whole scratch formatting. Each loaded six digest-bound assemblies, including Azure.Security.KeyVault.Keys4.10.0.0. DataProtection assembly version10.0.0.0 belongs to the installed10.0.12 shared framework, not another archive in manifest37. Framework-only restore supplies no fresh advisory or signature proof.

The reviewer independently rejected nine runner input controls and four source-copy/missing/change controls; these are thirteen negative checks, not additional runtime cases. Python AST, five README local links, UTF-8, whitespace and scoped Gitleaks passed. Reviewer and coordinator checked twelve raw command-log byte/hash receipts and six loaded assembly hashes each. [Source-bound receipt](evidence/https-key-resolver-pipeline-20261004.json) records exact sources, command receipts, scope reviews, retained failures and limits.

## Actual SDK mechanics

| Cases | Executed result |
|---|---|
| RP01 | Actual sync/async KeyResolver metadata200 returns the concrete fixture version. Actual Microsoft provider wraps using downloaded public RSA metadata; stored historical version selects a scripted remote unwrap. |
| RP02 | Actual unguarded metadata403 produces the versionless force-remote client. First synchronous wrap: metadata1, crypto attempts2, supported challenge1, synthetic token call1, accepted crypto1. Subsequent asynchronous cached case: metadata1, crypto1, challenge0, synthetic token call1. |
| RP03 | Public test-only pipeline policy throws a non-RequestFailed GuardDenied on terminal metadata403 before resolver fallback. Cold sync/async and actual provider cases each metadata1, crypto0, credential0, policy denial1. |
| RP04/07 | Invalid fixed inputs deny before transport; foreign/versionless results deny before crypto; versionless historical kid denies before resolver. Unexpected destinations are trapped, sync/async dispatch and every case's request/credential counters are asserted. |
| RP05 | Memory-only normal observations: Activity4, DiagnosticListener8, EventSource4; fixed binding/protected-field presence true. Body-field, token and crypto-marker presence false in this bounded fixture with default content logging. |
| RP06 | Supported diagnostics-off options yielded0/0/0 observed emissions. Test allowlist sink rejected16 raw normal observations; accepted only one harmless closed label/count control. Hostile binding/body controls denied; protected fields reaching that sink false. |
| RP08 |45 cases, exact package/source/assembly/log bindings, build/format and independent negative checks passed. No full KEY row is completed. |

The supplied HttpPipelineTransport creates ordinary SDK request objects and never invokes base HTTP send or a socket. The allowed credential is explicitly synthetic and verifies the exact fixture context; unexpected acquisition throws. Scripted401/200/403 responses and token counts do not prove actual Azure RBAC, real credentials, TCP or live challenge behavior.

All unchallenged guarded403/input/result/historical negatives run before ordinary SDK challenge caching is primed in each fresh executable process. The zero-credential403 result is limited to those cold cases. Later positive cases intentionally observe the same fixed authority's warm cache; fixtures neither claim independent authorities nor clear private caches. Production warm-cache failure behavior remains unverified.

## Source-backed correction and formatting evidence

Run01 failed build with five errors from fixture assumptions: actual CryptographyClient.KeyId is string and public WrapKey returns WrapResult directly. Runs02–04 rejected lowercase operation paths; the exact SDK paths are `/wrapKey` and `/unwrapKey`. Runs05–06 exposed null initial request content. Immutable SDK source confirmed ChallengeBasedAuthenticationPolicy intentionally strips the body until a supported401 challenge restores it. This was fixture correction, not an SDK defect or request getter failure.

The independently reviewed clarification permitted the existing plan's narrow synthetic challenge/token path. Ordinary resource-domain verification stayed enabled. No replacement Request, SDK-private stashed-content access, private cache clearing, proxy/trust/revocation change or authentication bypass was used. Run07 and final runs passed45 cases. Coordinator checked92 raw logs across eight retained author runs; failures remain failures.

Author's scratch spacing control returned2 with WHITESPACE errors and restored source. Reviewer independently formatted the new resolver file: clean0, deliberate indentation error2, restored0, with source bytes preserved. Independent and coordinator whole-project format stderr was empty. Earlier dispatch formatting diagnostics remain historical; no suppression or runtime upgrade.

## Remaining work and human review

This packet contributes partial local KEY-005/011/012/013 prerequisites. It does not implement production guards, parser/size/deadline/readiness/generation limits, monotonic inventory/witness, bootstrap/recovery/lifecycle/retention/deletion policy, real Blob ranged/staged transfer/retries, audit outage composition, two replicas or full KEY/ARL/live acceptance. Fixtures do not establish production URI/crypto policy; bounded diagnostics absence is not universal privacy proof or an accepted production exporter contract. Unchanged product/PostgreSQL regression suites were not rerun for this test-only extension; their evidence remains historical.

[Proposed ADR-0013](../../architecture/decisions/ADR-0013-shared-key-inventory-and-recovery.md) and [input checklist](https-key-production-input-checklist.md) passed separate documentary review. No inventory option, witness authority or new policy quantity is selected. Production dependency/composition, protected bindings, fresh cost/session approval, G1–G9/Milestone2 and all six [human tasks](https-production-test-packet.md#human-tasks-and-completion-conditions) remain open. No Azure resource/grant/spending, production configuration/schema/IaC, public source publication or portal admission changed in this packet.

Prepared BFF/HTTPS board delta closes only this local diagnostic and documentary preparation. Last confirmed BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale: automatic approval review rejected the separate credential/network publisher proxy-bypass. No retry/bypass; unrelated Cycle14 publication is preserved.
