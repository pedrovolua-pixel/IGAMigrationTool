# Accepted local BFF authentication handoff

The owner approved exact D01/D02 on 2026-10-02. [OpenAPI v1](../../contracts/bff-authentication/bff-v1.openapi.json) freezes the three operations and [the cycle](../../plans/completed/bff-authentication-contract-cycle.md) owns source, checks, review and publication. This implementation is reusable local code; the existing diagnostic executable cannot activate it.

## Composition

Register the foundation with the shared store and trusted subject authority. Explicitly register `AddBffPublicAuthenticationTransport`; order authentication, `UseBffPublicAuthenticationEndpoints(new() { FixedOrigin = "https://exact-host[:port]" })`, then generic `UseBffRequestProtection`. The fixed canonical origin has no wildcard, path, caller return URL or forwarded-header trust. The additional-data provider uses only the fully validated exact-session feature. Anonymous issuance is confined to the public transport and framework synchronizer cookie.

`GET /bff/v1/session` supplies only version, authentication state and CSRF token. Ordinary authenticated-request idle bookkeeping follows existing policy; this operation creates/revokes no store ticket and changes no product authority. The unused web helper obtains the token then submits a native hidden form to sign-in; it never fetches a provider redirect. Local sign-out accepts only the bounded empty JSON body and exact-session token, awaits removal before clearing the cookie, and preserves other sessions. It makes no provider/global logout claim.

## Challenge and session evidence

Supported OIDC code/PKCE/state/correlation/nonce and signature/issuer/audience checks retain protocol ownership. The supported token validator requires the original signed scalar integer `auth_time`. Explicit supported token-response validation covers Microsoft's manually handled redemption path, whose framework branch otherwise skips nonce validation. The protected challenge carries an opaque random reference and issuance timestamp; shared PostgreSQL consumption is single-use, with deadline rechecks after row-lock waits and asynchronous completion. Invalid protocol requests never consume a challenge; later persistence failure may burn one without admitting a session.

Current subject eligibility supplies a reviewed cutoff, monotonic version and authoritative roles. Guest evidence binds the exact reviewed organizational home tenant to signed supported provider forms. Missing/unknown evidence denies. Original authentication and the exact stored session cannot borrow another session's time or MFA. Privileged verification stays default-denying.

## Migration and development checks

Additive `migrations/identity-sessions/002-authentication-context.sql` adds nullable provider cutoff and pending challenges. Existing null-cutoff rows deny until trusted administration supplies evidence. Apply001 then002 only through an explicit migration identity; no host startup migration or live schema/grant is introduced. No cleanup/retention policy is selected here.

The new executable actual-HTTPS transport harness, supported signed synthetic protocol harness, shared PostgreSQL/race tests and native Chromium helper test run under the pinned repository dependencies. [Sanitized evidence](evidence/bff-authentication-contract-20261002.json) records executed results and failures; [independent review](evidence/bff-authentication-review-20261002.md) records non-author verification. Existing configured Linux/Windows/browser/infrastructure/container checks remain required. Source and locks enter the existing Docker context through exact allowlist entries.

## Remaining next outcome

Prepare concrete D03/D04 production enrollment/assignment and current provider/app-role/guest-origin/cutoff contracts; then exact proxy/key/audit composition with durable issuance/revocation semantics. Those consequential inputs require their own review and owner decision. Actual Entra claims, managed-identity redemption, Conditional Access, signing-key rollover, shared deployed keys, audit and multi-replica activation remain NOT VERIFIED. Image/license/scan acceptance, signed retained provenance and private registry publication/pulls remain D05 dependencies. A new Azure session needs refreshed cost/capacity/what-if, exact bindings and separately approved spend/access; the $50 alert budget does not stop spending. Draft publication is not merge, release or live gate acceptance.
