# BFF browser and external identity options

Status: HISTORICAL OPTIONS with current private/organizational request below — no new access accepted
Date: 2026-10-02
Owner: Repository owner acting as technical/security owner; platform/identity/network reviewers
Related: [current preflight](azure-bff-next-session-preflight.md), [identity policy](../security/health-assessment-identity-session-design.md), [role matrix](../security/health-assessment-authorization-matrix.md), [accepted local authority/hosting](bff-production-authority-hosting-proposal.md), [Azure platform ADR](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md)

## Supplied intake

The owner authorized a private local folder accessible only to their Mac user. It was created outside Git with owner-only directory0700/file0600 permissions; ownership and absence of access-grant ACL entries were checked. Protected reference `LOCAL-INTAKE-20261002-01` contains the request and an intentionally incomplete binding template. No credentials are stored. Actual paths, accounts and protected values remain local.

Two organizational sign-in accounts were supplied, requesting Consultant and customer access. These are desired users, not verified directory records or granted assignments. The customer detailed role remains unresolved; Customer reviewer (review/confirm findings) and Executive (summary view) are existing approved role choices. Customer evidence authorization and residual-risk acceptance require their separately explicit roles and scopes. No role is inferred merely from the label customer. A personal external account was proposed for advice, and public browser reachability was requested as a feasibility option.

## External personal account feasibility

Microsoft Entra B2B can invite Gmail accounts using configured [Google federation](https://learn.microsoft.com/en-us/entra/external-id/google-federation) or a supported [email one-time passcode](https://learn.microsoft.com/en-us/entra/external-id/one-time-passcode) redemption path. The email address alone does not establish the redeemed identity provider or organizational origin. No invitation was sent, no provider setting changed and no account was enrolled.

The approved pilot profile is organizational-only. Its authority contract requires exact reviewed nonconsumer home-tenant and current home-account/session proof for external users. Google/consumer/email-OTP identity cannot be silently projected into that organizational contract. A Guest directory object, successful sign-in or resource app role does not satisfy those missing product admission conditions.

Microsoft currently documents that authentication-strength policies apply to external users authenticating with Entra; Google and email-OTP users require the MFA grant control. [External user Conditional Access](https://learn.microsoft.com/en-us/entra/external-id/authentication-conditional-access). No MFA exception, weaker authentication rule or unchanged privileged-action support can be assumed.

Options for owner review:

1. Keep the approved organizational-only profile and test external access using an explicitly onboarded user from another Entra organization. Live home-origin/account proof remains required.
2. Prepare a separate consumer-customer policy/ADR/specification/test amendment. Define allowed provider, explicit invitations/onboarding, immutable identity binding, named sponsor, scoped customer roles, MFA/privileged-action eligibility, revocation/incident/lifecycle proof and denial cases before implementing or inviting the personal account. Current external admission stays disabled.

This is feasibility advice. Neither option creates access; no consumer policy amendment is accepted here.

## Public authenticated application feasibility

Azure Container Apps supports an environment with a public inbound endpoint. The proposed experience is browser HTTPS access with Entra sign-in, explicit named-user enrollment and server-side customer/project/action authorization. Keep protected databases, evidence storage, registry, key services and operational endpoints behind their separately reviewed private boundaries. Public browser reachability is independent of customer access authority and the source collector's outbound HTTPS path.

The current foundation template creates an internal environment. Microsoft documents that an internal environment cannot be changed to accept internet traffic by toggling public network access or an app's external-ingress flag. A concrete next packet must select and review a fresh external environment or a separate ingress/edge design, with exact BFF trust and private data connectivity. [Container Apps networking](https://learn.microsoft.com/en-us/azure/container-apps/networking), [ingress visibility](https://learn.microsoft.com/en-us/azure/container-apps/ingress-overview).

Before any template/runtime change or public exposure:

- Prepare an ADR and bounded technical/implementation/test plan with exact HTTPS origin, TLS, redirect/logout bindings, proxy peer/header/canonical-host trust, internal worker isolation and only required public routes.
- Review internet-facing abuse controls, authorization/cookie/CSRF behavior, callback trust, account enumeration, ingress restrictions/rate controls and required operational monitoring. Concrete policies and any paid gateway/WAF selection require review; none are invented or enabled here.
- Retain private data connectivity, supported managed-identity federation and dedicated durable key/audit contracts; settle the existing audit-store-outage blocker.
- Test unauthenticated and unauthorized callers, wrong customer/role, spoofed host/forwarded headers, direct backend/worker access, code/nonce/state replay, MFA/CA, session revocation, key continuity and rollback. Actual deployed proof is required before live sign-in.
- Price exact quantities and session duration with charge-lag reserve inside the USD50 allowance, including retained private-service costs and any edge. Public access does not establish an affordable always-on deployment. Obtain concrete spend/access approval before provisioning.

No public application was deployed or production release authorized. The ordinary browser can use a reviewed public HTTPS origin without a VPN once this design is accepted and executed.

## Current canonical and board state

Protected intake location and desired account list are supplied. Detailed customer role, directory/licensing/CA verification, consumer identity decision, public reachability design, production composition and exact priced deployment remain open. G1–G9 and Milestone2 remain NOT VERIFIED.

Canonical records contain only the opaque protected reference and sanitized decisions. The private status-board update remains blocked by automatic approval review of the official publishing helper's credential/network path; its requested exact approval has not been received. Last confirmed BFF board snapshot4346e0f does not include this intake change. No board access or sharing was changed.


## Current owner decision — private hosting and organizational guest request

The owner retains private application hosting and replaces the personal external account request with an organizational account under protected intake `LOCAL-INTAKE-20261002-01`. Earlier Google/consumer and public-browser sections are historical options, not active implementation. Organizational home origin/current account proof is still NOT VERIFIED; email domain alone cannot satisfy admission. No invitation or assignment occurred. [The private browser access proposal](../../plans/active/private-pilot-browser-access-preparation.md) recommends one owner-operated test desktop through Bastion Developer, with [Proposed ADR-0010](../../architecture/decisions/ADR-0010-private-pilot-browser-access.md) and required security/cost/verification decisions. Customer roles and actual inputs remain unresolved; no public application, VPN or workstation has been provisioned.
