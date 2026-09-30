# Proposal: Pilot collector local service and CLI contract

Status: Proposed — technical, security and operations review pending  
Scope: One Identity Manager 10.x Windows collector only  
Owner: Technical owner  
Last updated: 2026-09-29

## Decision needed

Approve or revise this local contract before implementing the customer-facing Windows Service and one-shot CLI. The pilot collector technical specification approves the host profile but deliberately leaves command syntax and local configuration undefined. This proposal does not authorize a source connection, package exchange, MSI install, enrollment or release.

## Proposed process boundary

- One self-contained `win-x64` executable contains both host modes. The signed MSI registers the Windows Service; the same signed release offers a one-shot CLI. Neither mode listens on an inbound port or self-updates.
- The service starts only from a customer-administrator installed, protected absolute configuration path recorded by the installer. The service refuses work if the configuration, required signatures, exact build or policy lock is absent or incompatible. It reports a payload-free status and remains stopped/disabled until corrected by a customer administrator.
- The one-shot command is `collector collect-once --config <absolute-local-path> --offline-output <absolute-local-path>`. It shares the service's policy, query-pack, permission and checkpoint core. It does not accept SQL text, field lists, passwords, connection strings, endpoint overrides or arbitrary query parameters on the command line.
- A separate `collector status --config <absolute-local-path>` command returns local operational state only. It does not run a query or reveal SQL text, row values, host topology, protected identifiers, credentials or package contents.
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
- Service status and CLI output use stable event/error codes, counts, times, category-level gaps and opaque correlation IDs. They never print SQL text, query rows, secrets, topology or package payload. Exact event and exit-code values are a later local-contract appendix; implementation must not invent them independently.

## Security and test evidence before enablement

- Validate config schema, ACL, source/build/pack/policy locks and protected secret references before any evidence query.
- Reject relative, network-share, traversal and device paths for config and offline output unless security/operations review explicitly approves an exception. Never overwrite an existing offline package silently.
- Test service restart, cancellation, overlap skip, missed schedule, daylight-saving boundaries, config drift, command-injection arguments, status redaction, local storage limits and protected file permissions on Windows Server 2022/2025 including Server Core.
- Verify the service/CLI use the same collector core and cannot select a different query, field policy or permission outcome. A signed MSI and publisher/digest validation remain separate C2 gates.

## Open review points

- Confirm executable name, command words, protected configuration location, exact JSON schema, stable exit/event codes and status shape.
- Confirm daylight-saving choice, cross-process lease/store, local offline-output path policy, ACL implementation and service stop/disabled semantics.
- Confirm which customer-administrator workflow writes and signs configuration. Do not implement the service-facing contract until these are approved.

## Approval

Approved by: Pending  
Date: Pending
