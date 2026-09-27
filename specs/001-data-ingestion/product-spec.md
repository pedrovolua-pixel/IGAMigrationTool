# Feature: Multi-source data ingestion

Status: Draft

Owner: Product owner

Created: 2026-09-27

Last updated: 2026-09-27

## Summary

Provide read-only, traceable ingestion of supported Identity Governance and Administration evidence from SailPoint Identity Security Cloud and One Identity Manager. A project selects one logical source system, but may combine multiple acquisition endpoints for that source. The product supports hosted connections, customer-side collection, and file uploads; creates immutable evidence baselines; and preserves both normalized objects and source-native provenance for health assessment and later migration work.

Health assessment supports SailPoint and One Identity Manager sources. Migration remains sequenced: SailPoint to Veza first, followed by SailPoint to One Identity Manager and One Identity Manager to Veza, with One Identity Manager to SailPoint still planned. Each non-initial migration path requires separate destination-mapping discovery and approval.

## Problem

IGA configuration and operational evidence is distributed across APIs, databases, product services, exports, reports, scripts, schemas, and logs. A single acquisition path may be incomplete, customer networks may prohibit hosted access, and vendor-native concepts do not map cleanly into a shared model. Without explicit provenance, redaction, checkpointing, and gap handling, an assessment or migration can appear complete while relying on partial or inconsistent evidence.

The product must acquire enough evidence to document and assess the source while remaining read-only, minimizing identity data, excluding secrets, preserving native semantics, and making incomplete or conflicting evidence visible.

## Goals

- Support SailPoint Identity Security Cloud and One Identity Manager as source systems for health assessment.
- Allow exactly one logical source system per project while combining multiple acquisition endpoints for that source.
- Support hosted connections, customer-side collection, native exports, and structured uploads.
- Create immutable, versioned evidence baselines with source-native provenance and complete collected-inventory relationships.
- Support resumable full and incremental collection, reconciliation, partial success, and repeat comparison.
- Minimize identity and account data while retaining references required for ownership, approvals, assignments, static membership, and migration matching.
- Preserve One Identity Manager native types, UIDs, versions, modules, schema references, and customization evidence alongside the normalized model.
- Enforce configurable exclusion, redaction, raw-evidence retention, and customer-side handling policies.
- Give users an explicit preview and capability matrix before collection begins.

## Non-goals

- Combining multiple logical source systems in one project.
- Merging identities or governance objects across SailPoint and One Identity Manager within one project.
- Supporting One Identity Manager version 11 or later without separate compatibility review.
- Approving or implementing non-initial source-to-destination mappings in this specification.
- Migrating completed certification or attestation history or reviewer decisions.
- Collecting passwords, private keys, connection secrets, or unrestricted identity and account profiles as evidence.
- Guaranteeing support for future or customer-custom authentication modules that are not in the applicable capability matrix.

## Users and preconditions

- An authorized partner administrator, customer administrator, consultant, or platform operator is assigned to the customer and engagement.
- The project identifies exactly one logical source system, environment, product version, acquisition methods, scope, exclusions, redaction policy, retention policy, and schedule.
- One Identity Manager source versions are limited to 8.x through 10.x. Each source version and acquisition method must appear in the supported capability matrix.
- The customer has authorized the read-only access paths and data boundary used for collection.
- Customer administrators control installation, enrollment, configuration, upgrade policy, and removal of customer-side collectors.

## User flows

### Primary flow

1. An authorized user selects SailPoint Identity Security Cloud or One Identity Manager as the project's sole logical source.
2. The product shows supported versions, acquisition methods, authentication mechanisms, evidence categories, and known limitations.
3. The user configures one or more endpoints for that logical source, scope, exclusions, redaction, raw-evidence handling, retention, and schedule.
4. The product previews requested object types and relationships, estimated volume, permissions, included and excluded fields, redaction and retention rules, expected API usage, expected duration, and unsupported or inaccessible categories.
5. The product validates connectivity and reports excessive permissions as a warning. The unresolved least-privilege conflict in this specification must be resolved before approval.
6. The collection runs without modifying the source, adapting to rate-limit feedback and saving resumable checkpoints.
7. Evidence from multiple endpoints is merged into one immutable baseline while retaining value-level provenance and visible conflicts.
8. The product activates a complete or partial baseline. Partial baselines activate with prominent warnings and explicit gap states.
9. Health assessment evaluates every supported, installed, in-scope object or records an explicit gap state.

