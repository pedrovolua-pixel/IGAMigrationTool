# Independent local authentication review — 2026-10-02

Reviewer: AUTH-V non-author worker, isolated detached checkout. Decision: approved for bounded local integration at `c21b93a659aee1f931ac0fffd4566ee667809f25`; no unresolved source findings. This does not accept production identity, draft merge, live permissions or any feature/milestone gate.

## Scope and corrections

Reviewed the attributed D01/D02 decision, frozen OpenAPI, exact public route/media/status/body/CSRF contracts, native navigation helper, supported OIDC handler extension, original signed scalar authentication time, pending shared-store consumption, exact session/cutoff/version/role binding and reviewed guest-origin match. The diagnostic Program.cs was confirmed unchanged from the hard-disabled package. No production adapter, privileged verifier or cloud activation was introduced.

Independent review required deadline checks after actual database lock waits and asynchronous completion, shared exact-session CSRF compatibility with generic protected mutations, and exact reviewed guest home-tenant binding. Author fixtures found supported manually handled code redemption skips the framework's token-response nonce validation; explicit supported validation now covers it. Original signed payload scalar checks prevent projected claim flattening from accepting arrays/string authentication times. All targeted corrections were reviewed and exercised; no unresolved implementation finding remains.

## Independently executed evidence

| Check | Exact reviewed result |
|---|---|
| Final full Release build and formatting | PASS; zero warnings/errors |
| Foundation | PASS188, including116 actual HTTPS signed synthetic protocol assertions |
| Public transport | PASS308 actual HTTPS checks |
| Shared PostgreSQL | PASS126, including32-way cross-replica consumption and observed row-lock deadline crossing |
| Two-server HTTPS/shared PostgreSQL | PASS84 |
| Session unit | PASS27 |
| Exact native navigation helper | PASS actual pinned Chromium/HTTPS, native form document navigation and same-loopback synthetic provider redirect |
| Frozen OpenAPI and generated types | PASS |
| Earlier locked audited restore, existing human policy and hard-disabled host | PASS on unchanged predecessor runtime portions;2546 policy and254 actual-process assertions |

Reviewer used PostgreSQL18.4 in its own disposable loopback fixture, then stopped it. No real provider credentials/customer source or Azure state was used. Sandbox compiler IPC, missing browser runtime, a test redirect chain and an accidental reuse of a one-shot flow schema initially prevented verification; scoped setup repairs/fresh dedicated schema resolved them before passing evidence. Failed attempts are not recorded as passes.

## Hosted packaging follow-up

The first real container CI failed because embedded migration002 was absent from the Dockerfile's explicit COPY, despite its context allowlist entry. Coordinator correction `46f009a` adds only the exact002 COPY and an embedded-resource copy check. Independent input review/repeat and new actual hosted package evidence are recorded in [the execution bindings](bff-authentication-contract-20261002.json). Input inspection is not a substitute for the actual image build/smoke.

## Limits

Actual Entra/Graph/CA/federation, provider emission/guest semantics/current app-role authority, licensed users, key rollover, production enrollment/proxy/shared key/audit composition, cloud grants, image acceptance/provenance/restricted preservation, Azure activation and G1–G9 remain NOT VERIFIED. Synthetic signed tokens prove the supported local validation branch, not the deployed provider. Hosted whole-pilot/container results are coordinator evidence bound separately to exact source and runs.
