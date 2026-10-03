# Actual Key Vault resolver pipeline and diagnostics

Status: PLANNED — bounded local SDK diagnostic
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