### Alternative flows

- A user uploads JSON, JSON Lines, CSV, ZIP, or a vendor-native export instead of using a live connection.
- A generic file starts with pre-populated mappings that the user may adjust and save for the customer, product, environment, and export version.
- A customer-side collector runs unattended, queues encrypted results while offline, or produces an encrypted offline package for health assessment only.
- An authorized user performs an ad hoc refresh for selected objects. Refreshed evidence merges into the active baseline while untouched categories retain their original collection times.
- A connection expires during collection. The run waits at its checkpoint and resumes after reconnection.
- One endpoint fails while others succeed. The merged baseline may activate with prominent endpoint and coverage gaps.
- An unsupported or unverified source version may use uploads for reduced-trust health assessment but may not proceed to migration.

## Functional requirements

### FR-ING-1: One logical source per project

Each project must select exactly one logical source system. A source may use multiple endpoints and acquisition methods, but the product must not combine separate SailPoint and One Identity Manager source systems in one project.

### FR-ING-2: Supported source products and versions

The product must support SailPoint Identity Security Cloud and One Identity Manager as health-assessment sources. One Identity Manager versions 8.x through 10.x are in scope; version 11 and later require separate review.

### FR-ING-3: Read-only source boundary

Collection, analysis, migration, validation, retry, and recovery must not create, update, or delete data or configuration in any configured source system.

### FR-ING-4: Acquisition methods

The product must support hosted connections where reachable, customer-side collectors, native exports, and JSON, JSON Lines, CSV, ZIP, and vendor-native upload packages. One Identity Manager must additionally support available product API or application-service access, dedicated read-only database access, useful read-only reports, and exported product configuration.

### FR-ING-5: Connection lifecycle

All authorized engagement roles may enter, replace, pause, or revoke credentials and connections. Connections are scoped separately by customer environment and remain reusable until revoked. Revocation must offer explicit choices to stop new collection, cancel active jobs, delete retained credentials immediately, and separately delete previously collected evidence.

### FR-ING-6: Authentication compatibility

SailPoint connections must support OAuth client credentials, interactive OAuth, and personal access tokens. One Identity Manager authentication candidates include OAuth, SQL credentials, system users, client secrets, and JWT where listed in the applicable version/deployment capability matrix.

### FR-ING-7: Permission validation

The product must test source connectivity and detected permissions before collection. It must warn when permissions exceed the documented read-only minimum and must not use source-write operations. Whether warned overprivileged credentials may proceed remains blocked by the conflict with approved `NFR-SEC-1`.

### FR-ING-8: Snapshot and incremental collection

Every completed run must create an immutable, versioned evidence baseline. Collection must use an initial or reconciled full snapshot plus incremental ingestion, preferring reliable object modification timestamps and using vendor cursors, events, checksums, and comparison with the previous baseline as available.

### FR-ING-9: Reconciliation and deletion

Full reconciliation must default to every seven days and be customer-configurable. Missing source objects must become tombstones in the new baseline while remaining available in prior baselines. A scope, exclusion, or redaction-policy change does not automatically require a full collection.

### FR-ING-10: Scheduling

Collection must support on-demand and scheduled operation with a minimum daily cadence. Schedules are configured separately by source environment and acquisition method and follow the customer environment's timezone. Overlapping scheduled runs are skipped. A run missed while offline does not catch up and waits for the next cycle. Notifications are configurable and default to errors or degraded outcomes only.

### FR-ING-11: Targeted refresh

Authorized users may run ad hoc synchronization for selected object types or individual objects. New evidence must merge into the active baseline without changing the recorded collection times of untouched categories.

### FR-ING-12: Retry and checkpoint behavior

The product must honor vendor rate-limit feedback, use adaptive backoff, cache completed work, and resume after reconnection. Automatic retry duration is customer-configurable with a 24-hour default, after which the run pauses for human action. Checkpoints must cover API pages or cursors, object types, individual files, upload chunks, and record batches, prioritizing API-page continuity.

### FR-ING-13: File constraints and safety

Uploads are limited to 100 MB per file and 100 MB total per collection and must support resumable or chunked transfer. The product must scan uploads for malware, archive bombs, unsafe paths, unexpected executable content, and uninspectable content. Password-protected or encrypted uploads must be rejected.

