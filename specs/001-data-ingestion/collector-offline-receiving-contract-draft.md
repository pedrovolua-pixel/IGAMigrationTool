# Draft: Collector offline package and receiving contract

Status: **DRAFT — UNAPPROVED — DISABLED**

Contract candidate: `offline-receiving/1`, revision `C2-1`. Owner: Technical owner. Last updated: 2026-10-01.

Required reviewers: Technical, security, operations and quality owners; customer evidence-policy authorizer for the applicable customer.

## Authority and intended review

This is the C2 review artifact in [parallel cycle 02](../../plans/active/local-pilot-parallel-cycle-02.md), tracing ING-PILOT-003/004, FR-ING-23–27, C0/C3 and the offline contract-negative subset of [TP-ING](test-plan.md). The repository owner's delivery/installer **direction** approval permits preparing this candidate. It does not approve these exact schemas, security decisions, trust bootstrap, resource bounds or installation procedure. No package writer, import operation, encryption adapter, signer, installer or release is enabled by this document. Every statement in the candidate sections is a proposal requiring the decisions below.

The [approved product](product-spec.md), [technical specification](technical-spec.md), [implementation plan](implementation-plan.md), [database evidence contract](../003-health-assessment/database-evidence-contract.md), [delivery proposal](collector-delivery-and-installer-contract-proposal.md), accepted ADRs and approved security/operations policy remain authoritative. This document does not modify them. Approval of this draft would still require updating the governing contracts and implementation/test plans before dependent behavior is implemented or enabled. Consequential changes or conflicts require the existing human specification/ADR process; packet review is engineering review only.

## Fixed requirements already approved

| Boundary | Governing record | Consequence for this candidate |
|---|---|---|
| Customer-operated signed self-contained .NET 10 `win-x64` Windows Service and one-shot CLI; Server 2022/2025 including Core; manual signed MSI updates | [ADR-0004](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md), collector technical specification | No inbound listener, remote self-update, Azure collector identity or platform-held SQL credential; no production installer/toolchain is selected here. |
| Random per-package AES-256-GCM key wrapped with approved platform RSA public key using RSA-OAEP-SHA256; authenticated signature binding header, manifest and ciphertext digest | Technical specification and delivery proposal | Signature identity, trust source and exact signature/serialization profile remain unapproved. Local encryption primitives do not establish package trust. |
| Server-side role/action/category/state authorization and trusted assignment determine one customer data plane | [ADR-0002](../../architecture/decisions/ADR-0002-pilot-tenant-isolation.md), [authorization policy](../../docs/security/health-assessment-authorization-matrix.md), feature-001 permissions | Scope claims, signatures, digests, filenames or package possession cannot grant import/activation authority or choose a data-plane locator. Collector installation/admin authority remains customer-only. |
| Permitted minimized evidence only; secret/government-identifier exclusions and explicit reason markers | FR-ING-19, 23–25 and database evidence contract | Validate signed policy and exact field dictionary; no general identity profiles, topology or protected value in metadata, ordinary logs or forensic summaries. |
| Ordered page/chunk integrity, native provenance, permission result, version/build/module compatibility, coverage/gaps/conflicts | Database evidence contract | No silent duplicate-native-identity merging; blocked permission or integrity failure is ineligible. Excess read-only warnings remain visible under the approved narrow exception. |
| Immutable customer-scoped baseline; independent raw/normalized lifecycle; digest is never authority | [ADR-0003](../../architecture/decisions/ADR-0003-immutable-evidence-and-publication-storage.md), FR-ING-23/24 | No mutation of prior pages/manifests; temporary hosted raw processing must be disclosed and end no later than 30 days when permanent raw retention is disabled. |
| Encrypted bounded local queue, latest extraction until next success subject to configured expiry/capacity | Collector technical specification and [local service contract](collector-local-service-contract-proposal.md) | This envelope's import expiry does not reset collection/local retention or authorize deletion. Cross-run queue/cleanup remains an operations decision. |
| Payload-free audit, 12-month audit lifecycle, approved deletion-safe backup/restore | [audit policy](../../docs/security/health-assessment-audit-policy.md), [operations plan](../../docs/operations/health-assessment-pilot-operations-plan.md) | Do not borrow audit retention for package payloads or replay metadata without review. Restore must replay revocation/deletion decisions before accepting work. |

The signed collector envelope is a dedicated acquisition method. It is not feature-001's later generic CSV/JSON/archive upload path or its reduced-trust missing-manifest behavior. Generic-upload AC-ING-7/8 rejects encrypted uninspectable uploads; the approved pilot slice separately requires authenticated decryption and inspection for its offline envelope. No automatic fallback between these methods is proposed.

## Candidate decisions for explicit acceptance or replacement

