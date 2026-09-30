# Identity and Session Design: Health-Assessment Pilot

Status: Approved — implementation and provider configuration not verified  
Owner: Technical and security owners  
Last updated: 2026-09-29

## Selected identity provider

Microsoft Entra ID is the approved identity provider for pilot human and workload authentication. Application authorization remains in the product policy layer defined by `health-assessment-authorization-matrix.md`; an authenticated Entra identity does not automatically receive customer, project, evidence-category or action access.

Official Microsoft guidance recommends OAuth 2.0 authorization code flow with OpenID Connect and PKCE for browser applications, application roles for application-specific authorization, full API token validation, and managed identities for Azure-hosted workloads where available.

## Approved human authentication profile

- Use a single-tenant application registration. Organizational accounts only; personal Microsoft accounts are denied.
- Customer and partner users outside the product tenant use explicitly onboarded Microsoft Entra B2B guest identities. Guest access is never automatic and requires sponsor, customer/project assignment, role, expiry/review and revocation.
- OIDC authorization code flow with PKCE; implicit and resource-owner-password flows are prohibited.
- Use a supported Microsoft authentication library rather than hand-built protocol handling.
- Require tenant-specific issuer validation. A multi-tenant application may accept a customer tenant only after explicit organization onboarding and verified tenant binding.
- Validate issuer, audience, signature, lifetime, nonce/state, authorized party/client, and required claims on the server. Signing-key rollover must be handled through provider metadata.
- Use immutable Entra `oid` plus tenant ID as the external identity key. Email, display name and group name are presentation attributes and never authorization keys.
- Entra app roles map only to coarse application roles. The product policy engine additionally evaluates customer assignment, project/environment, action, evidence category and resource state.
- MFA and Conditional Access are required for privileged pilot roles. The concrete authentication strength, exclusions and emergency-account implementation require verification.

## Approved application-registration pattern

- Create one single-tenant confidential web application registration for the pilot UI/backend-for-frontend. Register only approved HTTPS web redirect and logout URIs per environment. Do not enable public-client, implicit or wildcard redirect behavior.
- The UI/backend-for-frontend does not expose a general delegated API scope to browser JavaScript. Its coarse Entra app roles are `PilotConsultant`, `PilotCustomerUser`, `PilotAuditor` and `PilotPlatformOperator`; detailed customer roles and every resource/action/evidence decision remain in the product policy store.
- Create a separate API/resource and client registration only when Phase 1D MCP is implemented. Its approved scopes are read-only and cannot be inferred from the UI registration.
- Azure-hosted worker classes use separate managed identities. They do not require ordinary human-login app registrations and never reuse the UI application's delegated authority.
- Registration object IDs, client IDs, tenant ID, redirect URLs and credential references are environment configuration and may be supplied during provisioning. Their absence does not block approval of this design.
- The confidential web client uses a dedicated user-assigned managed identity federated to this app registration as its client credential. Trust requires exact tenant issuer, managed-identity principal ID subject and `api://AzureADTokenExchange` audience. The BFF does not configure a client secret or certificate private key. Sign-in remains disabled until full authorization-code redemption and session creation pass the Container Apps deployment spike.

## Approved session profile

- Use a server-managed session in the control-plane data store. The browser receives only an opaque, rotated session identifier in a `Secure`, `HttpOnly` cookie; Entra access and refresh tokens are not exposed to ordinary application JavaScript.
- Use `SameSite=Lax` unless an approved integration demonstrably requires a narrower exception. Narrow cookie path/domain and apply synchronizer-token or equivalent CSRF defense to every state-changing request.
- Default idle lifetime is 30 minutes and default absolute lifetime is 8 hours. Activity may extend the idle limit but never the absolute limit. Background assessment work continues under its workload identity after the initiating user's session ends.
- Require Entra reauthentication at the absolute limit and when the application cannot revalidate provider status within 15 minutes. Do not silently extend the application session beyond the Entra/provider session and Conditional Access result.
- Require authentication no older than 15 minutes, with the required MFA/Conditional Access context, for protected-evidence authorization, residual-risk acceptance, report publication, report-link creation, deletion and security/role administration. The user returns to the intended action only after authorization is evaluated again.
- Assignment, role, customer and resource-state revocation are checked from authoritative product state on every request. A monotonically increasing security version invalidates all of the subject's sessions immediately after a product-side change; no authorization-result cache may exceed one minute.
- Rotate the local session identifier at sign-in, privilege change and privileged reauthentication. Concurrent sessions are visible and individually revocable; a subject-wide revoke ends all product sessions.
- Logout revokes the local session and clears local credentials; provider-session logout behavior is documented without claiming global sign-out guarantees.

