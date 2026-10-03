# Continuation recovery independent oracle

Pinned before inspecting McpHarness, CursorRegistry, Projection, PublicationCodec or other implementation source, 2026-10-03, baseline `3551fe5693196ab371282c38bae53d2f9ed23f19`. Derived from approved local-phase1d-approval.md, frozen local-phase1d-implementation-contract.md and P1D-T06/07/09/10/12; original fixture bytes are authoritative. No production behavior is added.

## Expected matrix

Both NamedUser and Service, independently for Findings and Recommendations (the original three-item groups), page size 1, all four categories. Explicit allowed fields: Findings `summary,severity,mandatoryReview,referenceIds`; Recommendations `findingId,summary,options,reviewLabel`. Required `itemId,category` always remain. No expected-output production projection/codec helper is permitted.

1. First page is ordinal item a, successor continuation is ordinal item b and its successor ends on item c. Expected JSON values are taken directly from the corresponding original payload, restricted to the explicit fields. Envelope has exactly contractVersion/resourceKind/manifestDigest/bindings/items/nextCursor. Every scalar binding equals the original manifest scalar; original digest is unchanged.
2. A selected-item barrier observes two overlapping invocations of the same original handle. Each independently evaluates policy, reads exactly item b, attempts and completes its own success audit, returns the same stable item/bindings and a distinct opaque lowercase 64-hex successor. Original handle remains repeatable; successor reads exact item c, final cursor null.
3. Separate selected-item barriers inject cancellation, monotonic time exactly 5 seconds after request start, audit false, and audit throw after continuation resolution. Non-success exposes neither envelope nor cursor. Cancellation outcome Cancelled; other three DependencyUnavailable. Each invocation attempts one completion audit. False/throw attempts are not completed durable events and signal only the safe audit failure enum. Retry original cursor under unchanged authority before its original 300-second expiry succeeds on exact item b, with its own policy evaluation/audit. Selected-item reads and manifest reads are separately observed; no unintended payload read is allowed.
4. Revision changes under the same trusted synchronization fence as TryCommit while selected item b is held. Final stale fence yields Unavailable, no content/cursor, and one generic scope-free audit. Revoke/regrant advances revisions; original handle then fails with zero manifest/item reads. Fresh first page uses the current grant and original item a/bindings.

Every terminal test has a fresh harness, avoiding unrelated rate/capacity pressure. Test clock never approaches cursor expiry (except the exact request deadline at t=5); expiry-at-emission policy is not chosen. Barrier arrival uses observable selected-item calls, never sleep; both arrival and completion waits are bounded and barriers release in finally. Policy evaluations, commit calls, audit attempts and completed events are independently counted. Cancellation/deadline occur before terminal synchronous audit, so these tests make no production end-to-end deadline promise.

## Original fixture SHA-256 pins

- `author_oracle.py`: `a7826822dd4e42ca562b841325307203e728129dc22eeb55f29d267ceef31da9`
- `denial-corpus.json`: `4d3bc8f66eb0eeca7da0838d5819ee383d1a1d9aa8ca050b3acf2280f08ec22c`
- `golden-hashes.json`: `c50954fcd4881b3c1ca70e6865cd2db4c3a8d2e886c217a2335c5d42db3bdb6f`
- `manifest.json`: `613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d`
- `syn-coverage-a.json`: `52b67124fef5c88d0ee732bf5d021a743325b0dd9f9987ff5ec1219c1dd1fd2f`
- `syn-coverage-b.json`: `782fdba6985795e9096917c467372defa716aa4c8e1095619b7f67129713df60`
- `syn-finding-a.json`: `b23d755e06c869b3d4c1f58e65f979d13e652415db80a9db2a4fe0e5d00c9ad5`
- `syn-finding-b.json`: `7b4c6caf8852489ffa5af5bf91a3b431b9d6a31a460ef4dc5191e0b0dce3a6b2`
- `syn-finding-c.json`: `b166fe91aa3b25acc3164b2e40b4bed6238d728211c2bff4b089e97e91a9dd0d`
- `syn-rec-a.json`: `9847bb817d7c2bdc53682b8a8b97dedf0aed1cca0f40d62d016e51085dbb8277`
- `syn-rec-b.json`: `b11eaa7d51e86ae98bd44467b4f9336ba7b9331e88d753002549310a5f8fbfa9`
- `syn-rec-c.json`: `757e7856d25f2d8a3ff637b842145f857763e83b3aea374aa59796f47c0a06e4`
- `syn-ref-a.json`: `2c520b0ce57fe558d86aea3240ac1486c868a6aa39fa75535c70ce738497eb0b`
- `syn-ref-b.json`: `cfaa70a4318a3f81304849ffe4d3ae72d28af033fc6a526c477fe3ccfa11d59b`
- `syn-score.json`: `85d82c2d1d16b5181f195a72d165448673a1a500d6dc40b7625399ae1b481e10`
- `syn-status.json`: `9c4bc6272df05da4cf310c83101bf6803b686d8123343e4417d63568db32d8ef`