| ID | Exact candidate | Trade-off / outstanding authority |
|---|---|---|
| OFF-D01 | Fixed binary envelope, restricted canonical JSON header/manifest, no archive/compression; one immutable collection per package | Easier bounded inspection and byte identity; split/related packages are unsupported in v1. Technical/quality owners must prove capacity and schema interoperability. |
| OFF-D02 | Separate offline package-signing key; RSA-PSS with SHA-256, MGF1-SHA256, salt length 32 bytes; RSA modulus 3072 bits and exponent 65537 | Candidate only. [ADR-0005](../../architecture/decisions/ADR-0005-gate-evidence-signing.md) approves a gate-bundle profile, not this collector signer. Security owner must approve this profile and actual key governance; no gate, reviewer, MSI or recipient key reuse. |
| OFF-D03 | Manual public-key proof/registration for a host unable to enroll online; protected server registry binds signer to collector and one assignment | Avoids self-signed trust-on-first-use; introduces a customer/security handoff. Exact identities, custody, proof delivery and revocation runbook remain required. |
| OFF-D04 | Platform RSA recipient profile 3072 bits/exponent 65537, OAEP SHA-256/MGF1-SHA256 with empty label; AES nonce 12 bytes and tag 16 bytes | Algorithms AES-256-GCM/RSA-OAEP-SHA256 are fixed; these parameter and key-lifecycle details are candidates. Recipient and signing keys have separate purposes. |
| OFF-D05 | 100,000,000-byte total envelope; 16,384-byte header; 1,048,576-byte manifest; at most 4,096 pages, each at most 1,048,576 bytes | Conservative decimal 100 MB candidate consistent with the product upload bound; no claim it fits every 100,000-record category. Quality/operations need size/memory/duration evidence. Overflow must leave a partial/gap outcome, never silently split/truncate. |
| OFF-D06 | Import lifetime at most 24 hours from package creation, also bounded by original run expiry and signer/recipient import windows; future creation tolerance 300 seconds; zero expiry grace | Values are unapproved, separate from the existing 1–720-hour local setting and central lifecycle. Operations/security/customer policy review may replace them; package retries never extend clocks. |
| OFF-D07 | Durable replay/reservation rows in the resolved customer PostgreSQL data plane, unique package and collection identities, transaction/outbox/reconciliation | Fits ADR-0004's canonical PostgreSQL work state. Requires schema/index/lifecycle and crash/restore tests; an in-memory cache or queue duplicate detection cannot satisfy this decision. |
| OFF-D08 | Authentication/authorization before receiving protected bytes, then bounded signature verification before key unwrap/decrypt; isolated data-only inspection before assembly; revalidate authority/trust at activation | Separates transport acceptance from eligibility. Concrete hosted operation/authentication and sandbox policy still require approval; no route is selected here. |

## Candidate byte and type rules

These rules define a reviewable v1 byte contract, not an implemented format. All integers in framing are unsigned big-endian. Checked arithmetic verifies total lengths before allocation. Frames must consume the entire input exactly; trailing data, truncation, extra sections and zero-length required sections fail closed.

Restricted JSON profile `iga-offline-json/1`: UTF-8 without BOM; ASCII property names and metadata string values; object keys sorted by ordinal ASCII bytes; no whitespace; no duplicate/unknown keys at any object level. Strings use double quotes and escape only `"` and `\\`; control characters and other escapes are disallowed. Numbers are unsigned base-10 integers with no leading zero except `0`, no fraction/exponent, range `0..9007199254740991`; booleans are `true`/`false`; null is disallowed. Receivers parse strictly and reserialize using this profile, then require exact byte equality. Hashes are lowercase 64-character SHA-256 hex; IDs are nonzero lowercase UUID D strings. Times are UTC `YYYY-MM-DDTHH:mm:ssZ`, with valid calendar values and no leap-second/fraction/offset aliases. Candidate metadata codes use `[A-Za-z0-9._-]`, length 1–128. Payload values are not restricted to ASCII: their separately approved versioned page schema determines their serialization and inspection, and their exact bytes are hashed without normalization.

Version tuples have exactly `id` (UUID), `version` (positive integer), `sha256` (hash). Schema references have exactly `id` (metadata code), `version` (positive integer), `sha256` (hash of the approved schema artifact). Unknown versions/algorithms or absent trusted schema artifacts reject; a submitted schema or digest does not promote itself. Future changes create a distinct version and reviewed compatibility matrix; readers never coerce a future shape into v1.

Binary envelope sequence:

| Section in order | Encoding / candidate bound |
|---|---|
| Magic and format | Eight ASCII bytes `IGAOFL01` |
| Header | `u32(headerLength)` then canonical header bytes |
| Wrapped AES key | `u16(wrappedKeyLength)` then RSA wrapped-key bytes; exactly 384 bytes for OFF-D04 |
| AES nonce | Exactly 12 random bytes |
| Ciphertext | `u64(ciphertextLength)` then ciphertext bytes; length must equal header `plaintextLength` |
| AES tag | Exactly 16 bytes |
| Signature | `u16(signatureLength)` then detached signature bytes; exactly 384 bytes for OFF-D02 |

`ciphertextDigest = SHA256(ciphertextBytes)`; it is calculated by the receiver and not trusted from a caller. Encryption associated data is exactly `ASCII("IGA-OFFLINE-AAD-1\n") || magic || u32(headerLength) || headerBytes || u16(wrappedKeyLength) || wrappedKeyBytes || nonce`. Signature message is exactly `ASCII("IGA-OFFLINE-SIGNATURE-1\n") || magic || u32(headerLength) || headerBytes || u16(wrappedKeyLength) || wrappedKeyBytes || nonce || u64(ciphertextLength) || ciphertextDigestBytes || tag`, where digest bytes are the 32 raw bytes rather than hex text. RSA-PSS signs this message using SHA-256 as its hashing algorithm, with no caller prehash/double-hash variant. The signature binds header metadata and its plaintext manifest/collection-manifest/content digests, encrypted bytes, key wrap, nonce, tag and lengths. No public key/certificate/chain in the envelope is a trust source.

