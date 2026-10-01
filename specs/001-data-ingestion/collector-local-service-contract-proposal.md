# Proposal: Pilot collector local service and CLI contract

Status: Approved for pilot-local implementation by repository owner standing preapproval; source and release gates remain open
Scope: One Identity Manager 10.x Windows collector only  
Owner: Technical owner  
Last updated: 2026-09-30

## Decision and limit

The repository owner preapproved all pilot-local work on 2026-09-29 and directed development to continue without local review pauses. This local command, configuration and schedule contract is approved for implementation under that standing authorization. It does not authorize a source connection, package exchange, MSI install, enrollment or release.

## Proposed process boundary

- One self-contained `win-x64` executable contains both host modes. The signed MSI registers the Windows Service; the same signed release offers a one-shot CLI. Neither mode listens on an inbound port or self-updates.
- The service starts only from a customer-administrator installed, protected absolute configuration path recorded by the installer. It refuses collection if the configuration, required signatures, exact build or policy lock is absent or incompatible. The pilot shell may stay running to report a payload-free blocked state; an invalid or unprotected configuration stops it.
- The one-shot command is `collector collect-once --config <absolute-local-path> --offline-output <absolute-local-path>`. It shares the service's policy, query-pack, permission and checkpoint core. It does not accept SQL text, field lists, passwords, connection strings, endpoint overrides or arbitrary query parameters on the command line.
- A separate `collector status --config <absolute-local-path>` command returns local operational state only. It does not run a query or reveal SQL text, row values, host topology, protected identifiers, credentials or package contents.
- The installer invokes `collector service --config <absolute-local-path>` as the Windows Service command. This verb has the same protected configuration boundary and never accepts an output path or query overrides.
- The executable has no install, enroll, revoke, upgrade, delete-evidence or source-write command. Those workflows require their separately approved customer-administrator and hosted operation contracts.

## Proposed protected configuration

The local file is versioned JSON with strict schema validation. Unknown future schema versions and unknown security-relevant properties fail closed. The installer creates its directory with an ACL granting write access only to customer administrators and the dedicated service identity. The service verifies the ACL before reading configuration. The file contains references, limits and opaque scope IDs, never plaintext SQL credentials or source evidence.

| Field group | Proposed contents | Boundary |
|---|---|---|
| Version and scope | `schemaVersion`, opaque project/environment/source/scope IDs | Hosted assignment remains authoritative; no data-plane locator |
| Source access | Protected local SQL connection descriptor reference and auth mode | Descriptor and any SQL fallback secret stay DPAPI/ACL protected; never uploaded or logged |
| Exact eligibility | Exact build/module evidence reference, capability row version, promoted query-pack ID/version/digest | Drift pauses collection; the config cannot promote its own pack |
| Minimization | Signed customer field/category policy ID/version/digest | Effective policy is locked per run and applied before staging |
| Schedule | Customer environment timezone, local run time, enabled flag | At least daily while enabled; overlap skipped; missed offline run not replayed |
| Bounds | Page, row, duration, concurrency, local-byte and retention limits | Values may narrow approved pack/operations maxima but cannot widen them |
| Delivery | Offline or outbound mode plus approved key/endpoint references | No raw URL override; actual upload/envelope contracts remain separate |

Configuration changes are written atomically by an administrator, validated before becoming active and recorded in payload-free audit. A running extraction keeps its original config/policy digest; a change takes effect on the next eligible run or pauses a run whose safety lock can no longer be honored. The config contains no customer database topology in exported status or platform evidence.

## Proposed schedule and result behavior

