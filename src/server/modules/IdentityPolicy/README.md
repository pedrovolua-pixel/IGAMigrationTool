# Internal human policy foundation

Implements the approved human matrix in `docs/security/health-assessment-authorization-matrix.md` and identity/session design. This is an internal service module, with no HTTP operation, trusted provider adapter, production policy repository, response serializer or live sign-in activation.

`HumanAuthorizer.AuthorizeAsync` requires an authenticated immutable tenant/object subject from the host and a trusted `IHumanAuthoritySnapshotSource` implementation. The adapter resolves every request from current server state, including the actual resource scope, revision, category, available action set, projection, policy, assignment and session security version. Client identifiers only select a target; they cannot provide the authoritative snapshot. The pure evaluator is internal. No adapter is registered here. Missing adapters and provider failures deny access.

`IPrivilegedAuthenticationVerifier` separately verifies required MFA/Conditional Access and recent authentication for the exact subject, session, security version and action. Presentation claims cannot implement this adapter. Authentication age is at most 15 minutes; provider status has the same bound. Authoritative snapshot age has the approved maximum one-minute cache bound. Future timestamps deny access. Every call resolves authority anew.

Roles and conditions are evaluated independently. Coarse Entra roles never replace a product assignment. Category permission is explicit on each assignment and intersected with current customer policy. Returned projection is an authorization constraint: downstream code must resolve only that exact projection and filter fields before serialization. This module does not perform evidence serialization, audit writing or customer routing.

## Matrix mapping and limitations

- Raw evidence viewing and protected-evidence authorization are separate internal actions. An evidence authorizer alone can authorize; viewing additionally requires an explicit separate viewer grant, customer authorization and dedicated protected permission. Consultant authority never accepts residual risk.
- Recommendation creation and recommendation comment/review are separate actions so a customer reviewer cannot create packages. Review/confirm/reject finding remains one policy action.
- Per-role `GrantCondition` values represent facts verified by the server adapter from current product records, never flags submitted by a caller. `RiskRequiredFields` requires the adapter to validate customer owner, rationale, compensating controls, authorization and a review/expiry date no later than one year. Actual mutation payload validation remains outside this pure policy slice. Warning acknowledgment, export permission, audit/provenance scope and deletion permission likewise require authoritative evidence.
- Dashboard and audit projections preserve executive summary/excerpts, auditor approved scope and support metadata limits. Support cannot view raw or normalized evidence. Auditor export requires its own explicit grant.
- Published content is immutable: only reads and separately approved report acknowledgment, report-link creation/revocation and policy deletion may be allowed. `ResourceAllowedActions` must also bind those lifecycle operations to a resource that supports them; this is not a grant to mutate arbitrary published records. All content edits are denied.
- Combined profile/rules/outcomes/AI-budget configuration conservatively requires AI policy permission. A distinct non-AI configuration action is not introduced without its approved contract.
- Security/role administration is denied because this matrix does not grant it. Remediation/migration is always denied. Worker, share and MCP identities are not accepted by this human contract.
- Guest onboarding requires an active sponsor, explicit assignment, maximum 90-day expiry and a review within 30 days; sponsor/engagement changes deny access until a new reviewed record exists.

The module is not complete identity acceptance: real authoritative adapters, provider verification, cross-path authorization, persistent revocation, serializer redaction, audit and deployed BFF federation remain unverified.