The producer chooses a fresh random 32-byte AES key and fresh random 12-byte nonce for each newly created package; it never encrypts changed content under a reused package key/nonce. A same-package transfer retry copies identical envelope bytes. Repackaging after any metadata/content change creates a new package ID, key and nonce; the collection-identity conflict rule below still applies. Signature/AAD domain strings and integer widths are literal and cannot be substituted by JSON representations.

## Candidate header schema

Root properties are exactly those below. The header contains only metadata safe for the approved transfer boundary. The encrypted manifest holds build/module/provenance/coverage detail. Server-resolved expected values must match repeated fields exactly.

| Property | Type / constraint |
|---|---|
| `headerSchemaVersion` | Integer `1` |
| `packageId`, `collectionId`, `collectorId` | UUIDs; package unique per envelope; collection identifies one immutable extraction |
| `customerId`, `projectId`, `environmentId`, `scopeId` | Opaque UUID assignment claims only; no locator/name/address |
| `scopeVersion` | Positive integer; match current authorized assignment version |
| `collectorRelease` | Version tuple referencing approved release artifact |
| `queryPack`, `fieldPolicy` | Version tuples; match reviewed locked material |
| `manifestSchema`, `pageSchema` | Schema references |
| `createdAt`, `expiresAt` | Times; candidate lifetime and clock rules below |
| `recipientKeyId`, `signerKeyId` | Opaque UUID lookup references; never embedded keys |
| `encryptionAlgorithm` | Literal `AES-256-GCM` |
| `keyWrapAlgorithm` | Literal `RSA-OAEP-SHA256` |
| `signatureAlgorithm` | Literal `RSA-PSS-SHA256-SALT32` |
| `manifestSha256`, `collectionManifestSha256`, `contentSha256` | Exact manifest, stable collection-manifest and ordered page-content hashes defined below |
| `plaintextLength`, `pageCount` | Integer; exact encrypted-body size; page count 0–4,096 |

No source credentials, raw values, native UIDs, topology, filenames, URLs, storage/container/database locators, free-text reason or caller-supplied endpoint appear in this header. Artifact references are claims to compare with trusted registrations, not downloadable URLs. An empty page list is representable for explicit unsupported/excluded/empty/gap outcomes, but cannot imply complete eligible inventory.

## Candidate encrypted manifest and page framing

Decrypted body is `u32(manifestLength) || manifestBytes || pageFrames`. Each page frame is `u32(pageLength) || pageBytes`; frame order equals the ordered manifest `pages` array. No directory/path/name or executable section exists. `manifestSha256` hashes the exact canonical manifest bytes. `collectionManifestSha256` hashes the canonical manifest object after removing only its root `packageId` property; all collection/source/policy/provenance/coverage/gap/conflict metadata remains bound. A replacement envelope may change package ID/recipient/key material while the immutable collection manifest remains otherwise identical. `contentSha256` hashes `ASCII("IGA-OFFLINE-CONTENT-1\n") || u32(pageCount) || pageFrames`, including each page's length and bytes. The envelope has no compression, nested archive, archive extraction or concatenated package support. Malware/prohibited-content inspection remains necessary for inert permitted code/script evidence; a signature is not an inspection exemption.

Manifest root properties are exactly:

| Property | Type / required contents |
|---|---|
| `manifestSchemaVersion` | Integer `1` |
| `packageId`, `collectionId`, `collectorId`, `customerId`, `projectId`, `environmentId`, `scopeId`, `scopeVersion` | Exact header matches |
| `collectorRelease`, `queryPack`, `fieldPolicy`, `pageSchema` | Exact header tuple/reference matches |
| `source` | Object: `product` literal `OneIdentityManager`; `exactBuildEvidenceId`, `compatibilityEvidenceId`, `endpointEvidenceId` UUIDs; `modules` array of objects with `moduleEvidenceId` UUID and `versionEvidenceId` UUID |
| `acquisitionMethod` | Literal `customer-collector-offline` |
| `collectionStartedAt`, `collectionEndedAt`, `runExpiresAt` | Times; original authenticated run-start/expiry claims, not reset by packaging |
| `permission` | Object: `principalEvidenceId` UUID; `queryPackVersion` positive integer; `minimumReadSetId`, `methodVersion` codes; `excessReadOnlyDetected`, `blockingCapabilityDetected` booleans; `excessReadOnlyCategories`, `blockingCategories` arrays of codes; `decision` literal `eligible`, `eligible_with_warning` or `blocked`; `testedAt` time; `correlationId` UUID |
| `evidencePolicy` | Object: `policyEvidenceId` UUID; `rawRetentionClassId`, `normalizedRetentionClassId` codes; `permanentRawRetention` boolean; `temporaryProcessingDisclosureId` UUID |
| `coverage` | Array of coverage entries described below |
| `pages` | Array of page entries described below, ordered by `ordinal` |
| `gaps`, `conflicts` | Arrays of marker entries described below |
| `normalizedObjectCount`, `relationshipCount`, `unresolvedReferenceCount` | Nonnegative integers; validated against pages using the approved schema, not accepted on claim alone |
| `contentSha256` | Exact header match |

