# BFF local authentication contract implementation

Status: VERIFIED LOCAL CHECKPOINT — configured checks passed; matching private publication pending
Owner: Azure/BFF coordinator
Decision: Repository owner “approved”,2026-10-02; exact D01/D02 in [the decision record](../../docs/development/bff-production-contract-proposal.md)
Base: `8a746dc` on existing unmerged `codex/bff-integration` draft

## Approved outcome and boundaries

Implement D01 session status/CSRF, bounded browser form sign-in, local sign-out and D02 exact challenge/session-bound authentication evidence. Trace to IP-HAS-001/002/003, Milestone2, approved identity/session design, authorization matrix and authentication/session/revocation/CSRF tests in feature003. Freeze a versioned OpenAPI artifact and contract checks. Supported framework OIDC callbacks retain state/nonce/correlation/PKCE validation; no hand-written token protocol. The permanently disabled diagnostic executable remains unable to enable production routes or sign-in. Synthetic test compositions exercise reusable transport and authentication guards.

Production enrollment/app-role/provider authority, Graph consent, privileged CA verification, shared production key/proxy/audit composition, actual licensed users, Container Apps activation, release/image acceptance and refreshed paid session remain deferred. No customer source, Azure grant, live provider call or startup migration. Additive pending-challenge persistence is module-owned synthetic local schema only; its15-minute validity is the accepted D02 contract, not a new retention policy.

## Bounded packets and path ownership

| Packet | Owner | Paths | Acceptance |
|---|---|---|---|
| AUTH-D02 | Session worker | Existing BffFoundation registration/options/request protection/guard files and new challenge/session evidence files except PublicAuthenticationEndpoints.cs; IdentitySessions module and new additive migration; existing foundation/session/flow tests; new focused D02 tests if needed | Separate current subject eligibility from exact authentication evidence. Signed auth_time freshness and protected single-use challenge, consume only after all supported protocol checks. Reject personal/unknown guest origin; per-session evidence cannot borrow subject-wide timestamps/MFA; defaults deny privileged evidence. Preserve current DB idle/absolute/provider/version/revoke checks. |
| AUTH-D01 | Transport worker | New src/server/hosts/BffFoundation/PublicAuthenticationEndpoints.cs and exclusively prefixed PublicAuthentication helper files; new tests/integration/BffAuthenticationTransport.Tests/ | Exact three route/method/body/status/header contracts, authenticated/anonymous CSRF, fixed HTTPS origin, form redirect, sign-out revoke-before-clear, all negative parsing/precedence/failure cases using actual HTTPS. No existing host activation or product UI changes. |
| AUTH-V | Non-author verifier | Read-only reviews and independently repeated evidence; report needed fixes to authors | Exact contract-to-implementation review, challenge replay/nonce timing and authority/session confusion, controlled actual HTTPS/PG evidence. |
| AUTH-I | Coordinator | contracts/bff-authentication/, shared solution/workflows/project wiring, canonical docs/evidence and private board | Freeze exact schema and internal integration interfaces, integrate reviewed patches, execute every configured affected check; publish sanitized records and owner-private board. |

Writing workers use isolated worktrees from the recorded approval commit. Coordinator owns shared project/solution/CI changes. Settle internal integration signatures before dependent code; workers request changes to files owned by another packet. Non-author review cannot accept production/gate evidence.

## Test plan

D01: exact JSON shape and no claims/tokens/session refs; anonymous/authenticated antiforgery context isolation; no-store; unknown path404/method405; disabled sign-in403 before parsing/provider; unauthenticated sign-out401; authenticated sign-in409; fixed-origin403; UTF8 form, singleton token4096char and8192byte bounds; strict empty JSON, wrong charset/media/duplicate/extra/body/truncated/malformed denial; CSRF then current-authority denial; supported fresh-auth redirect to fixed session path; store failure denies before cookie clear; local204, other sessions remain and revoked cookie fails across replicas.

D02: signed immutable identities/client/issuer and auth_time, missing/malformed/future/stale time; integer issuance floor and strict15-minute deadline; protected transaction missing/expired/altered/replayed/concurrent consumes; no consumption before framework state/nonce/correlation validation; no provider activity when disabled; guest personal-idp and unknown-origin denial; two same-subject sessions with distinct original authentication/privileged evidence; next-request version/role/assignment/account/cutoff revocation; DB failure/concurrency. Actual Entra/CA/FIC/provider claims remain NOT VERIFIED; synthetic OIDC proof is not deployed-provider acceptance.

Coordinator: pinned locked audited restore, format, warning-free build, architecture/contract drift, affected unit/real PostgreSQL/HTTPS suites, all configured current browser regressions, infrastructure and secret/dependency checks, actual container smoke/inventory where hosted runner exists. No missing scan/license/provenance/manual or restricted-store proof is relabeled PASS. Record source/test/run IDs and known limitations. Update canonical feature status, execution/evidence index and existing private human tasks board in the same cycle. Close only the exact D01/D02 approval task; production/live human tasks remain open.

## Executed local checkpoint

AUTH-D01 original816e5fe plus browser-only fixture correction2ec515a and AUTH-D02 originalf302f4f plus1cbfc36 are integrated as runtimec21b93a. The51-project solution preserves the published pilot AI/runtime and later closuref977515. The only merge conflict was adjacent historical implementation-plan prose, resolved additively; concurrent primary UI and site work stayed intact.

Coordinator and non-author final checks passed:188 foundation including116 signed synthetic HTTPS,308 public transport,126 actualPG,84 two-serverHTTPS/sharedPG,27 session and2546 policy checks, native Chromium actual helper, frozen OpenAPI/generated types, full locked audited restore/format/zero-warning Release build, current local unit/architecture, frontend/typecheck/build/audits, whole-repository secret and exact-container input checks. Existing hard-disabled process passed254 locally. The first hosted image build failed due to missing explicit002 COPY; reviewed correction46f009a passed the resource-copy guard and real hosted packageCI37027174418 (264 Linux process assertions, non-root/read-only container,14 fixtures and pinned vulnerability/license/SPDX collection). Raw reports stayed private ephemeral outputs and were removed; no acceptance, signed provenance or retained gate artifact follows.

Whole-pilot configured Linux/Windows/browser/PG/infrastructure CI37027174252 passed on correction46f009a. [The execution record](../../docs/development/evidence/bff-authentication-contract-20261002.json) distinguishes every original failure and final result; [non-author review](../../docs/development/evidence/bff-authentication-review-20261002.md) has no unresolved findings. All three clean temporary authentication worker worktrees were removed; original Git branches, review metadata and needed executed synthetic binaries are preserved outside them. Coordinator and reviewer disposable PostgreSQL instances and test listeners are stopped. Owner-private publication remains pending the final canonical source publication.

## Configured verification complete

Every configured hosted job passed on source46f009a: [bootstrap run37027174252](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37027174252) and [actual package run37027174418](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37027174418). Runtime source remains exactly the independently reviewedc21b93a; correction46f009a only copies002 and checks embedded resources. The local Docker config ID is sha256:de58283c3d139bca35f6b18668b92e46359bf8ace862bcfbba7c0335a166e069, not a published manifest digest. Final documentation preserves completed cycle08f977515. D01/D02 is now implemented, independently reviewed and verified; production, acceptance, access/spend and all full milestone/gate decisions remain open.