### FR-ING-14: Import mapping and manifests

Generic CSV and JSON imports must provide pre-populated mappings that users may adjust and save. Optional manifests may describe tenant, environment, export time, product version, object types, counts, and checksums. Missing manifests must not block ingestion but must produce a reduced-trust warning. Schema changes should trigger automatic remapping with warnings and require review only for unresolved mappings.

### FR-ING-15: Strict and partial imports

Users must be able to select strict rejection or partial import. Missing required relationship keys must fail visibly. Partial mode may activate valid evidence with rejected-record details, explicit gaps, and warnings.

### FR-ING-16: Duplicate and relationship handling

The product must deduplicate repeated pages, records, and uploads using source identifiers, checksums, and checkpoints. ZIP packages may contain multiple related files, and relationships must be resolved across those files. Unknown permitted fields must remain available as unmapped source-specific evidence rather than being silently discarded.

### FR-ING-17: Baseline comparison and status

Users must be able to compare any two baselines for additions, changes, deletions, newly inaccessible evidence, and newly redacted evidence. Collection states must include queued, validating, collecting, rate-limited, waiting for reconnection, partially complete, reconciling, complete, complete with warnings, failed, canceled, and expired. The product does not label a baseline stale.

### FR-ING-18: Evidence precedence and conflicts

When multiple methods contribute to one source baseline, the product may suggest this precedence: direct current-state metadata, native configuration export, vendor report, then generic customer file. It must retain separate provenance for every value, show conflicting values, and require resolution rather than silently selecting evidence.

### FR-ING-19: Metadata-first identity and account scope

The product must not collect general identity profiles or account values by default. It may collect source identifiers, email, employee ID, account ID, ownership, approval, assignment, static membership, and relationship references required for supported analysis and migration matching. Customers and consultants may configure further exclusions, which must appear as coverage gaps.

### FR-ING-20: General source metadata

The product must collect permitted metadata, definitions, configuration, and relationships for source systems, entitlements, roles, access profiles or packages, transforms, rules, scripts, provisioning policies, forms, schemas, workflow definitions, certification or attestation configuration, tenant-wide settings, and custom attributes. All relationships across the collected inventory must be mapped. Unreachable dependencies must remain unresolved external references.

### FR-ING-21: Certification and attestation history boundary

Collection must include templates or policies, scheduled items, active runs, and configuration. Completed history, aggregate historical outcomes, and reviewer decisions are out of scope.

### FR-ING-22: Operational evidence

Initial operational evidence includes audit logs, provisioning events and failures, workflow execution history, certification or access-review events, connector or source errors, queue and task failures, and API or system errors. Authentication events are included when security-focused analysis is enabled. The default lookback is one year and the default collective cap is 100,000 records per collection; both are configurable.

### FR-ING-23: Raw and normalized evidence

After field-level exclusions, full permitted payloads may be collected. Central retention of raw evidence is customer-configurable. The product must retain normalized evidence and source-specific fields that the normalized model cannot represent. Raw and normalized evidence may be deleted independently, and surviving findings must disclose when source evidence is unavailable.

### FR-ING-24: Temporary raw processing

When permanent central raw retention is disabled, permitted raw evidence may be transmitted for hosted normalization and retained for up to 30 days before deletion. The policy and temporary central storage must be disclosed to the customer.

### FR-ING-25: Redaction and gap markers

Source credentials, passwords, private keys, connection strings, and encrypted secret values must not become evidence. Redacted, prohibited, customer-retained, permission-denied, unsupported, malformed, and inaccessible fields must use explicit reason markers rather than appearing absent.

### FR-ING-26: Customer-side collector

The collector must support unattended schedules, outbound-only delivery, encrypted offline health-assessment packages, offline encrypted queuing, customer-configured field and category controls, and operational status reporting without payload exposure. Only customer administrators may install, enroll, configure, set upgrade policy for, or remove it. Upgrade policy may be automatic or manually approved. The collector retains only its latest extraction until the next successful extraction, subject to customer-configured local limits.

### FR-ING-27: One Identity deployment and access

One Identity Manager collection must support customer-managed on-premises, private-cloud, and hosted deployments. Private or on-premises deployments default to the customer-side collector; hosted access is optional when the customer exposes an approved endpoint. Direct database access requires a dedicated read-only account and an approved query set, and queries must avoid locks or material production impact even when that reduces depth.