`source.modules` sorts by `moduleEvidenceId`; duplicates reject. Exact build/hotfix/module data is resolved through immutable protected evidence references in the permitted evidence payloads and trusted compatibility records. A reference alone cannot establish exact-build eligibility. No native build wildcard or raw permission listing is introduced by this envelope. This candidate reference layout requires technical/SME review against the full baseline manifest; assembly must retain the evidence contract's exact source semantics and native provenance.

A page entry has exactly `ordinal` (zero-based contiguous integer), `pageId` (UUID), `query` (version tuple), `boundaryEvidenceId` (UUID reference to schema-governed protected ordering/boundary evidence), `categoryId`, `objectTypeId` (codes), `extractedAt` (time), `byteLength`, `rowCount` (integers), `terminalPage` (boolean) and `sha256` (page byte digest). Duplicate page IDs or query/boundary identity reject. No reordering, missing ordinal, reused ordinal, changed digest, unresolved query/boundary reference or false terminal marker is silently repaired. Boundary refs must resolve within the same locked collection. The current local `IGS2` serialization is not automatically this transport page schema; transport schema/dictionary and conversion vectors remain required decisions.

A coverage entry has exactly `categoryId`, `objectTypeId` (codes), `sourceTimestamp`, `evidenceTimestamp` (times), `requested`, `completed`, `partial`, `inaccessible`, `excluded`, `redacted`, `unsupported`, `malformed`, `error` (nonnegative integers), and `limitationsEvidenceId` (UUID). Entries sort by category/type and duplicates reject. These are approved-contract category/object counts, with explicit schema-defined units. Counts alone do not prove completeness or applicability. Redaction can coexist with an otherwise completed object; totals must not be summed as a partition or used to calculate a perfect-coverage percentage. Exact unit/overlap reconciliation is pending approved category dictionary/quality vectors. Every failed/truncated category has markers; uninstalled modules retain not-applicable intent in the approved baseline/applicability metadata, rather than being fabricated as inaccessible.

A marker entry has exactly `markerId`, `provenanceEvidenceId` (UUIDs), `categoryId`, `objectTypeId`, `reasonCode` (codes), `affectedCount` (nonnegative integer) and `explanationCode` (code resolved by an approved server-side explanation catalog). Arrays sort by `markerId`; duplicates reject. Native affected-object references and value-level conflict details belong in schema-governed encrypted pages, preserving them without exposing values in this metadata. Approved reason classes include redacted, prohibited, customer-retained, permission-denied, unsupported, malformed, inaccessible and unresolved-reference markers. No unsigned free-text explanation or raw conflict value is added to ordinary audit/status. Accepted explanation/marker/category dictionaries and page schema are still missing, so this manifest cannot yet drive import.

## Candidate signer, recipient and bootstrap decisions

The package signer is a distinct collector key, not its MSI Authenticode publisher, SQL principal, human import identity, recipient encryption key or engineering gate signer. This draft selects no real signer, certificate authority, key vault, publisher or trust administrator. Candidate registry fields are key ID, public-key digest/parameters, collector ID, permitted assignment/scope version, package-signing purpose, validity start/end, active/revoked status, security revision and protected approval evidence reference. The verifier reads current authoritative registry state for every attempt and before activation. Package metadata cannot register a key or change a registry entry.

For a collector unable to enroll online, the candidate bootstrap is:

1. Customer collector administrator establishes a protected host/service identity and generates a non-exportable package-signing key in approved Windows protected key storage. Offline export contains the public key and an opaque request ID only; no SQL credential, private key, host topology or evidence payload.
2. Through a separately authenticated authorized application session, that administrator submits the public-key request for the server-selected assignment. The server creates a one-use 32-random-byte proof challenge with a 15-minute candidate lifetime, bound to request ID, collector ID, assignment/scope version and public-key digest. Challenge transport to the offline host is customer-controlled; these numbers/operation shapes require security/operations review.
3. The host signs the exact challenge record under a separate `IGA-OFFLINE-REGISTER-1` domain. The proof response returns through the authenticated admin workflow. The server rejects expired/reused challenge, altered binding, unapproved key/profile or missing possession proof. Possession proves the key only; it does not prove customer role, safe host custody or source authority.
4. Security/operations verify the protected host custody record and administrator's current assignment. A recorded authorized security decision activates the scoped registry entry. Automatic first-package trust and self-registration are denied. The exact authority mapping and challenge schema/vectors must be approved; this is not the existing online device-certificate enrollment contract.
5. Loss/compromise/scope change revokes the old registry entry before a replacement registration. Candidate revocation denies all pending/new imports and rechecks already accepted work before activation; it does not retroactively erase completed baselines. Valid rotation creates a new key ID; there is no indefinite dual-key acceptance. Incident/recovery decisions remain attributed under approved policy.

Recipient public keys arrive through a protected customer-admin approved distribution record whose fingerprint is verified independently of the package/transfer channel. Candidate recipient registry contains key ID, fingerprint, algorithm/parameters, customer assignment binding, packaging window, decryption/import window, active/revoked state and protected recovery reference. Private unwrap capability remains solely inside the platform's approved key boundary. Signer keys cannot unwrap, and collector keys cannot resolve storage.

