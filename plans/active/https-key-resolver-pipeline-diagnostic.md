# Actual Key Vault resolver pipeline and diagnostics

Status: VERIFIED — bounded local SDK diagnostic only
Date: 2026-10-03 UTC
Owner: Coordinator
Baseline: 65f2608
Authority: owner “approved, next step”; accepted PC-D02 local Microsoft provider/guard strategy in [cycle01](https-production-local-cycle01.md), [key contract](../../docs/development/https-production-key-contract-proposal.md) and [artifact audit](../../docs/development/https-key-provider-artifact-audit.md). The [previous dispatch packet](https-key-provider-dispatch-diagnostic.md) independently verified the exact37 graph and public provider dispatch. This next packet observes the actual SDK pipeline under deterministic responses, not Azure or production composition.

## Scope and ownership

Exercise actual Azure.Security.KeyVault.Keys4.10.0 KeyResolver/CryptographyClient through a supplied HttpPipelineTransport that never opens a socket. Supply a synthetic credential that throws if called unexpectedly; any explicit token/challenge fixture is synthetic and recorded, with ordinary challenge-resource validation intact. No real credentials, key/vault/tenant calls, grants, paid session, public source publication or network transport. Fixed test bindings and generated crypto material never become production policy.

Writing worker owns only tests/infrastructure/HttpsKeyProviderDispatch/ResolverPipelineCases.cs, Program.cs, run-experimental.py and README.md, in a new isolated worktree. Preserve the existing27 case scenarios and source integrity guards. Coordinator owns plans/canonical evidence/shared configuration/private board. A non-author reviewer first checks scope, then independently executes immutable candidate checks. No production adapter, dependency/lock/solution/workflow/schema/IaC or runtime configuration changes.

Continue the exact assets/lock/manifest37 and74 compile/runtime entry verification, SDK10.0.401/runtime10.0.12, explicit temporary project references and empty-source framework restore. Extend source bindings/copying to include the additional test file; generated project and output remain under fresh owned /tmp. Do not resolve a new graph, install runtimes, change trust/revocation/proxy/challenge validation, enable credential fallback or claim a new vulnerability audit. Corresponding SDK source commit6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692 and raw package API documentation inform mechanics; actual archive-derived assemblies supply execution proof.

## Required bounded cases

| ID | Required evidence |
|---|---|
| RP01 | Actual KeyResolver sync and async metadata200 from transport returns the fixed concrete same-key version; public Microsoft encryptor/new wrap and decryptor/historical selection remain on actual package code |
| RP02 | Unguarded actual metadata403 control observes supported force-remote resolution; invoke wrap through that returned SDK client and observe deterministic crypto request/count/result. Record resolved KeyId facts without printing IDs/bodies; no inference from source alone |
| RP03 | A test-only public pipeline policy observes metadata403 and throws a non-RequestFailed guard failure before KeyResolver can turn it into force-remote. Sync/async cases show metadata attempt and zero crypto requests; actual provider registration also refuses new wrapping under the fixture |
| RP04 | Preserve exact fixed input and resolved/historical binding guards; invalid/foreign/versionless historical or resolved IDs deny. Test policy permits only fixed fixture metadata path/response, not a new production URI grammar or authorization contract |
| RP05 | Observe actual SDK Activity/DiagnosticListener/EventSource emissions in memory under normal fixture options, including whether protected key-ID/URI/body fields are present. Output only case labels, counts, booleans and hashes; never raw key/crypto/token/XML or diagnostic payloads |
| RP06 | Test supported diagnostics-off options and a test-only allowlist observation boundary against the same controls. Assert no fixture binding/crypto/protected fields reach that test sink and show a harmless positive sink control. Do not accept a production OpenTelemetry exporter/redaction policy from these fixture results |
| RP07 | Count all transport/credential/crypto calls per negative case; catch unexpected requests and deny without base HTTP. Label scripted SDK responses as simulation; neither actual Azure RBAC nor TCP/credential/challenge behavior is established |
| RP08 | Preserve all prior27 scenarios; zero-warning Release build, whole scratch C# format with meaningful negative control if workspace diagnostic recurs, focused new cases, Python AST/docs/whitespace/scoped secrets and independent execution/source/log hashes |