## Approved workload identity profile

- Azure-hosted workloads use managed identities where supported.
- Non-Azure workloads use workload identity federation or certificates; client secrets are a last resort requiring explicit security approval and managed rotation.
- Workload tokens use app roles/static consent and are narrowed again by the application's one-customer, one-project, one-job scope.
- Interactive delegated tokens are never reused by workers. Share links and MCP identities are distinct identity types.

## Provisioning and lifecycle

- Pilot access is assigned through approved Entra app roles/groups plus an explicit product-side customer/project assignment.
- Role/group claims never substitute for product-side tenancy assignment.
- Join, role change, customer reassignment, suspension, termination and guest lifecycle events must produce attributable product-side updates or denial.
- Guest/B2B access is limited to the approved explicit onboarding flow with tenant trust, sponsor, expiry and access review.
- Pilot guest assignments expire after no more than 90 days unless renewed and are reviewed at least every 30 days and on sponsor/engagement change.
- There is no application-local break-glass password or authorization bypass. Entra tenant emergency-access accounts remain an identity-platform operational control and receive no application access unless they also have an explicit, time-bounded product assignment. They cannot bypass customer, evidence-category or audit policy.

## Audit and incident controls

Record sign-in correlation, tenant/object ID, application/client ID, authentication method context where available, product assignment decision, session creation/revocation, role/assignment changes and denied authorization outcomes without tokens or sensitive claims. Entra sign-in logs remain a provider record; product audit records link through safe correlation identifiers.

Compromised identity response must support immediate product-session revocation, assignment disablement, workload credential/federation revocation, queued-job cancellation where authority was revoked, share-link review and customer-scoped investigation.

## Provisioning and implementation decisions

- Select the supported phishing-resistant authentication strength and document any Entra Conditional Access exclusions during tenant provisioning; exclusions cannot weaken product authorization.
- Verify the approved managed-identity federation path in the deployment spike, including multiple replicas, assertion renewal, wrong issuer/subject/audience/tenant denial, trust/identity removal, session revocation and no token/credential leakage. If unsupported, a Key Vault-backed certificate requires an explicit plan/security change; a client secret requires separate explicit security approval.
- Define the named operational owner and incident path that triggers immediate product-side subject/session revocation when an Entra account is compromised or disabled.

Actual registration names, object/client IDs, tenant ID, redirect URIs, managed-identity IDs and certificate/secret references are deployment inputs. They are recorded and verified before pilot execution, not approved as durable architecture values.

## Verification requirements

- Valid sign-in plus wrong issuer/tenant/audience/client, expired/not-yet-valid, altered signature, nonce/state/PKCE replay and signing-key rollover.
- Personal-account and unonboarded-tenant denial.
- App-role and product-assignment combinations, including group overage or missing claims.
- Revocation during an active UI session and queued job.
- CSRF, cookie, token leakage and session fixation tests.
- Workload cross-customer/job scope and credential-rotation tests.
- BFF federated-credential authorization-code redemption, assertion renewal and negative trust tests in Container Apps.
- Privileged action MFA/recent-authentication and break-glass audit tests.

## References

- [Microsoft identity platform authorization-code flow](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
- [Microsoft Entra application, API, and workload authorization](https://learn.microsoft.com/en-us/entra/architecture/authorize-applications-resources-workloads)
- [Microsoft Entra application roles](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)

## Approval

Technical owner: Repository owner  
Security owner: Repository owner  
Date: 2026-09-29

Initial tenancy, guest and MFA policy approved by Repository owner on 2026-09-28. Application-registration, workload and session defaults approved by Repository owner on 2026-09-29.