Normal recipient rotation may retain an old private key for the recorded import window, bounded by package expiry and customer policy. Revoked keys deny new/pending imports; retaining them for an authorized forensic recovery is a separate security decision and grants no normal activation. Unknown key IDs never trigger downloads, arbitrary network fetches or key search across customers. Recipient compromise, signer compromise, certificate expiry, key outage and offline administrator recovery all require named procedures and executed vectors before enablement.

## Candidate clock, bounds and lifecycle

Candidate clock validation uses trusted UTC on the receiver. Let `t` be validation/activation time and `s` the candidate 300-second future tolerance: require `createdAt <= t + s`, `collectionStartedAt <= collectionEndedAt <= createdAt`, `createdAt < expiresAt`, `expiresAt <= createdAt + 24h`, and `expiresAt <= runExpiresAt`. Import requires `t < expiresAt`; equality is expired, with no tolerance after expiry. Signer validity includes creation and current import/activation time; recipient packaging/import windows must include the corresponding instants. An unverifiable clock, backward receiver-clock jump or untrusted run-start provenance yields a blocked clock/eligibility result, not an extended lifetime. Authoritative run/policy/registry evidence must validate the timestamps; a signed claim alone cannot recreate the local run-start proof.

Expiry is rechecked before unwrap/decrypt and activation, including after queue delays/retries. A package starting inspection before expiry cannot activate after expiry. An already activated baseline follows its independent lifecycle; package expiry does not invalidate it retroactively. Candidate lifetime is never used as the raw-retention clock: configured lifecycle events, customer disclosure, independent raw/normalized deletion and the 30-day temporary-raw maximum still apply.

Enforce the envelope total ceiling before payload buffering; validate header and per-section bounds before allocation; plaintext/ciphertext length must agree; sum of page frames plus manifest framing must exactly equal `plaintextLength`. Decrypt into customer-scoped protected encrypted-at-rest staging. Do not expose unauthenticated plaintext from GCM streaming before final tag verification; any necessary scratch remains protected and ineligible. Inspect under separately bounded CPU/memory/duration and per-customer queue/concurrency limits. Those operational maxima are not inferred from local CLI settings and must be approved with tested capacity before an importer can run. No parser may allocate from unchecked manifest row/count claims.

OFF-D05 deliberately leaves a measurable risk: a single-envelope v1 may not fit all approved scale categories. Measure synthetic maximum permitted field sizes at 100,000 records per applicable category. If they cannot fit, request a reviewed multi-package/streaming design and corresponding specification change; do not raise caps or silently reduce scope. Cancellation/expiry stops new processing, preserves immutable completed evidence and follows existing protected cleanup/incident policy. Temporary forensic payload retention is not invented here: default output is approved payload-free metadata, with any protected retention/hold governed by an explicit authorized policy record.

## Candidate receiving operation and state machine

Operation names below are logical module actions, not public routes. Proposed `BeginOfflineReceipt` takes the actor's authenticated context, selected project/environment resource IDs, request-id UUID and declared total byte length. Resolve customer assignment from authoritative server state before customer-data access; authorize import and data categories under feature-001 permissions and common policy. Enforce CSRF/recent-authentication where the approved identity policy requires it. No client-supplied data-plane locator or package scope is an input to routing. Return an opaque receipt ID, state/code and correlation ID only. Same request ID with identical selected scope/declaration returns the same receipt to a currently authorized actor; changed inputs conflict. This request key is not evidence deduplication or import authority.

Bytes remain in protected, ineligible receipt staging until all checks finish. An authorized status operation reports state, bounded counts, warning/gap codes and opaque correlation; it reveals no decrypted metadata, protected identifiers or existence in another scope. Cancellation is a scoped authorized action; it cannot erase completed baselines or replay history.

| State / transition | Candidate prerequisite and durable effect |
|---|---|
| `AUTHORIZED_RECEIPT` → `RECEIVING` | Current import authority; expected scope; bounded declared length; customer-scoped protected staging and receipt reservation. |
| `RECEIVING` → `STRUCTURE_VALIDATED` | Exact bounded framing, canonical header, known versions/algorithms; actual size matches declaration; claimed scope equals trusted expected assignment. No decryption yet. |
| `STRUCTURE_VALIDATED` → `SIGNATURE_VALIDATED` | Current scoped signer trust and time checks; verify exact signature message against received bytes; any mismatch denies. |
| `SIGNATURE_VALIDATED` → `RESERVED` | Transactionally reserve package ID/envelope digest and collection ID/content/collection-manifest digests under resolved customer/project/environment/scope; duplicate/conflict rules below. |
| `RESERVED` → `DECRYPTED_INELIGIBLE` | Revalidate authority/time/trust/recipient; unwrap approved key; verify GCM tag and header AAD; exact decrypted-body lengths. Nothing is evidence yet. |
| `DECRYPTED_INELIGIBLE` → `INSPECTED_INELIGIBLE` | Strict manifest/schema references; repeated bindings and all digests/count/provenance checks; malware/structure/prohibited-field inspection; signed field policy and exact pack/build/module/permission compatibility. Native duplicates/conflicts are preserved as evidence-contract conflicts, never silently merged. |
| `INSPECTED_INELIGIBLE` → `BASELINE_PREPARED` | Immutable versioned assembler creates candidate manifest/pages with explicit gap/conflict/warning records; no health adapter access yet. |
| `BASELINE_PREPARED` → `ACTIVATED` or `ACTIVATED_WITH_WARNINGS` | Separate current activation authorization/state/policy/eligibility check; atomic uniqueness/state commit plus outbox; exact baseline ID recorded with digest. Partial activation follows existing product authority and visible warnings; no new approval policy is inferred. |
| Any pre-activation state → `REJECTED`, `CANCELED` or `EXPIRED` | Typed payload-free reason and cleanup/lifecycle reference; no active evidence. Corrupt/prohibited unit is quarantined from activation, with only approved forensic metadata exposed. |
| Dependency outage → `WAITING_FOR_DEPENDENCY` | Preserve protected staging/reservation and prior state; bounded retry under approved operation budget; revalidate all mutable authority/time/trust on resume. No success result. |