### FR-ING-28: One Identity discovery

Before evidence collection, the product must discover product version, patch or hotfix level, installed modules, enabled features, database staging level where available, schema extensions, custom tables and columns, and applicable acquisition capabilities.

### FR-ING-29: One Identity native coverage

When installed and in scope, collection must cover identities and minimum references; target systems; account definitions and manage levels; accounts, groups, and system entitlements; organizations and business roles; system roles; application roles and permission groups; IT Shop configuration; requests and approvals; attestation; compliance and separation-of-duties controls; synchronization projects; processes and scripts; configuration parameters and schema; Job Queue, DBQueue, synchronization, audit, and error evidence; and report configuration.

### FR-ING-30: One Identity native fidelity

Business roles, system roles, application roles, organizations, and permission groups must remain distinct native object types beneath the common Role category. IT Shop products and assignment resources may be evaluated as access-package equivalents without losing their native request and inheritance behavior. Synchronization projects and processes must retain their native scopes, mappings, direction, schedules, revision behavior, triggers, steps, retries, scripts, and dependencies.

### FR-ING-31: One Identity customization evidence

Designer system-configuration or customization reports and directly collected schema/configuration metadata must complement each other. Evidence must distinguish vendor-default, customer-modified default, customer-created, and unknown artifacts. For overwritten defaults, both the matching vendor value and actual value must remain visible.

### FR-ING-32: One Identity customization depth

Collection must include permitted transport or change metadata, change labels, compilation state, consistency results, schema and dependency metadata, custom scripts, templates, processes, customizers, configuration parameters, and custom table/column definitions. Unknown custom tables are schema-first; their row values remain excluded unless the customer explicitly permits selected fields.

### FR-ING-33: One Identity identifiers and baselines

Every normalized One Identity object must preserve native table, column, UID where available, module, version, extraction method, and source-extraction identifiers. UID is the initial correlation key for database objects that provide it. Mappings and health checks must be versioned so later connector changes cannot reinterpret older baselines.

### FR-ING-34: One Identity reports and customizations

Report definitions and metadata are collected; generated contents are out of scope unless explicitly selected. Useful read-only reports may run during collection. Failed compilation, inconsistent customization state, unapplied changes, unsupported patches, and broken references must become health-assessment candidates. If the exact vendor baseline is unavailable, comparisons must be labeled incomplete.

### FR-ING-35: Capability and coverage visibility

Before configuration, the product must show a version- and method-specific capability matrix. Every supported, installed, in-scope object must appear in health assessment as assessed or with an explicit gap state. Uninstalled One Identity modules must be marked not applicable rather than as gaps.

### FR-ING-36: Migration sequencing boundary

Health assessment supports SailPoint and One Identity Manager. Migration delivery begins with SailPoint to Veza. SailPoint to One Identity Manager and One Identity Manager to Veza follow; One Identity Manager to SailPoint remains planned. Each later path requires its own product specification or approved destination-mapping extension before implementation.

## Permissions

| Actor | Allowed action | Constraints |
|---|---|---|
| Customer administrator | Configure connections, credentials, scope, schedules, evidence policy, uploads, baselines, and collectors | Restricted to its customer organization and authorized projects |
| Assigned partner administrator | Configure connections, credentials, scope, schedules, uploads, and baselines | Restricted to assigned customers; cannot install or administer a customer-side collector |
| Assigned consultant | Configure connections, credentials, scope, schedules, uploads, and baselines | Restricted to assigned projects; cannot install or administer a customer-side collector |
| Platform operator | Configure or troubleshoot authorized connections and runs | Must preserve customer isolation and avoid unnecessary payload access; cannot install or administer the customer's collector |
| Customer collector administrator | Install, enroll, configure, upgrade, and remove a collector | Must be a customer-authorized administrator |

All listed engagement roles may pause or revoke a connection, rerun failed categories, cancel a collection, and activate a partial or reconciled baseline within their authorized scope.

## Validation and failure behavior

