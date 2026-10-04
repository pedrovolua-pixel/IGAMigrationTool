# Phase 1D cycle02 owner intake

Status: OPEN — required publication and client inputs not supplied
Requested from: Phase1C reporting owner, technical and security owners; identity and operations owners for their evidence
Date: 2026-10-03
Readiness source: [committed inventory and gaps](phase1d-integration-readiness-cycle02.md)

This is a source-backed handoff template. Copy it for the exact reviewed contract. Contract documents and fictional fixtures may be repository artifacts; customer payloads, protected identifiers, credentials and provisioning values belong in protected evidence, referenced by approved opaque artifact references. No secret values are requested here.

## Reader entry inputs

| ID | Required input | Requested owner | Current state / what completes it |
| --- | --- | --- | --- |
| P02-I01 | Publisher source revision, approved ReportVersion/manifest schemas and historical version support; server-owned exact-version lookup; source-bound fictional publication bytes and original digests | Reporting + technical | NOT PROVIDED. Attributable reviewed contract and publisher conformance evidence, not a draft/evaluation capture. |
| P02-I02 | Atomic manifest/frozen-score visibility, expected revision, idempotency/retry and incomplete-render rules; frozen approval and warned-unreviewed/gap semantics | Reporting | NOT PROVIDED. Exact publisher guarantees mapped to FR45/46 and TP006/017, preserving allowed warned publication. |
| P02-I03 | Native scope/assessment/report/run revision and baseline/catalog/profiles/application/approval bindings; reviewed native integrity scheme and original-versus-serving mapping; any per-item, response or grant commitment only if separately selected and approved | Reporting + technical | NOT PROVIDED. Closed schema/mapping with canonical bytes and independent integrity evidence; no transformed hash relabeled original. |
| P02-I04 | Each six-resource field, category, state and reference-link mapping; explicit text/provenance/original/comment classification, minimization and unknown-field handling before protected loading | Reporting + security + technical | NOT PROVIDED. Complete reviewed mapping and adversarial fictional corpus; AutoConfirmed/proposed and provisional/publishable/quality labels preserved explicitly. |
| P02-I05 | Authoritative current report/evidence deletion, expiry, hold and availability sources, retention class/clock-start and revision changes; tombstone/revocation recovery replay | Technical + security + operations | NOT PROVIDED. Exact enforcement wiring and race/recovery expectations under existing approved policy; original manifests remain immutable. |
| P02-I06 | Central identity/assignment/customer/action/category/field policy and server scope routing; no UI/share/worker inference; revocation/final-emission boundary | Security + technical | NOT PROVIDED. Reviewed separate MCP grant contract and current-authority composition; a test fence cannot establish production authorization. |
| P02-I07 | Audit event schema/durability/identity/correlation, payload-free fields, one invocation vs retry/delivery definition, outage handling and deadline/cancellation around irreversible completion | Security + operations + technical | NOT PROVIDED. Reviewed exact real-port semantics and independent failure/race evidence; no inherited end-to-end5second claim. |

For each completed row record: decision author/role/date, exact source commit and contract SHA256, approved schema/version, permitted fixture/evidence reference, acceptance-test IDs, unresolved cases and sign-off. Current reader implementation entry is NOT READY until all applicable rows and the candidate implementation/test plans are approved. Inventing placeholder published IDs or copying fictional approval labels does not complete a row.

## External exposure inputs — a separate later handoff

| ID | Required input | Requested owner | Current state / what completes it |
| --- | --- | --- | --- |
| P02-I08 | Supported client name/version/identity mode and required read workflow; exact protocol/version/transport/SDK/platform, resource/method schemas, public paths/error/paging/compatibility contracts | Technical with security review | NOT PROVIDED. Concrete bytes and client matrix approved under technical-spec Interfaces and contracts. No client/SDK/version selected by this template. |
| P02-I09 | Distinct API/resource and client model; named-user/service issuer/tenant/audience/client/consent/scopes/app roles and independent product action/category grants; signature/expiry/revocation/key rollover expectations | Security + technical + identity | NOT PROVIDED. Exact approved design and negative-token/grant plan. Actual registration identifiers/configuration evidence arrive later through protected provisioning. |
| P02-I10 | Intended topology/workload, approved identity/customer/distributed limits, safe failure/fairness/dependency behavior; authenticated snapshot cursor binding/expiry/replay/key rotation/replica/restart and audit/deadline mechanism | Operations + technical + security | NOT PROVIDED. Explicit mechanisms/values and measured test/rollback/disablement ownership, not silent reuse of fixture numbers. |
| P02-I11 | Configured deployment, private routing and client integration evidence, customer isolation/revocation/retention/audit verification, staged activation/rollback scope and human authority | Operations + security + technical | NOT PROVIDED. Deployed evidence and explicit activation authorization after contract and applicable gate prerequisites. Preparation or local tests cannot activate it. |

Client intake rows should identify exact requested user workflows and read resources, not broad “all MCP clients” compatibility. Record real client versions only when supplied and verified for the selected contract. Public errors must be explicitly approved without exposing resource existence or protected values. Existing prohibition of raw/business operations remains total.

## Staged completion

1. Reporting owner delivers I01–I05 publication/minimization/lifecycle contracts; technical/security/operations supply I06–I07 composition decisions.
2. Owners approve the exact isolated reader candidate/test packet; independent fixtures and internal freeze precede code. Execute the actual publisher/reader conformance checks before claiming integration.
3. Complete and approve I08–I10 external contract and independent test matrix. Exact deployment identifiers are provisioning evidence, not substitutes for design approval.
4. Complete I11 configured verification and explicitly authorize only the reviewed activation scope. Full feature/UAT/gate acceptance remains separate.

Reader and external parts are recorded independently. A client contract can be prepared while publication work continues, but cannot supply a published source. Existing P1D-D01–03 local approval remains closed; this intake requests missing inputs rather than repeating that approval.