Ineligibility is not a health finding or a coverage pass. No state alone proves G2 or a pilot-validated exact build. Activation remains blocked until immutable-baseline and health capability eligibility contracts are approved/verified for the applicable environment.

Candidate error codes: `AUTHORIZATION_DENIED`, `SCOPE_MISMATCH`, `SIZE_LIMIT`, `STRUCTURE_INVALID`, `VERSION_UNSUPPORTED`, `ALGORITHM_UNSUPPORTED`, `SIGNER_UNTRUSTED`, `SIGNATURE_INVALID`, `CLOCK_UNVERIFIED`, `PACKAGE_EXPIRED`, `REPLAY_CONFLICT`, `RECIPIENT_UNAVAILABLE`, `INTEGRITY_INVALID`, `INSPECTION_DENIED`, `POLICY_DENIED`, `BASELINE_INELIGIBLE`, `RECEIPT_CANCELED`, `DEPENDENCY_UNAVAILABLE`. External errors group details where needed to prevent key/resource-existence disclosure; finer cause is restricted approved audit metadata. No stack trace, crypto bytes, decoded field value, host name or raw rejected input enters ordinary errors.

## Candidate replay, concurrency and recovery

OFF-D07 proposes two durable uniqueness boundaries inside the trusted customer data plane: `(projectId, environmentId, scopeId, packageId)` for the exact envelope digest and `(projectId, environmentId, scopeId, collectionId)` for the collection content digest, stable collection-manifest digest and locked versions. `packageId` is never authoritative customer routing. Hash/dedup information is not exposed globally or across customers. Local page replay and receiving dedup use their respective versioned contracts; an authenticated transport retry cannot overwrite prior evidence.

| Condition | Candidate outcome |
|---|---|
| Same package ID and identical full-envelope SHA-256 | Return/resume the existing authorized receipt; no second baseline or staging copy. Recheck scope/state/trust/expiry before pending work resumes. |
| Same package ID, changed any byte/digest | `REPLAY_CONFLICT`; preserve prior record and deny replacement. |
| New package ID, same collection ID, same content/collection-manifest digests and identical locked versions | Map to the existing candidate/active baseline after full new-envelope validation; no duplicate activation. This permits valid re-encryption after an approved recipient rotation. |
| Same collection ID with changed content, substantive manifest metadata, scope-version or other locked inputs | `REPLAY_CONFLICT`; no override/overwrite. A new extraction uses a new collection ID and immutable baseline. |
| Concurrent imports or online/offline delivery of the same collection/pages | One committed baseline identity via transactional uniqueness and shared assembler idempotency; losing caller sees existing state/conflict. Online interoperability still needs its separately approved contract/vectors. |
| Crash after staging/decrypt/preparation but before activation | Recover the durable previous state and exact blob versions/digests; revalidate authority/trust/expiry; orphan blobs stay ineligible pending reconciliation. |
| Crash after activation commit but before response/outbox publication | Return recorded baseline; reconcile the outbox without creating/activating another baseline. |
| Lost database/replay store, unknown reservation or unavailable trust source | Block import/activation until approved recovery proves consistency; do not degrade to memory/cache acceptance. |

Reservations need an internal fenced lease/version so an expired worker cannot commit after another resumes; proposed lease duration is 5 minutes with 1-minute renewals, bounded by package expiry. A lease expiring does not delete replay history or release a collection for conflicting content. Exact storage schema, fenced-transaction implementation, renewal budget and reconciliation ownership need technical/operations approval and failure vectors.

Replay rows are not package payloads and not automatically audit records. Proposed expiry replay protection retains minimal package ID/digest/scope/outcome through `expiresAt` plus restore validation, while permanent baseline identity/collection mapping follows its approved independent lifecycle and deletion tombstones. **Exact replay retention is unresolved and enablement-blocking**: data-governance/operations/security must specify a lawful clock, purge deadline and backup/restore protection. In particular, the longest accepted package window and restore validation horizon must never outlive a deleted replay row; restoration cannot accept a deleted/previously activated collection again. No indefinite retention, invented 12-month replay policy or automatic deletion task is selected here.

## Contract-negative fixture matrix — planned, not executed

Every fixture uses synthetic keys/opaque IDs and permitted artificial pages. Expected invariants: no unauthorized plaintext exposure, no active baseline on denial, no cross-customer access, no duplicate activation, prior immutable evidence unchanged, payload-free logs/audit and explicit partial/gap outcomes. These are future contract fixtures under the approved test plan, not reported test passes.

