# HTTPS-PC02: production shared protection key contract

Status: **Accepted preferred local strategy — exact artifact/guard/inventory prerequisites and live activation remain blocked**

Last updated: 2026-10-03 UTC

Product specification: [health assessment](../../specs/003-health-assessment/product-spec.md)

Author: bounded HTTPS-PC02 worker; reviewers: technical, platform, security and operations owners

Preparation baseline: `b065b51ad97eda9cd96dc08504802b974e819cd1`

## Scope and requirement mapping

This packet specifies the missing production adapter behind [BffSharedProtectionContract](../../src/server/hosts/BffFoundation/BffHostingContracts.cs) and the existing framework `IDataProtectionProvider` consumed by [PostgreSqlTicketStore](../../src/server/modules/IdentitySessions/PostgreSqlTicketStore.cs). It changes no public API, runtime, dependencies, policy, permissions or resources. Actual deployment bindings remain in the owner-only [binding intake](bff-production-bindings-template.json). The diagnostic host remains permanently disabled; preparing this contract does not authorize its promotion or a production host.

Follow accepted [ADR0004](../../architecture/decisions/ADR-0004-azure-pilot-technology-platform.md), [ADR0009](../../architecture/decisions/ADR-0009-production-bff-authority-and-audit.md), [ADR0011](../../architecture/decisions/ADR-0011-https-pilot-portal.md) and [P04](bff-production-authority-hosting-proposal.md#p04--proxy-and-shared-protection-keys). The coordinator owns the overarching host, outage and approval records. This focused proposal selects no availability exception or key deletion schedule.

| Approved source | Contract contribution | Planned evidence |
|---|---|---|
| [NFR-SEC-1/3/4/5/6/10](../../product/non-functional-requirements.md), AC-HAS-9 / [TP-HAS-009](../../specs/003-health-assessment/test-plan.md) | Explicit workload identity, private encrypted ring, secret exclusion, environment separation; decryptability never grants resource authorization | KEY-001–005, 011–013, 017 |
| [NFR-REL-3/4](../../product/non-functional-requirements.md), FR-HAS-50 | Replica/restart continuity and verified coordinated recovery; the approved recovery objective does not authorize dropping an unrecoverable wrapping version | KEY-006–010, 014–016 |
| [Identity/session design](../security/health-assessment-identity-session-design.md), [audit policy](../security/health-assessment-audit-policy.md), NFR-SEC-7 / NFR-COM-2 | Session authority and atomic audit remain mandatory; key availability or rotation cannot extend authentication or replace revocation | KEY-010–012, 015, 017 |
| [HTTPS test addendum](https-pilot-portal-proposal.md), [preparation](../../plans/active/https-pilot-portal-preparation.md), [next packet](https-pilot-portal-next-packet.md) | Contributes to HTTPS-T03/T05/T06/T08; these gates require deployed evidence beyond this document | KEY-001–018 |

Existing [local tests](../../tests/integration/BffHostingContracts.Tests/Program.cs) exercise the actual .NET Data Protection framework with **synthetic** XML repository/encryptor fixtures. They establish local separation, wrapping, cold/warm and conflict behavior only. Their XML format and simulated ETags do not establish Microsoft provider compatibility, Azure permissions or deployed continuity. All KEY cases below are planned, **NOT EXECUTED** by this packet.

## Exact bindings and boundaries

| Binding | Proposed invariant using existing types |
|---|---|
| Environment | Nonempty stable `EnvironmentId`; discriminator is exactly `IGAMigrationTool.Bff.{EnvironmentId:D}.v1` with the existing lowercase, hyphenated GUID rendering. Immutable across compatible replicas/revisions. A new environment uses a distinct discriminator, repository and wrapping key. |
| Ring | Exactly `https://<account>.blob.core.windows.net/bff-data-protection/keyring.xml`; canonical lowercase account, existing validator, HTTPS, no SAS/query/fragment/userinfo. Dedicated private control-plane container; no customer artifacts or other workload keys. |
| Wrapping configuration | Exactly `https://<vault>.vault.azure.net/keys/bff-data-protection`, canonical existing validator, **versionless** for new wrapping. Historical encrypted entries retain concrete versioned identifiers. |
| Identity | Explicit nonempty `ManagedIdentityClientId` selects the dedicated BFF user-assigned identity. Protected intake separately proves its principal/object ID for grants; those identifiers are not interchangeable. No credential chain or developer identity fallback. |
| Purpose/format | Preserve `IGAMigrationTool.IdentitySessions.Ticket.v1`, cookie scheme `IgaBffCookie`, OIDC scheme `IgaBffOidc`, and framework purpose derivation. No caller-controlled purpose/discriminator or hand-built replacement cookie format. Inventory additional production protected values before activation. |
| Deployment | Private Blob/Key Vault DNS and routing from each replica, explicit identity attachment, compatible locked runtime/provider versions and reviewed bootstrap state. A valid URI does not prove reachability, permission or readiness. |

Proposed custom runtime DataActions, **not grants**: Blob `Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read` and `Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write` at `/subscriptions/<subscription>/resourceGroups/<group>/providers/Microsoft.Storage/storageAccounts/<account>/blobServices/default/containers/bff-data-protection`; Key Vault `Microsoft.KeyVault/vaults/keys/read`, `Microsoft.KeyVault/vaults/keys/wrap/action`, `Microsoft.KeyVault/vaults/keys/unwrap/action` at the dedicated `/.../providers/Microsoft.KeyVault/vaults/<vault>/keys/bff-data-protection` scope, covering required versions. Exact supported assignments/actions need SDK and Azure negative proof. No deletion, purge, export, key creation/rotation administration, account-key retrieval, SAS issuance or role administration is requested.

Container-scoped Blob RBAC authorizes more than this object's name: use a dedicated container and exact adapter URI; do not claim object-level RBAC isolation. Workers/renderers receive no ring permission. Key/container provisioning, recovery and lifecycle operations belong to separately reviewed named operators. Built-in Storage Blob Data Contributor includes deletion and surplus actions; Key Vault Crypto Service Encryption User includes additional EventGrid management actions. Neither is silently substituted for the proposed custom roles. [Storage actions](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/storage#storage-blob-data-contributor), [Key Vault actions](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/security#key-vault-crypto-service-encryption-user), [Entra Blob authorization](https://learn.microsoft.com/en-us/azure/storage/blobs/authorize-access-azure-active-directory).

## Provider composition and dependency decision

Preferred path remains the P04 Microsoft extensions: `Azure.Extensions.AspNetCore.DataProtection.Blobs` and `Azure.Extensions.AspNetCore.DataProtection.Keys`. The new production composition must validate the existing record, explicitly call `SetApplicationName`, attach Blob persistence and Key Vault protection, and supply `ManagedIdentityCredential` for that exact identity. Key Vault encryption alone does not configure persistent storage. No file, plaintext, ephemeral or unwrapped fallback is allowed. [Microsoft configuration guidance](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

| Option | Compatibility and decision |
|---|---|
| **Preferred: Microsoft providers plus narrow lifecycle guards** | Keep supported persistence/crypto and standard framework `IDataProtectionProvider`. Public BlobClient/factory and `IKeyEncryptionKeyResolver`/factory overloads permit supplied clients/resolvers. Prove all actual read/write/resolve paths are guarded on the selected packages. No replacement repository protocol is preferred. |
| Custom `IXmlRepository` bridge, retaining supported Key Vault encryption | Existing synthetic seam demonstrates a framework extension point, not a production implementation. Owning canonical XML, conditional merge, retry, cache and recovery semantics adds a durable maintenance constraint; requires explicit architecture review/Proposed ADR before selection. No synthetic encryptor reuse or custom cryptographic format. |
| Read-only replicas with a separate key writer | `DisableAutomaticKeyGeneration` is a supported framework option, but requires a read-only runtime credential and separately bound bootstrap/rotation capability. Adds a new identity/role boundary and rotation protocol requiring human architecture review. Missing/no viable ring must deny without creation; disabling generation alone does not guarantee new protection refuses expired keys. Not selected here. |

Source inspection on 2026-10-03 found public [Blob client/factory overloads](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Blobs/src/AzureStorageBlobDataProtectionBuilderExtensions.cs) and [Key Vault resolver/factory overloads](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureDataProtectionKeyVaultKeyBuilderExtensions.cs). These are mutable `main` sources, **not** proof of any installed package. Exact stable package/runtime versions, corresponding source commits, transitive versions, licenses, lockfile/content hashes and dependency audit remain implementation prerequisites. No package was installed or selected by this packet.

The adapter is composition-local: input is the validated existing contract plus reviewed immutable deployment/bootstrap evidence; output is one configured framework provider and internal readiness result. No new public API is proposed. A guard may use supplied BlobClient behavior or a tested SDK pipeline; the implementer must prove interception of every synchronous repository overload, retries and deletion races. If this cannot be achieved through supported APIs without replacing the provider, stop for architecture review. A startup-only existence probe does not close later automatic key creation after deletion.

For the resolver guard, allow the configured versionless URI only when resolving a new wrapping key; historical unwrap must use a concrete version under the **same exact vault and key path**, without query/fragment/alternate host. Validate the selected SDK's version identifier grammar and concrete resolved identifier before use. Reject unexpected XML/decryptor types or redirects to another binding. Supported formats and size/parser limits require explicit implementation review; do not accept arbitrary caller-provided XML or resolver destinations.

## Bootstrap, concurrency and acknowledgment contract

Proposed states are `Unbound/Denied`, `BootstrapAuthorized`, `ExistingReady`, and `RecoveryRequired`; these are internal design states, not a new public schema. An ordinary BFF replica can enter readiness only using reviewed **existing** storage. Bootstrap is a separately authorized initialization in a precreated dedicated container with explicit proof that no previously protected data depends on a missing ring. Persist wrapped material, read it back and prove cold unwrap before accepting bootstrap. No request or restart can self-authorize bootstrap.

Microsoft's current [Blob repository source](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Blobs/src/AzureBlobXmlRepository.cs) merges XML using the previous ETag / `If-Match`, or `If-None-Match:*` when absent; it rereads after conflicts and eventually throws. It treats 404 and zero-length content as an empty repository. These inspected details must be revalidated on locked artifacts; provider retry constants are not an approved service-level policy.

The proposed guard must reject 404, empty/invalid ring and missing expected key inventory in **existing mode**, including during rotation and after retries; it must forbid absent-object conditional creation in this mode. Bootstrap alone may create using `If-None-Match:*`. A concurrent bootstrap winner is reread and validated against the approved binding/inventory; never overwrite it. No unconditional upload, deletion or exception-swallowing retry is allowed. [Azure ETag conditions](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage).

The guard must inspect successful downloaded content as well as HTTP errors, and reject every existing-mode upload lacking a concrete `If-Match`. A delete after read makes that conditional update fail; retry must deny missing content rather than change to create. ETags alone cannot detect an authorized operator deleting/recreating a valid older ring, nor prevent a principal with ring-write permission inserting keys. Detecting inventory rollback requires protected bootstrap/recovery inventory and a reviewed monotonic update/witness mechanism; its persistence/authority is an unresolved consequential design decision, not an implemented safeguard. A plain writable provider or a preflight probe alone cannot satisfy KEY-014/016. The read-only alternative also needs this recovery/integrity review.

For concurrent append/rotation, preserve every existing key and revocation record, reread after 409/412, merge without losing another replica's entry, and propagate terminal failure to the caller. A stale cached ring must not override newer storage. SDK HTTP retry bounds/timeouts and service readiness freshness are unresolved configuration inputs; no infinite retry or invented durations are selected here.

An upload whose acknowledgment is lost has an **unknown** outcome. Freshly read durable storage and compare the attempted key ID/entry and full preserved inventory; neither assert rollback nor blindly replace the ring. If outcome cannot be established, deny the dependent issuance. Require tests of SDK retries and duplicate-entry handling; no exactly-once Azure write guarantee is assumed. Do not expose keys, protected values or retryable cookies through reconciliation.

Blob persistence and SQL are separate transactions. A successfully persisted unused ring key can remain after a SQL rollback; it does not authorize a session. Protection/persistence must succeed before the existing audited SQL session issuance can finish, and cookie emission still waits for its confirmed commit. Unknown SQL commit remains metadata-only receipt reconciliation under P03; this adapter cannot replay a cookie or alter authority.

## Wrapping rotation and failure behavior

New Data Protection material is wrapped using the configured versionless key. The provider records the resolved version as `kid`; historical decryption resolves that stored version and does not substitute the current version. Current encryptor source uses `RSA-OAEP`; the reviewed key type/operations must be compatible, with size/HSM choice still pending. Key Vault wrapping rotation and Data Protection key generation/activation are different lifecycles. Test both without inventing a rotation interval. [Microsoft encryptor](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureKeyVaultXmlEncryptor.cs), [Microsoft decryptor](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureKeyVaultXmlDecryptor.cs).

| Failure/transition | Proposed mandatory boundary; unresolved policy remains a blocker |
|---|---|
| Wrong binding/identity, unavailable private DNS or denied ring read | Cold replica is not ready; no bootstrap/fallback or cookie issuance. Return bounded payload-free failure. |
| Conditional write or new-key wrap failure | No success reported for dependent protection/issuance/renewal. Preserve previous durable ring; verify no accepted session/cookie escaped. |
| Historical wrapping version disabled/deleted/inaccessible | Cold unwrap of dependent material denies. Never rewrap undecryptable data with newest key or ignore revocation. |
| Warm replica during Blob/Key Vault outage | Cached cryptographic material can remain usable. That does not establish persistence health, fresh authority or durable failure audit. The coordinator's reviewed outage/readiness contract must explicitly determine allowed protected operations; no cached-access exception is selected here. |
| Key/grant revocation while warm | Do not promise immediate cache eviction. Product subject/session revocation and replica drain/restart remain required incident controls; unavailable keys are not a logout mechanism. |
| Corrupt, truncated, replayed older or substituted ring | Deny readiness and dependent operations; preserve incident evidence in protected operations storage. Validate recovery inventory before reopening; do not create a replacement ring. |

Framework key expiry does not itself prevent historical unwrap, while deletion can permanently destroy it; disabling automatic generation can select an expired protection key. Therefore the reviewed generation/lifetime/activation policy must explicitly govern new protection readiness. Framework defaults are not adopted as pilot policy. [Framework key management](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-management?view=aspnetcore-10.0).

## Lifecycle, restore and compatibility

Keep all Data Protection entries and wrapping versions needed by live protected values **and every recoverable backup**. No deletion interval, Blob version/soft-delete window, vault purge protection setting, key size/type, automatic rotation schedule or cleanup operator is approved here. Disposable vault settings, audit12months, customer data purge deadlines and database backup maxima do not silently become key-retention policy. Owners must reconcile these inputs and approve exact recovery/deletion eligibility before activation.

Recovery evidence must bind the discriminator/purposes, compatible runtime/provider/decryptor format, ring object version/ETag plus key/revocation inventory, concrete wrapping versions, protected SQL snapshot and authoritative revocation/tombstone state. Restore into isolation with no session admission; verify all retained required payloads and replay required authoritative revocations before reopening. A ring predating a retained protected value, omitted revocation record or unavailable wrapping version is a failed recovery, not permission to rebootstrap or bypass checks. Preserve the independent audit witness and verify the existing restore contract separately.

Do not migrate synthetic test XML/payloads into production. A first real deployment needs an approved fresh bootstrap; a later compatible revision must retain the real ring, discriminator, schemes and purposes. Mixed-version cold replicas must prove cross-decryption on the actual selected artifacts before rollout/rollback. Never restore older authority versions or delete historical keys to roll back an image. Rollback denies/drains an incompatible host while preserving protected stores for recovery.

Telemetry includes bounded readiness/failure category, operation/correlation and approved metrics for read/wrap/unwrap/conditional conflicts; no ring XML, plaintext/wrapped key bytes, tokens, cookies, payloads or actual protected binding identifiers in Git/logs/site. Ordinary logs are not durable security audit. Key lifecycle event producers/approved audit vocabulary and outage preservation are coordinator dependencies; this packet does not invent a `KeyRotated` event or log-based fallback.

Inspect SDK-generated diagnostics and exported Activity attributes as well as application logs: current [KeyResolver source](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/keyvault/Azure.Security.KeyVault.Keys/src/Cryptography/KeyResolver.cs) attaches key identifiers to diagnostic scopes. Explicit suppression/redaction must be proven on selected packages before telemetry export; safe application logging alone is insufficient. Resolver results without a concrete approved version must deny, including a metadata-read-denied fallback.

## Planned KEY evidence

`L` means locked Microsoft provider + actual framework against deterministic SDK transport/client/resolver fixtures. `A` means separately authorized Azure deployment/private networking with actual managed identities; `C` means composed BFF/session/audit recovery. All rows are **NOT VERIFIED** until their evidence is executed and indexed. Deterministic acknowledgment loss must be labeled simulation; it is not a real TCP interruption test.

| Stable ID | Required scenario and assertion | Layer; mapping |
|---|---|---|
| KEY-001 | Reject empty IDs, noncanonical/foreign URI, SAS, versioned configuration and altered discriminator; exact binding round-trip | L; SEC-3/4/6/10, HTTPS-T03/T05 |
| KEY-002 | Two cold replicas read one durable wrapped ring; ticket/cookie/OIDC protected state continuity and restart with retained inventory | L+A+C; REL-3/4, HTTPS-T03 |
| KEY-003 | Cross-environment discriminator denies; separate rings/keys and wrong workload identity cannot read/write/unwrap | L+A; SEC-1/5/6/10, AC-HAS-9, HTTPS-T05 |
| KEY-004 | Exact proposed roles work; delete/purge/admin/other container/key denied; private endpoint DNS/network negatives | A; SEC-1/4/5, HTTPS-T05 |
| KEY-005 | Persisted XML contains supported wrapped material; no plaintext/secret/cookie leakage in output or diagnostics | L+A; SEC-3/4 |
| KEY-006 | Concurrent first bootstrap: one conditional winner, loser rereads valid inventory; neither unconditional overwrite nor missing keys | L+A; REL-3, HTTPS-T03 |
| KEY-007 | Concurrent rotation/appends with stale ETags preserve both new keys, old keys and revocations | L+A; REL-3/4, HTTPS-T03 |
| KEY-008 | Forced repeated 409/412 and terminal write failure deny dependent issuance; no ephemeral fallback/lost existing ring | L+A+C; SEC-4, REL-3 |
| KEY-009 | After successful upload simulate lost acknowledgment; durable reread resolves attempted entry, or denies unknown; no overwrite/cookie replay | L+C; REL-3, HTTPS-T06 |
| KEY-010 | Ring ready but SQL/audit commit fails or is unknown: no unconfirmed cookie, no false rollback, metadata-only P03 reconciliation | C; SEC-7, HTTPS-T06 |
| KEY-011 | New wrapping after vault rotation uses new concrete version; old payload cold-unwraps via old version; wrong/foreign `kid` denied | L+A; SEC-4/6, HTTPS-T03/T05 |
| KEY-012 | Disable/remove historical version or grant: cold failure; demonstrate warm cache limitations plus product revoke/drain authority denial | L+A+C; SEC-5/7, HTTPS-T06 |
| KEY-013 | Blob/Key Vault unavailable separately: cold readiness/protection fail; warm operations obey approved outage decision with mandatory authority/audit | L+A+C; SEC-4/5/7, HTTPS-T06 |
| KEY-014 | Existing ring deleted/zeroed/corrupted between read, retry and rotation: no automatic bootstrap or conditional recreation | L+A; REL-4, HTTPS-T06 |
| KEY-015 | Restore approved inventory + SQL/authority/tombstones/witness in isolation; revoked session remains denied after restore | A+C; REL-3/4, FR-HAS-50, HTTPS-T06 |
| KEY-016 | Restore older ring/missing required key/version/revocation or incompatible decryptor: deny admission; retain recovery evidence | L+A+C; REL-4, HTTPS-T06 |
| KEY-017 | Compatible mixed revisions/rollback, expired/revoked key selection negatives, unchanged schemes/purposes/authority bounds | L+C; SEC-5/7, AC-HAS-9, HTTPS-T03/T06 |
| KEY-018 | Bounded session disposal preserves non-disposable ring/wrapping versions/recovery artifacts; reviewed costs include versions and Key Vault operations | A; REL-4, HTTPS-T08 |

## Review and remaining decisions

Before implementation approval, reviewers must close: exact package/runtime/source locks and audit; supported guard mechanism and resolver/XML compatibility; protected inventory/rollback witness or reviewed boundary amendment; bootstrap operator/evidence; permitted new-key generation/lifetime/activation; exact scoped custom roles; coordinated backup/recovery and eventual deletion eligibility; readiness freshness and the overarching outage/audit preservation decision. No actual bindings belong in this file.

After approval, local adapter tests precede independent review and any separately authorized Azure spike. Live roles, paid session, identity activation, public exposure and release each retain their existing authorization gates. This proposal alone leaves HTTPS-T02–T08, G1–G9 and Milestone2 **NOT VERIFIED**.

Approval: Repository owner accepted the preferred local strategy on2026-10-03 UTC; exact artifact/guard/inventory inputs, named live reviews and production authorization remain unresolved.


## Owner approval and current implementation scope — 2026-10-03 UTC

Repository owner replied “Approved” after exact review packet `d518d25` and PC-D01–PC-D05 were presented. This accepts the recommended local design and supporting tests; the earlier proposed-state descriptions are the preparation record. [Local cycle01](../../plans/active/https-production-local-cycle01.md) records the exact accepted scope, implementation/test sequence and dependencies. No unspecified quantity, inventory authority, public step-up/outage response, actual tenant/role/ingress proof, cloud deployment, spending or production release is supplied by this approval. External users and privileged routes remain denied until their independent evidence/contracts close. Only explicitly executed cases may be marked verified.