This packet contributes partial local KEY-005/011/012/013 prerequisites. No full KEY row,403 Azure role proof, production guard coverage, inventory/witness/rollback authority, lifecycle/generation/parser/size/deadline/readiness policy, real ranged/staged transport/retry/outage, composed admission or release follows. If supported public policy/diagnostics APIs cannot intercept safely, retain evidence and stop for architecture review rather than replace Microsoft internals.

## Verification and completion

Read-only non-author scope review precedes writing. Author reports exact commands/exits, failures/corrections, package/source/assembly/log hashes and actual case counts. Reviewer independently repeats actual package execution and meaningful negative controls, verifies status interception precedes force-remote, and inspects callback/sink leakage without exporting raw fixtures. Coordinator integrates reviewed bounded source, repeats affected checks and updates canonical feature plan/status/test/evidence plus six linked human-task delta. Unchanged product regressions are historical, not rerun; no product behavior changed. Framework-only restore is not fresh advisory/signature evidence.

Production graph/pin/build review, monotonic inventory/witness and lifecycle decisions, protected key/identity/ingress bindings, remaining real SDK transport/composition/fullKEY/ARL/live and G1–G9/Milestone2 remain open. Existing private BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale under the separate automatic-review-rejected credential/network publisher proxy-bypass; no retry/bypass, preserve unrelated Cycle14 publication. No new publication or credentials are requested by this plan.

## Supported synthetic challenge clarification — 2026-10-04 UTC

The immutable SDK ChallengeBasedAuthenticationPolicy source at6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692 removes initial request content until a supported401 authentication challenge restores it. The plan already permits an explicitly synthetic token/challenge. Positive scripted remote wrap/unwrap cases must therefore exercise that public challenge path and count only narrowly expected synthetic credential calls, header presence and authenticated body validation; no raw token, header, body or payload may be printed. The credential throws outside that expected scope. Unguarded metadata403 control and guarded403 cases remain separately attributable; guarded metadata must cause zero crypto and credential calls. Fixture authorities must isolate ordinary SDK challenge caching through supported inputs, without cache mutation or disabling challenge-resource verification. No request replacement or recovery of SDK-private stashed content is allowed to mask the body behavior. Retain all failed runs as fixture corrections, not SDK defects.

This clarification adds no credential, grant, network or production authentication behavior. Scripted401/200/403 and synthetic credential counts do not prove Azure RBAC, live credential acquisition or network challenge safety. Immutable shared pipeline/body source digests and the independently reviewed scope clarification belong in the final diagnostic receipt.


## Executed checkpoint — 2026-10-04 UTC

Immutable authorfb6e837d715ce2534eba4bd71e027f9c4b57cdb9 passed non-author RP01–08 review and integrated as35a317529a28ca0b39f667e9519b9ae39582203e with identical four-file hashes. Author/reviewer/coordinator each executed45 actual cases (27 preserved plus18 new),37 archive checks/74 selected entries, six loaded assembly digest/version checks, empty-source framework restore, Release0 warnings/errors and whole scratch format. Reviewer13 input/source-integrity negatives and a meaningful new-file formatter clean0/misformatted2/restored0 control passed. Python AST/links/UTF-8/whitespace/secrets and raw log/source bindings passed. [Detailed result](../../docs/development/https-key-resolver-pipeline-evidence.md) and [source-bound receipt](../../docs/development/evidence/https-key-resolver-pipeline-20261004.json) preserve failures, corrections, scope reviews and exact limits.

The clarification's cache isolation was realized through a fresh process and explicit supported fixed-authority ordering: all cold guarded403 negatives before ordinary challenge-cache priming; later positive cases intentionally observe the warm cache. Fixtures do not claim independent authorities. Credential0/crypto0 applies to the cold guarded403 cases only; production warm-cache failure behavior remains open. Synthetic401 restores the actual SDK body without request replacement/private stashed-content/cache access or disabling ordinary resource verification. Normal observations4/8/4 exposed protected binding fields in memory; diagnostics-off observed0/0/0 and raw fields never reached the fixture allowlist sink. No universal privacy/exporter contract follows.

RP01–08 is VERIFIED only for the stated local diagnostic. No production graph/policy/guard/inventory/witness/lifecycle/bindings or full KEY/ARL/live/G1–G9/Milestone2 approval follows. Unchanged product regression suites remain historical. The separately reviewed Proposed ADR/input checklist selects no architecture. All six human tasks and stale private BFF/HTTPS publisher status persist; no Azure operation, grant, paid session, public source or private site publication.