| Fixture | Mutation / condition | Expected checkpoint / outcome |
|---|---|---|
| OFF-N01 | Missing authentication, wrong role/category/customer/project/environment; direct locator substitution | Deny before protected receipt/data-plane access; no key/resource-existence disclosure. |
| OFF-N02 | Assignment/resource revoked, suspended, deleted or policy changed during queueing/inspection | Deny on current check, including activation; no stale authorization reuse. |
| OFF-N03 | Missing/unknown/duplicate JSON property; wrong casing, BOM, whitespace, escape alias, invalid UUID/time/integer, future version | Structure/version denial before key unwrap; exact byte-canonical equality required. |
| OFF-N04 | Truncated frame, integer overflow, inconsistent length, trailing/concatenated bytes, archive/compression/executable section | Reject before allocation/decryption beyond approved bounds. |
| OFF-N05 | At each exact size cap and cap+1; oversized declared/actual/plaintext/header/manifest/page count/page size | Boundary accepts only otherwise valid input; cap+1 rejects without partial activation/unbounded allocation. |
| OFF-N06 | Unsupported/changed algorithm; OAEP label/hash or PSS salt/encoding mismatch; wrong recipient/signer key | Fail closed; no fallback profile or bundle-provided key trust. |
| OFF-N07 | Flip header, wrapped key, nonce, ciphertext, tag, ciphertext length or signature | Signature/integrity denial before evidence visibility; unchanged prior record. |
| OFF-N08 | Trusted key signed altered manifest/page with stale manifest/content/page digest | Authenticated decrypt followed by digest denial; no prepared baseline. |
| OFF-N09 | Unknown/revoked/wrong-scope/wrong-purpose/expired signer; package includes its own public key | Trust denial; imported key material cannot register trust. |
| OFF-N10 | Offline proof replay, expiry, wrong request/collector/assignment/public-key digest; missing custody review | No active registration and no import; exact challenge vectors pending review. |
| OFF-N11 | Recipient revoked/rotated/unknown/unavailable; restored stale key registry | Deny/block per current trusted window; recovery cannot resurrect revoked trust. |
| OFF-N12 | Creation exactly at future tolerance and tolerance+1; expiry exactly `t`; >24h lifetime; beyond run expiry; unknown/backward clock | Exact candidate boundary result, expiry equality denies, clock uncertainty blocks. No retry clock reset. |
| OFF-N13 | Repeat identical envelope before/after activation; same package ID changed bytes; same collection changed content or coverage/lifecycle manifest metadata | Idempotent single identity or conflict; prior immutable evidence preserved. Expired pending retry denies. |
| OFF-N14 | Two concurrent receipts, lease loss/stale writer, online/offline same collection | Exactly one commit/active baseline; stale fence fails; cross-method contract remains pending. |
| OFF-N15 | Crash after each durable state; commit-before-response/publish; replay store outage; orphan blob | Resume/reconcile with revalidation or block; no false completion/second activation. |
| OFF-N16 | Missing/reordered/duplicate pages; missing boundary ref; bad terminal/count/module/schema tuple | Deny malformed/incompatible manifest; no repair/coercion or silent page replacement. |
| OFF-N17 | Duplicate native UID, value conflict, unresolved relationship | Preserve provenance/conflict/explicit gaps under approved schema; never silently merge or show perfect coverage. |
| OFF-N18 | Prohibited secret/profile/government identifier/topology; unclassified field; malware/polyglot/code execution attempt | Quarantine affected unit from activation; inspection failure; approved reason markers only, data never executes. |
| OFF-N19 | Untrusted/mismatched pack/policy/build/module; blocking permission; excess read-only warning omitted | Ineligible for unsafe/missing locks; warned read-only only under existing exception with visible audit/warnings. |
| OFF-N20 | Empty inventory, category failure/truncation, inaccessible installed module, uninstalled module | Explicit empty/gap/partial/not-applicable intent; no fabricated findings or complete coverage. |
| OFF-N21 | Customer disables raw retention; expiry, cancellation, cleanup failure or raw-only deletion | Disclosure and policy clocks honored; no value in cleanup audit; normalized provenance/unavailable markers survive independently. |
| OFF-N22 | Restore older replay/trust/assignment/deletion state | Reapply authoritative tombstones/revocations before import; no reactivation/resurrection. |
| OFF-N23 | Error/status/audit serialization includes decoded rejected value, native ID, crypto bytes or protected header detail | Negative telemetry corpus fails; only approved metadata schema allowed. |
| OFF-N24 | 100,000 maximum-size synthetic records per applicable category; queue saturation/inspection deadline | Bounded resources; measured capacity or explicit design gap/partial outcome; no unsupported cap increase or gate claim. |

Quality evidence must include exact input/envelope digests, schema/algorithm revision, fixture IDs, actual state/results, logs inspected, concurrency/crash checkpoints and tool versions. At least two independently produced fixed serialization/signature/decrypt vectors are required; randomized primitive round trips alone do not prove byte interoperability. Synthetic secret-shaped negative values must remain in a controlled test corpus, with no live secret/customer data committed or published.

## Installation and transfer decision checklist — unapproved procedure

These are required review inputs, not executable installation instructions. No MSI authoring toolchain, production signing key, certificate issuer, timestamp service or publisher has been chosen. [The local shell](collector-local-service-contract-proposal.md) remains blocked from package creation/source work.

