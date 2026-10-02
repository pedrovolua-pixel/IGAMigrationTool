# BFF foundation scoped security review — 2026-10-02

Status: Independent local review passed; human live-use review remains required
Implementation: `3781b3b464102062795185fd812818555259aa20`
Evidence: [executed check record](bff-identity-session-20261002.json)

## Scope and trust boundaries

The approved human identity design and role/action matrix govern this disabled-by-default internal foundation. Assets include opaque cookie references, protected control-plane tickets, immutable identity, assignment/resource scope and monotonic revocation. The browser cannot supply authority snapshots, privileged MFA/Conditional Access decisions, guest onboarding facts or worker/share/MCP authority. Trusted production adapters remain unimplemented; synthetic test adapters do not establish production trust.

Separate reviewers inspected session persistence/rotation/concurrency, supported Microsoft OIDC and managed-identity configuration, cookie/CSRF enforcement, human policy and the coordinator's actual HTTPS integration. The final coordinator snapshot received an independent locked offline restore, warning-free Release build and all 84 real HTTPS/PostgreSQL checks. The session reviewer independently ran 103 PostgreSQL checks; the policy reviewer ran 2546 matrix cases. No remaining material finding was observed in those bounded snapshots.

## Corrections verified before acceptance

- Session rotation after idle/absolute expiry requires replacement authentication at or after that boundary; ordinary activity/rotation does not reset the original eight-hour maximum.
- Supported OIDC handler guards refuse disabled/insecure challenges, callbacks and sign-out before metadata retrieval. Negative probes observed zero metadata fetches.
- The joined fixture authorizes the actual authenticated subject. A second admitted subject cannot read the first subject's resource or replay its CSRF token.
- Typed Minimal API handlers return policy denial results to the HTTP response. Actual tests observe the intended forbidden/unauthorized statuses.
- Remote provider sign-out callback is scoped under the secure cookie path.

## Residual risks and owner actions

Live protocol validation, deployed managed-identity assertion renewal/trust removal, actual provider status/assignment/guest onboarding/privileged Conditional Access, protected production key lifecycle, separate database grants, safe migration/rollback, field filtering, append-only audit, cross-path authorization and queued-work cancellation are NOT VERIFIED. Platform and identity administrators must supply and verify those production integrations; the repository owner and authorized security reviewer must accept their evidence before enabling sign-in. The schema migration has not been applied to Azure and no active trust, permissions or customer data were created.

Independent patch assessments classified privileged-boundary impact as high and regression protection as partial; their local merge recommendation does not grant human approval, full Milestone 2 completion, G3 acceptance or production release. The synthetic fixture's mutable sequential authority adapter is not a production implementation pattern.
