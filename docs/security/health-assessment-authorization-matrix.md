# Health-Assessment Pilot Authorization Matrix

Status: Approved  
Owner: Security owner  
Last updated: 2026-09-28

## Policy model

Every decision is server-side and deny-by-default. Access requires all of:

1. authenticated active named-user/service/workload identity;
2. active assignment to the resolved customer and, where applicable, project/environment;
3. allowed actor role and action;
4. allowed resource state and evidence category;
5. customer policy permitting the data class, export, AI, sharing or MCP operation;
6. no deletion, retention, revocation, legal/security hold or capability denial that blocks the action.

Partner membership alone never grants customer access. Customer roles never cross their organization. A worker receives one customer, project, run, job type and action set. A share-link viewer is not an application identity and can read only its pre-rendered redacted artifact.

## Human-role matrix

`A` allowed when scoped and policy-valid; `D` denied; `C` allowed only with the named additional condition.

| Action | Consultant | Qualified customer reviewer | Customer evidence authorizer | Customer risk owner | Executive | Auditor | Platform support |
|---|---:|---:|---:|---:|---:|---:|---:|
| View authorized dashboard/findings | A | A | C: if separately granted viewer | A | A: summary/excerpts | A: approved records | C: metadata by support policy |
| View normalized evidence excerpt | A: category permission | A: category permission | C: if separately granted viewer | C: if separately granted viewer | D by default | C: approved report/provenance | D by default |
| View protected raw evidence | C: customer authorization plus dedicated permission | C: customer authorization plus dedicated permission | A: may authorize, not automatically view | C: if separately authorized | D | D by default | D under approved pilot policy |
| Configure/start pilot assessment | A | D | D | D | D | D | D |
| Configure profile/rules/outcomes/AI budget | A | D | D | D | D | D | D |
| Override AI budget | A: audited | D | D | D | D | D | D |
| Review/confirm/reject finding | A | A | D unless reviewer role also assigned | D unless reviewer role also assigned | D | D | D |
| Edit presentation/business context | A | A when granted | D | D | D | D | D |
| Accept residual risk | D | D unless customer risk role also assigned | D | A with required fields | D | D | D |
| Create recommendation/fix package/task | A | C: comment/review only | D | D | D | D | D |
| Export consultant tasks CSV | A: export policy | D | D | D | D | C: if explicit export grant | D |
| Publish report | A with warning acknowledgment | D | D | D | D | D | D |
| Acknowledge report | D | D | D | A | C: if assigned acknowledgment authority | D | D |
| Create/revoke expiring report link | A: export/share policy | D | D | D | D | D | D |
| Delete within approved policy | A: explicit deletion permission | D | D | C: if separately granted admin | D | D | D |
| View audit history | C: project/business audit subset | C: related review history | C: authorization events | C: risk/ack events | D | A: approved scope | C: operational metadata only |
| Run remediation or migration | D | D | D | D | D | D | D |

Holding multiple customer roles is permitted only when explicitly assigned; each action evaluates the relevant role independently. Consultant authority never satisfies customer risk acceptance or protected-evidence authorization.

## Non-human identity matrix

| Identity | Allowed | Explicitly denied |
|---|---|---|
| API/UI application | Policy evaluation, scoped canonical reads/writes, enqueue opaque work | Direct source DB, broad cross-customer queries, raw credential access |
| Assessment worker | One job's normalized evidence, rule/profile inputs, checkpoint/results | Interactive session reuse, other customer/project/run, raw evidence, publication approval |
| AI worker/gateway | Minimized packet creation, approved provider call, schema/citation validation, budget events | Raw evidence, arbitrary tools/network, secrets, customer-system access, policy changes |
| Renderer | Signed frozen render manifest and one output target | Network, database, credential/key store, arbitrary filesystem, canonical writes |
| Purge worker | One approved retention/deletion work item and deletion ledger | Read/display evidence content, change policy, restore |
| Backup/restore operator | Approved backup/restore control plane and isolated recovery workflow | Ordinary customer application access; bypass tombstone/link revocation replay |
| MCP identity | Read status, coverage, scores, findings, recommendations and protected-reference metadata within grants | Raw evidence, start/run, edit/disposition, risk acceptance, publication, export/link/task creation, mutation |
| Share-link viewer | One pre-rendered redacted report until revoke/expiry/passcode lock | Canonical APIs, raw/protected evidence, other report versions, search/index |

## Evidence-category controls

Evidence categories are independently grantable. A broad project-reader role does not imply protected code/script, authorization design, identity relationship, operational log or raw evidence access. Serialization filters fields before response construction; denied fields produce an authorized redaction marker where product requirements require visibility, without revealing the value.

AI access is a separate project/run/category policy. MCP access is a separate identity/action/category grant. Export and sharing are separate permissions and customer-policy gates. Access to a finding never automatically grants its underlying evidence value.

## Resource-state controls

- Soft-deleted or expired resources are denied to ordinary users/workers even if their former role remains active.
- Published versions are read-only; edits create later run/report state.
- Suspended capability/rule rows cannot start new work.
- Revoked partner/customer assignment invalidates sessions/tokens/caches at the next request and cancels unstarted work authorized solely by that assignment.
- Risk acceptance past its review/expiry date becomes review-required and cannot be presented as current acceptance.
- Share links are denied after expiry, revocation, report deletion, customer-policy change or failed-attempt lockout.

## Required decision tests

For every allowed action, test authenticated success plus unauthenticated, wrong role, wrong customer, wrong project, wrong environment, wrong assessment, wrong evidence category, stale assignment, revoked identity, suspended identity, deleted resource, expired link, stale revision and direct-identifier substitution. Tests must cover UI/API, queued work, export, renderer input, share viewer and MCP rather than treating UI hiding as evidence.

## Open decisions

- Platform support protected-evidence access is denied for the pilot. Any future time-bounded customer-authorized elevation requires a separate approved design and must not be inferred from support responsibility.
- Microsoft Entra ID single-tenant registration, explicit B2B onboarding, MFA/Conditional Access, BFF registration, coarse app roles, managed workload identities, session/recent-authentication lifetimes, guest review, revocation and no-local-break-glass defaults in `health-assessment-identity-session-design.md` are approved and remain `NOT VERIFIED` in implementation.
- Audit records follow the approved 12-month payload-free lifecycle and business/security visibility rules in `health-assessment-audit-policy.md`.

## Approval

Approved by: Repository owner  
Date: 2026-09-28
