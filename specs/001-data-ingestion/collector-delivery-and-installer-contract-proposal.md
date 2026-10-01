# Proposal: Pilot collector delivery and installer contracts

Status: Draft for technical, security and operations review; no delivery or installer implementation approved
Scope: One Identity Manager 10.x pilot collector only
Owner: Technical owner
Last updated: 2026-10-01

## Fixed boundaries

The [approved pilot technical specification](technical-spec.md), [local service contract](collector-local-service-contract-proposal.md), and [ADR-0004](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md) already select a customer-operated, signed, self-contained .NET 10 `win-x64` Windows Service and one-shot CLI. Online delivery is outbound HTTPS after one-time device-certificate enrollment; offline delivery is an authenticated encrypted package. The collector has no inbound listener, remote self-update, Azure identity, cloud-held SQL credential, or authority to select a customer data plane. These proposals cannot enable collection or G2.

## Candidate online enrollment and upload flow

1. An authorized customer administrator requests a one-time enrollment grant for one active customer/project/environment assignment through the hosted application. The server records the granting role, scope, expiry, one-use state, and an opaque grant reference. The grant is not a SQL credential or a reusable upload credential.
2. The collector creates a non-exportable device private key in Windows protected key storage and submits a certificate request and proof of possession over validated TLS. The server validates the one-time grant, current administrator authority, scope state, replay state, key proof, and device policy before registering a device certificate. It binds the device to one server-side assignment and records certificate/key identity and revocation state without accepting a client-selected data-plane locator.
3. For each upload, the collector authenticates as that device over outbound HTTPS. The server checks current device and assignment state and issues short-lived authorization limited to the intended immutable run, scope, policy/pack versions, bytes and expiry. Chunk identities and content digests make same-content retry idempotent; a changed digest for an existing chunk is a conflict. The receiving side validates all chunks, manifest, classification and policy before baseline activation.
4. Revocation denies new enrollment and upload authorization. Existing completed baselines follow their separate evidence-deletion decision. A lost key, expired certificate, suspended scope or changed assignment requires an authorized recovery decision; ordinary service operation never asks for a human credential.

**Contract review must fix:** exact request/response and error schemas, one-time grant delivery and lifetime, device key algorithm/certificate profile and renewal, proof/replay binding, TLS trust requirements, upload authorization lifetime, chunk size/total limits, idempotency and cancellation semantics, server-side scope lookup, credential storage and redacted audit fields. No API route or wire schema is selected by this draft. Technical and security reviewers must approve the exact protocol and negative fixtures before an enrollment or upload adapter is enabled.

## Candidate offline package boundary

The package must identify a versioned header, recipient key identity, opaque assigned scope, collector/query-pack/schema/policy versions, creation and expiry, ordered page/chunk digests, coverage and gap markers, and a content digest. It must contain only minimized permitted evidence. A random per-package AES-256-GCM key encrypts the permitted content, and the platform's approved RSA public key wraps it with RSA-OAEP-SHA256. An authenticated signature must bind the header, manifest and ciphertext digest to a reviewer-approved signing identity. Package possession or a digest alone never grants import authority.

The receiving path must check authorization and assignment, package size and structure, supported versions and algorithms, expiry, replay, signature trust, wrapped-key identity, authenticated decryption, page/chunk digests, prohibited-field policy and immutable baseline compatibility before activation. A failed unit stays ineligible and exposes only approved forensic metadata. Import cannot resolve a customer data plane from a package-provided locator.

**Contract review must fix:** the offline signer and its trust-bootstrap/revocation path when the collector cannot enroll online, exact byte serialization and canonical signature input, key IDs/rotation and accepted algorithms, maximum package and chunk sizes, expiry clock tolerance, import operation and replay store, corruption/duplicate test vectors, and customer-controlled transfer and cleanup procedure. Until these choices and receiving inspection are approved, the existing AES/RSA primitive remains an isolated local component and the CLI writes no package.

## Candidate MSI and customer-admin procedure

The release candidate is one self-contained `win-x64` payload installed by a signed MSI on Windows Server 2022/2025, including Server Core. The MSI registers the existing fixed service command and makes the same executable available for one-shot CLI use. A customer administrator chooses the dedicated service identity, protected local configuration and staging locations, source principal, and enrollment path. Installation provisions no broad SQL or Azure privilege. Config, key and staging paths require the approved ACL/reparse checks; any generated DPAPI key must be recoverable only under the approved machine/scope and local ACL boundary.

An upgrade is a manual administrator action after verifying Authenticode chain, publisher, release version and published digest. It stops collection, checks compatible config/checkpoint/package versions, installs the signed candidate and resumes only after protected state validation. Downgrade requires an explicit recovery record identifying the prior signed version and compatibility evidence. Removal stops schedules and revokes enrollment, while retained evidence and local cleanup follow the customer-selected lifecycle decision; the MSI must never silently delete completed immutable baselines.

**Contract review must fix:** pinned MSI authoring toolchain, signing and timestamp service, service identity creation/selection, directory and key provisioning/rotation, upgrade/rollback compatibility matrix, installer custom-action privileges, uninstall/revocation order, offline-host procedure, publisher/digest publication, and Server Core test matrix. No installer, signing key or release artifact is produced by this draft.

## Required review and validation record

| Reviewer role | Reviewable output before enablement |
|---|---|
| Technical owner | Versioned operation and package schemas; idempotency, scope resolution, compatibility and receiving-state transitions |
| Security owner | Enrollment trust, non-exportable key proof, revocation, TLS, signature/encryption vectors, secret minimization and negative authorization results |
| Operations owner | Customer-admin install/recovery, service identity and ACLs, certificate/key rotation, bounded queue/retention, signed upgrade/rollback and Server Core evidence |
| Quality owner | Synthetic wrong-scope, replay, expiry, corruption, chunk conflict, revoked-device, interrupted install and mixed-version fixtures with expected outcomes |

Record each decision with its artifact version/digest, reviewer role, approval or explicit gap and protected evidence reference. The approved [test plan](test-plan.md) remains the verification authority. No source test, package exchange, customer installation or gate promotion follows from this draft alone.