- Unsupported source products, versions, authentication mechanisms, and acquisition methods must fail visibly against the capability matrix.
- Unsupported or unverified versions must be blocked from migration but may use reduced-trust uploads for health assessment.
- Authentication expiry pauses the run at its last checkpoint until reconnection.
- API throttling triggers adaptive backoff and preserves completed work.
- Source changes during collection do not invalidate the baseline; subsequent delta or reconciliation collection updates it.
- A malformed upload follows the selected strict or partial-import policy.
- A missing manifest produces a reduced-trust warning but does not block ingestion.
- A scheduled run that overlaps another run is skipped. A missed run does not catch up.
- Failed endpoints or object categories remain explicit gaps; they must not be represented as complete.
- Duplicate pages, chunks, files, and records must not create duplicate active evidence.
- Unreachable referenced dependencies remain explicit unresolved references.
- Overwritten vendor defaults must show both default and actual values when the matching baseline is available.

## Edge cases

- A source object appears through API, database, report, and export with conflicting values.
- A One Identity UID is missing, duplicated, or changed between extraction methods.
- A module is installed but only partially accessible to the configured credentials.
- A customer changes exclusions during an incremental collection.
- Temporary raw evidence expires while normalized findings remain active.
- An offline collector never completes a subsequent successful extraction and reaches its configured local retention or storage limit.
- An archive contains multiple schemas, nested archives, unsafe paths, executables, or relationship keys that do not resolve.
- A customer-custom authentication module is present but absent from the supported capability matrix.
- A version 8.x through 10.x environment includes a patch for which the exact vendor customization baseline is unavailable.

## User-facing errors

- Connection unavailable, authentication expired, or credentials revoked.
- Source permissions insufficient or broader than documented minimum.
- Unsupported or unverified source version, module, authentication method, or acquisition path.
- File exceeds the per-file or aggregate 100 MB limit.
- Encrypted, password-protected, malicious, unsafe, or uninspectable upload rejected.
- Mapping unresolved after schema change.
- Required relationship key missing.
- Partial baseline activated with inaccessible, excluded, malformed, unsupported, or conflicting evidence.
- Retry window exhausted and run paused for human action.
- Collector offline, storage-limited, outdated, or configuration-drifted.

Errors must identify the project, environment, endpoint, collection, evidence category, and correlation context without exposing credentials or protected payloads.

## Analytics and audit requirements

- Audit credential creation, replacement, revocation, and deletion without recording secret values.
- Audit connection tests, endpoint and permission results, source/version selection, scope and policy changes, schedules, collection start/cancel/retry, uploads, collector enrollment and upgrades, baseline activation, evidence export, and evidence deletion.
- Measure run duration, records and objects processed, endpoint coverage, retry and throttling behavior, partial outcomes, rejected records, conflict counts, and gap counts.
- Preserve value-level provenance, extraction time, endpoint, method, source-native identifier, and baseline version.
- Distinguish collector health and operational telemetry from customer evidence payloads.

## Accessibility requirements

- Collection states, warnings, gaps, and conflicts must not rely on color alone.
- Preview, capability, coverage, baseline-difference, and relationship views must have keyboard-accessible and non-graphical representations.
- Error messages and partial-baseline warnings must identify actionable next steps and affected evidence categories.

## Security and privacy considerations

- All source interaction is read-only and uses environment-specific connections.
- Source secrets are retained only in the approved credential store and never enter evidence, reports, ordinary logs, or generated artifacts.
- Customer-configured exclusions and redaction rules apply to hosted, collector, database, report, and upload acquisition paths.
- Government identifiers remain customer-side or are redacted before leaving the approved boundary.
- Uploads require structural and malware safety controls before parsing.
- Database access uses a dedicated read-only account, approved queries, and production-safe execution.
- Customer-side queues and retained extractions are encrypted and bounded by configured storage and retention.
- Temporary hosted raw retention of up to 30 days must be presented clearly even when permanent raw retention is disabled.
- The decision to warn and proceed with overprivileged credentials conflicts with the approved least-privilege requirement and blocks approval until reconciled.

## Acceptance criteria

### AC-ING-1: Single logical source

Given a new project

When an authorized user configures its source

Then the project accepts exactly one logical SailPoint or One Identity Manager source and may attach multiple endpoints only to that source.

### AC-ING-2: Read-only enforcement

Given any collection, retry, reconciliation, analysis, migration, or validation operation

When it interacts with the configured source

Then no source create, update, or delete operation is invoked.

### AC-ING-3: Preview and capability matrix

Given a selected source version and acquisition method

When the user reviews the collection before starting it

Then the product shows supported capabilities, scope, relationships, permissions, exclusions, retention, estimated volume, and known gaps.