- Service scheduling uses the configured customer timezone and local time. On a daylight-saving gap, skip that day's nonexistent local occurrence. On an ambiguous local time, use the later occurrence once. Missed offline occurrences do not catch up.
- A per-scope in-process gate skips overlapping runs. Restart recovery also requires a durable encrypted checkpoint and a cross-process/instance lease; neither is implemented by the current in-memory gate.
- The CLI exits nonzero for invalid configuration, unsupported build, blocking permission, source/query failure, canceled work, local limit, package failure or incompatible checkpoint. A partially completed run is never reported as fully complete.
- Service status and CLI output use stable event/error codes, counts, times, category-level gaps and opaque correlation IDs. They never print SQL text, query rows, secrets, topology or package payload. The implemented shell's exact codes are below; collection-phase codes require their own implementation contract before source access.

## Pilot-local shell contract, version 1

The executable accepts only the three commands above. `--config` and `--offline-output` require a drive-qualified local Windows path (`C:\...`). UNC, device, relative, traversal, empty-segment and alternate-stream paths are rejected. The current one-shot command creates no file because no query pack or offline envelope is approved.

The JSON root has exactly these case-sensitive properties; duplicate and unknown properties fail. `schemaVersion` is integer `1`; `scopeId`, `queryPackId`, `fieldPolicyId` and `offlineRecipientKeyId` are nonzero UUIDs in D form. `exactBuild` is nonempty text up to 128 characters. `queryPackVersion` and `fieldPolicyVersion` are positive integers; each `*Sha256` is exactly 64 hexadecimal characters. `sqlDescriptorRef` is a local absolute path, not a connection string. `timeZoneId` must resolve on the host; `localRunTime` is `HH:mm`; `enabled` is a boolean. `maxPageSize`, `maxRows`, `maxDurationSeconds` and `maxLocalBytes` are positive integers and cannot authorize work until compared with a promoted pack's limits. `retentionHours` is 1–720. The file is at most 64 KiB. The field references and digests are claims, not proof of signed pack/policy promotion.

The shell emits one of the following payload-free codes. `status` exits `0` with `COLLECTOR_DISABLED` or `SOURCE_CONTRACT_PENDING`. Invalid arguments exit `2` with `ARGUMENTS_INVALID`; invalid/unprotected configuration exits `3` with `CONFIG_INVALID`; a non-Windows host exits `4` with `PLATFORM_UNSUPPORTED`. `collect-once` exits `5` with `COLLECTOR_DISABLED` or `SOURCE_CONTRACT_PENDING`. The service logs those states and `RUN_NOT_STARTED_CONTRACT_PENDING` at an enabled scheduled occurrence, without running SQL or writing an offline package. Future collection states and codes must be documented with the matching implementation.

The Windows reader rejects reparse points and disallowed write-capable ACL entries on the config file and its immediate directory, and disallowed replacement-capable entries on ancestors. An invalid ACL stops the service. Windows Server 2022/2025 ACL and service-identity behavior still require controlled host tests; this local build alone does not prove installability.

## Pilot-local checkpoint ledger, version 2

The internal checkpoint file begins with ASCII `IGC2`, a 12-byte random AES-GCM nonce, a 16-byte tag and ciphertext. Plaintext is versioned JSON containing the exact query/build/scope/policy/order context and an ordered array of completed page boundaries, SHA-256 digests, nonnegative row counts and terminal-page markers. The exact context is authenticated as associated data. Earlier count-less `IGC1` files fail closed; this pilot has no customer-deployed checkpoint migration. On restart, the coordinator restores cumulative rows, applies the configured row cap before another read, and returns completion without rereading a terminal page. The 32-byte local key is generated with a cryptographic RNG and protected by Windows DPAPI `LocalMachine` with scope-specific entropy. Its blob must be stored in a file passing the same ACL/reparse checks as the configuration; possession of the blob on the same machine must remain restricted by that ACL. An isolated create-once provisioner now writes a new blob into an existing restricted local directory and rejects replacement of an existing key. Installer/customer-administrator provisioning, service-identity binding, recovery and rotation procedures remain open. A wrong key/context, malformed file, duplicate page or changed completed-page prefix blocks recovery. A save writes and flushes a same-directory temporary file before replacing the ledger. The plaintext ceiling is 1 MiB and page ceiling 4,096. The shared local coordinator holds the run lease while using the ledger. Synthetic tests cover a forced child-process lease release and an idempotent retry after a failed checkpoint write; production sink and customer-host crash/Windows durability evidence remain open.

