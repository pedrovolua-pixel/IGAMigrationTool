# Local BFF authority, audit and hosting handoff

Status: VERIFIED LOCAL AND CONFIGURED HOSTED — matching private publication pending
Date: 2026-10-02
Authority: [P01–P05 approved local packet](bff-production-authority-hosting-proposal.md), [accepted local ADR-0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md), [execution plan](../../plans/active/bff-local-authority-audit-implementation.md).

## Runnable local boundaries

`IdentityAuthority` is a persistence/provider-seam adapter for existing `IdentityPolicy` contracts. It adds closed versioned enrollment, exact assignment, external lifecycle, direct-role manifest and provider observations. No sign-in enrollment, product permission inference or live Graph source is provided. Trusted internal attributed commands use revision-bound canonical digests and metadata receipts. Existing `identity_sessions.subjects.security_version` remains the single revocation counter. The resource selection and exact revision/category/action checks remain the existing `HumanAuthorizer` responsibility; authority assignment selection never grants descendants or wildcard scopes.

`IdentitySessions` adds a typed append-only audit/receipt stream and an explicit audited ticket-store constructor. Audited admission uses `ITransactionalSessionAdmissionPolicy` on the same PostgreSQL connection/transaction and original authentication timestamp. Subject/target/stream locks precede the final fresh deadline check and one durable commit. The four-argument ticket-store constructor and `PostgreSqlSessionAuthority` remain legacy synthetic fixture paths; they are not the accepted restricted production boundary. Production composition must select the audited path and separately reviewed roles, authority/provider sources and shared keys.

`PostgreSqlBffSubjectAuthority` is an opt-in projection, without registration or activation. External organizational Members receive the same home/lifecycle boundary as Guests; the effective cutoff is the later resource/home cutoff and freshness is the older verified source time. It supplies coarse roles only. Existing request/action/category permissions and default-denying privileged verifier remain unchanged.

`BffSharedProtectionContract` and `UseBffReviewedIngress` validate exact dedicated key/ring/environment inputs and one trusted peer/host/hop. Application Origin policy remains in D01 routes so the supported provider callback can run its own protocol validation. The key harness uses the real framework with synthetic encrypted storage/wrapping services. It does not attach Azure providers or prove Microsoft SDK conditional writes. The disabled diagnostic Program installs none of these activation paths.

## Additive migrations and configuration

Manual local order: identity-sessions `001-initial.sql`, `002-authentication-context.sql`, `003-atomic-audit.sql`, then identity-authority `001-authority.sql`. Existing 001/002 history is preserved. Migration application at startup is forbidden. No actual database/Azure/Graph permission is granted. Synthetic harnesses explicitly create disposable NONLOGIN owners and separate restricted LOGIN identities, approved scope/stream/role bindings and fictional authority decisions. Production identities and scope bindings remain empty protected inputs.

No new package is needed for hosting seams; exact existing framework/Npgsql/Microsoft.Identity.Web dependencies remain pinned. New test/module project references and locked graphs are coordinated in the solution/CI. Embedded migration resources must be included explicitly in the disabled container context.

## Audit and recovery limits

Events/receipts exclude credentials, cookies, lookup keys/hashes, protected ticket bytes, raw claims/provider errors and customer payload. Sequence, previous digest, canonical event digest and metadata receipts bind the committed operation; a synthetic independently supplied checkpoint can detect a missing/altered chain. This is not a deployed independent witness or protection against a privileged operator rewriting both store and witness.

Local lifecycle functions/tests preserve the approved 12-month event retention, holds, soft deletion, active purge within 30 additional days and retained chain tombstones. They select no schedule, receipt/authority/key retention policy or live lifecycle operation. Rolling backup maximum 35 days and restoration without resurrected authority/sessions remain actual operational proof requirements. Audit-unavailable admission denies and required mutation rolls back; no durable failure-event fallback exists when the audit store is unavailable. The separately reviewed outage-preservation decision remains a production blocker.

## Verification

Corrected code `ba8de41` passed independent258PG composition,87 authority unit/120PG,69 audit unit/81PG and60 actualHTTPS/framework key checks. The63-project locked audited restore/format/build and all configured Linux/Windows/browser/PG/infrastructure/container checks passed; non-author review closed. Exact source, failure/correction records and confirmed private publication are bound in [the execution evidence](evidence/bff-local-authority-audit-20261002.json). Passing bounded local checks does not complete Milestone 2, G1–G9, image acceptance, actual network/provider/CA/managed-identity/key/SQL evidence or production release.

## Next protected deployment inputs

Use [the binding intake](bff-production-bindings-template.json) and [the exact production blockers](bff-production-authority-hosting-proposal.md#protected-intake-and-separate-live-approvals). Required owners supply actual resource/client/role/federation and direct-role/property/home-status evidence; named licensed users/CA; attributed enrollment/assignment/lifecycle decisions; reviewed SQL functions/roles and audit outage/recovery/witness authority; private browser route/DNS/certificate/exact proxy peer; dedicated private Blob/Key Vault identity and historical key recovery; accepted image digest/scan/license/provenance/private pull and a refreshed priced session. Actual grants and activation require those concrete bindings and separate authorization.