| ID | Requested owner / exact review input | Completion condition |
|---|---|---|
| INST-D01 | Operations + security: MSI toolchain options, signing/timestamp/publisher custody, digest publication and provenance/SBOM verification | Named approved tool/profile/publisher and protected release evidence; verify unknown publisher/tamper/revocation/time cases. No artifact generated by this draft. |
| INST-D02 | Customer collector administrator + operations: dedicated service identity and SQL Integrated/DPAPI fallback bindings | Customer-approved least-privilege identity/source assignment; no broad SQL/Azure privilege; protected credential storage and 2022/2025/Core identity tests. |
| INST-D03 | Operations + security: config/key/run/queue paths and ACL/reparse/ancestor checks | Approved exact ACLs, no broad read/write/replacement, protected inherited state, key reload/permission-denial tests under actual service identity. Existing ephemeral checks alone do not complete it. |
| INST-D04 | Security + customer administrator: distinct offline signer and recipient bootstrap/rotation/recovery | OFF-D02–04 accepted or replaced; custody/possession/fingerprint records, revocation propagation and lost/compromised key cases executed. |
| INST-D05 | Operations + customer evidence authorizer: transfer media/channel, chain of custody and customer-controlled cleanup | Protected transfer procedure/digest verification, no email/public-link default; explicit local queue/failed-handoff/expiry behavior and separate raw/normalized handling. |
| INST-D06 | Technical + operations: release/config/pack/checkpoint/stage/envelope compatibility matrix | Exact compatible upgrade/downgrade versions; stop/drain/manual install/revalidate/resume and incompatible-state refusal evidence without altering completed baselines. |
| INST-D07 | Operations + customer administrator: uninstall and revocation ordering | Stop schedules/new collection, revoke online/offline trust, then customer-authorized local cleanup; outage/retry procedure and explicit retained evidence outcome; no silent immutable-baseline deletion. |
| INST-D08 | Quality + operations: signed MSI and Server Core validation matrix | 2022/2025 Full/Core install/start/stop/restart/upgrade/rollback/removal, offline host, Integrated/SQL fallback, interrupted install and repair exercised with approved synthetic source harness. |
| INST-D09 | Technical + security: disabled-until-approved release/adapter linkage | Unknown/missing pack/policy/envelope/trust/material never opens SQL/writes package; no inbound listener/self-update; all applicable checks/evidence recorded before separate customer release authorization. |

## Human review tasks and enablement completion

Each task can link directly to this section from the private human board. Owner roles below must record an explicit accept/replace/reject decision, artifact revision/digest, date, protected evidence reference and remaining gap. No task is closed by the direction approval, an agent review, a repository merge or passing primitive tests.

| Task / anchor | Requested role | Completion condition |
|---|---|---|
| [Exact envelope and receiving design](#candidate-decisions-for-explicit-acceptance-or-replacement) | Technical owner | OFF-D01, D05, D07, D08 accepted/replaced; exact schemas, operation errors, compatibility, page dictionary/conversion, conflict/coverage units and transaction/lease/activation design approved; governed specs/test plan updated. |
| [Offline signing and trust bootstrap](#candidate-signer-recipient-and-bootstrap-decisions) | Security owner + customer collector administrator | OFF-D02–04 accepted/replaced; named real custody/identity authorities, recipient distribution, manual registration proof schema, revocation/recovery and inspection boundaries approved; negative trust/crypto/authorization evidence reviewed. A real production signer is a later controlled choice. |
| [Lifetime, replay and recovery](#candidate-clock-bounds-and-lifecycle) | Operations + security + data-governance/customer evidence-policy authorizer | OFF-D05–07 accepted/replaced; exact import/clock/queue/inspection/replay retention and purge/restore horizons recorded; lawful customer lifecycle/disclosure confirmed; restoration cannot resurrect imports, trust or deleted evidence. |
| [Installation and customer handoff](#installation-and-transfer-decision-checklist--unapproved-procedure) | Operations owner + customer collector administrator + security owner | INST-D01–09 have approved decisions and applicable executed host/release evidence; actual publisher/toolchain and customer procedures supplied without source/release authority being inferred. |
| [Contract fixture evidence](#contract-negative-fixture-matrix--planned-not-executed) | Quality owner | OFF-N01–24 actual results and independent exact-byte vectors recorded for the approved revision; resource/capacity gaps explicit; no disabled adapter/gate falsely reported complete. |

Open enablement-blocking decisions are the OFF/INST approvals above, exact page/category/marker/permission/schema dictionaries, hosted import/activation operation authorization mappings, real trust/key lifecycle and authoritative clock/provenance validation, replay lifecycle/storage/recovery, sandbox limits/inspection policy, cross-method dedup and category-scale capacity. No source-environment fact or source authority can be supplied by this package draft. Exact source/query/customer database-owner and G2 evidence remain their existing separate gates; production release/irreversible operations require their own explicit authorization.

## C2 packet evidence and limitations

This packet adds only this draft Markdown file. No dependency, code, migration, environment configuration, signer, customer evidence, source connection, package exchange, installer or hosted operation changes occur. The worker reads the governing records and checks local links, scope ownership and diff whitespace; executed results are returned to the coordinator for independent review and canonical recording. OFF-N fixtures and MSI/receiving/security/lifecycle tests are planned here and **NOT EXECUTED** by this documentation packet. All C0/C3 delivery approval/evidence gates and G1–G9 remain unchanged.