The coordinator now requires the run-start and checkpoint paths to be distinct files in one existing run directory and passes that directory to the staging adapter. On Windows, it validates the directory ACL and reparse boundary before a source read. The checkpoint store validates its parent and file ACLs on Windows, checks the temporary file before replacement, and includes transient replacement bytes and all existing run files in `maxLocalBytes`. A byte-cap refusal leaves the prior checkpoint intact. Synthetic coordinator checks use the isolated protected run-directory provisioner on Windows. The shipped adapter remains blocked; installer binding, key lifecycle and customer-host recovery are still separate work.

On resume, the coordinator authenticates each checkpointed page from that same encrypted stage directory and compares its row count, terminal marker and digest with the ledger before another source read. A missing or altered stage returns `CheckpointRejected`; the ledger is not silently treated as recovered evidence. An orphan stage without a checkpoint remains available for an idempotent retry of that page.

The isolated encrypted page-stage file is now `IGS2`. Its digest also binds row count and terminal state. A new page is admitted only when its encrypted length plus all existing files in the run directory fit `maxLocalBytes`; an identical already-staged page may be replayed without another write. This preflight assumes the caller holds the run lease. It does not implement expiry, cleanup, key rotation or an aggregate queue shared by multiple runs.

An isolated Windows run-directory provisioner can now create a new GUID-named child under an already protected parent with a protected ACL for the current service identity, local Administrators and LocalSystem. It rejects an existing child or an unprotected parent and revalidates the result. It is not wired into the shipped service: installer selection of the parent, run-ID binding, recovery, cleanup and customer-host checks remain open.

The shared coordinator now evaluates `retentionHours` against a persisted extraction start before each page and again before staging. The internal `IGR1` AES-256-GCM run-start record binds the exact checkpoint context and is created once in the protected run directory before reading. Its encrypted bytes and every existing run-directory file count against `maxLocalBytes` before creation; a resumed record fails closed if a narrowed cap is below current usage. Retries load its original UTC start and ignore a later adapter-supplied timestamp. A checkpoint without its run-start record, or an orphan staged page before first record creation, fails closed. The reviewed adapter must keep the run-start path stable for the extraction, bind it to the same protected run directory as staging, and preserve the key and directory across restarts. Expired work returns a typed outcome without another page write. The shipped adapter supplies no run start or source access; local expiry does not itself delete evidence or authorize cleanup.

## Security and test evidence before enablement

- Validate config schema, ACL, source/build/pack/policy locks and protected secret references before any evidence query.
- Reject relative, network-share, traversal and device paths for config and offline output unless security/operations review explicitly approves an exception. Never overwrite an existing offline package silently.
- Test service restart, cancellation, overlap skip, missed schedule, daylight-saving boundaries, config drift, command-injection arguments, status redaction, local storage limits and protected file permissions on Windows Server 2022/2025 including Server Core.
- Verify the service/CLI use the same collector core and cannot select a different query, field policy or permission outcome. A signed MSI and publisher/digest validation remain separate C2 gates.

## Remaining implementation and external gates

- Both entry points now call a shared local coordinator. A synthetic adapter exercises the lease, encrypted checkpoint ledger, page bounds, field minimization and stage-before-checkpoint sequence. The shipped adapter has no approved run material and never reads or stages evidence. Customer-admin key provisioning, output directory ACL/space/overwrite enforcement and signed policy/pack verification must precede source collection or package creation.
- Customer-administrator provisioning, service identity, protected configuration location and MSI signing/install procedure require Windows evidence and reviewed operations steps before customer installation.
- The exact query pack, source permissions and impact plan require One Identity SME and customer database-owner evidence. Offline envelope and receiving import remain separate reviewed contracts.

## Approval

Approved for pilot-local implementation by: Repository owner standing preapproval
Date: 2026-09-29