### AC-ING-4: Resumable throttled collection

Given a collection that is throttled or loses authentication

When rate capacity or authentication becomes available again within the retry policy

Then collection resumes from the most specific valid checkpoint without duplicating completed evidence.

### AC-ING-5: Immutable incremental baseline

Given an existing baseline

When an incremental or targeted collection completes

Then a new immutable baseline is created, changed evidence is updated, missing objects are tombstoned, and untouched evidence retains its original collection time.

### AC-ING-6: Partial baseline

Given that at least one endpoint or evidence category fails

When other permitted evidence completes

Then the baseline may activate automatically with prominent warnings and an explicit gap state for every incomplete category.

### AC-ING-7: Safe upload

Given an uploaded collection package

When the file is encrypted, password-protected, malicious, unsafe, uninspectable, or exceeds 100 MB per file or collection

Then the product rejects it with an actionable error and does not parse it into evidence.

### AC-ING-8: Adjustable import mapping

Given a generic CSV or JSON upload

When the product proposes a mapping

Then an authorized user can adjust and save it, and later schema changes are remapped with warnings while unresolved fields require review.

### AC-ING-9: Raw-evidence policy

Given a customer that disables permanent central raw retention

When hosted normalization processes permitted raw evidence

Then the product discloses temporary storage, deletes raw evidence no later than 30 days, and preserves normalized provenance and deletion state.

### AC-ING-10: Minimal identity evidence

Given identities and accounts in the source

When collection runs under the default scope

Then it collects only approved matching, ownership, approval, assignment, membership, and relationship references and excludes general profiles and values.

### AC-ING-11: Multi-endpoint provenance

Given the same One Identity object from database, API, report, and export evidence

When the baseline is assembled

Then one correlated object retains value-level provenance for every method and exposes conflicting values without silently resolving them.

### AC-ING-12: One Identity customization comparison

Given an available matching vendor baseline

When an out-of-the-box value has been overwritten

Then the assessment shows the vendor-default and actual values and classifies the artifact as customer-modified.

### AC-ING-13: One Identity module state

Given a One Identity module that is not installed

When coverage is calculated

Then the capability is marked not applicable rather than incomplete.

### AC-ING-14: Explicit assessment coverage

Given a completed health-assessment collection

When its supported in-scope inventory is reviewed

Then every object is assessed or has an explicit inaccessible, incomplete, excluded, redacted, unsupported, or other gap state.

### AC-ING-15: Unsupported version boundary

Given an unverified source version or One Identity version 11 or later

When a user attempts migration

Then migration is blocked, while reduced-trust upload-based health assessment may remain available with warnings.

## Dependencies

- Approved customer/partner authorization and tenancy model.
- Approved credential-store and secret-management design.
- Version- and deployment-specific SailPoint and One Identity capability matrices.
- Vendor interface, licensing, rate-limit, authentication, and export validation.
- One Identity vendor-default configuration baselines for supported versions, modules, patches, and hotfixes.
- Customer-side collector product and security design.
- Raw/normalized evidence model, provenance model, retention enforcement, and deletion design.
- Upload isolation, malware scanning, and archive-validation capability.
- Separate destination-mapping specifications for migration paths after SailPoint to Veza.

## Open questions and assumptions

- **Blocking conflict:** approved `NFR-SEC-1` requires minimum privileges, while discovery currently allows warned overprivileged credentials to proceed. Product and security owners must reconcile this before approval.
- One Identity versions 8.x through 10.x are intended to be supported, but the exact tested combinations of releases, patches, modules, databases, and authentication mechanisms must be enumerated in the capability matrix.
- The authentication mechanism described as “SQL” is assumed to mean supported database authentication for the dedicated read-only account.
- Temporary hosted raw evidence is retained for up to 30 days even when permanent central raw retention is disabled; customer-facing policy and consent language require approval.
- Customer-side collector platform packaging, operating-system support, resource limits, and upgrade transport belong to technical design.
- Exact source endpoint contracts and field dictionaries belong to technical specifications but must conform to the metadata and data-minimization boundaries in this specification.
- Relative delivery order between SailPoint to One Identity Manager and One Identity Manager to Veza remains unspecified; both follow SailPoint to Veza. One Identity Manager to SailPoint follows later.

## Approval

Approved by:

Date:

Only the product owner may change `Status` to `Approved`.
