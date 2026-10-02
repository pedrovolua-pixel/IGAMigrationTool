# Composable BFF transport foundation

This class library implements reviewed identity/session transport building blocks. It exposes no product endpoint and does not enable live sign-in by default. The existing Azure bootstrap and local synthetic demo remain separate.

A host must inject the shared PostgreSQL `ITicketStore`, a trusted `IBffSubjectAuthority`, explicit tenant/app/dedicated managed-identity GUID configuration, and the shared protected Data Protection key ring. Register `AddBffFoundation`, then compose `UseAuthentication` followed by `UseBffRequestProtection` before every host-owned product endpoint. Hosts own endpoint schemas, product policy/resource routing, audited revocation and logout, safe read-only method semantics, reviewed HTTPS proxy trust, and antiforgery request-token delivery. The foundation maps no login/logout/token routes. Cookie/callback paths are host configuration and must remain narrow within the same application scope. Tokens are never returned through a library endpoint.

The subject adapter checks active status, product assignments, explicit B2B onboarding, trusted provider status and authentication time, security version and authoritative coarse roles. It binds every result to the requested immutable subject. Missing/unavailable/stale adapters deny access. MFA/Conditional Access is supplied only by a trusted provider adapter; `amr`, email, name, groups and browser inputs do not prove permission or MFA. Detailed per-customer/action decisions belong to the separate policy module, including recent authentication and session-bound privileged context. A role/version/assignment change must update authoritative security version; the cookie and request middleware check authority on every request.

## Microsoft federation and code redemption

Exact dependency: `Microsoft.Identity.Web` 4.15.0, with locked transitive packages. Registration uses only the supported `SignedAssertionFromManagedIdentity` credential source and the dedicated managed identity's client ID, never a secret or certificate. It enables the library's MSAL authorization-code redemption handler with no initial downstream scopes; requested OIDC scopes are only `openid` and `profile`. This registration is necessary: plain `AddMicrosoftIdentityWebApp` does not install the MSAL code-redemption path in this version.

MSAL may add reserved protocol scopes internally, including `offline_access`. Token responses may therefore transiently contain access/refresh tokens in redemption memory. The discard cache provider clears the MSAL cache before and after access, writes no token cache, and `SaveTokens=false` prevents cookie/session token storage. The reduced ticket contains only immutable `tid`/`oid`, approved authoritative coarse roles, and trusted server properties agreed with the session module. No downstream API invocation or Graph consent is added.

Sources: [Microsoft certificateless guidance](https://learn.microsoft.com/en-us/entra/msidweb/authentication/certificateless), [4.15.0 code-redemption composition](https://github.com/AzureAD/microsoft-identity-web/blob/4.15.0/src/Microsoft.Identity.Web/WebAppExtensions/MicrosoftIdentityWebAppAuthenticationBuilder.cs), [Microsoft issue 3631](https://github.com/AzureAD/microsoft-identity-web/issues/3631).

## Local verification and limits

The executable unit harness checks configuration refusal, immutable identity/admission negatives, real cookie authentication and attributes, authority/version revocation, HTTPS refusal and real synchronizer antiforgery validation with synthetic identities and an in-memory test ticket store. These are internal test compositions, not product login endpoints. The coordinator adds PostgreSQL/two-instance integration evidence separately.

Live Entra code redemption, managed assertion renewal, nonce/state/PKCE replay, signing-key rollover, trust-removal, multi-replica provider behavior, provider logout, licensed users and reviewed Conditional Access remain unverified. Local options/middleware assertions do not prove those provider cases. Live sign-in activation and Azure deployment require their existing gates.
