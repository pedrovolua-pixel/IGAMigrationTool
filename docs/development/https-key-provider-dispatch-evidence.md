# Exact Microsoft key-provider dispatch diagnostic

Status: VERIFIED — bounded local DISP01–08 diagnostic only
Date: 2026-10-03 UTC
Authority: owner “review it ... and continue”, accepted PC-D02 local strategy and [bounded DISP01–08 plan](../../plans/active/https-key-provider-dispatch-diagnostic.md).

## Inputs and isolated build

The [manual Linux artifact review](https-key-provider-linux-run-result.md#supported-linux-artifact-review-complete--2026-10-03-utc) accepted all37 normal signature receipts before SDK execution. The test-only [harness](../../tests/infrastructure/HttpsKeyProviderDispatch/README.md) uses the retained actual NuGet graph, not a new dependency resolver or production lock. Assets SHA256432f6c7796b7302a655eb65d0e3baa1bb1cb7c656c9fc05ec8ff16b7a8874a1f and experimental lock SHA2562d726d936d5cb3b5d647ef77a3f62d56e16fb0a70b49738f483ab3c7b5eebe9f bind those inputs. All37 raw archives and74 selected compile/runtime DLL entries are compared to the approved manifest and raw archive entries before any subprocess; selected bytes are rechecked before loading and archives after execution.

Generated project, explicit references, empty-source framework restore, build outputs and logs remain in owned scratch directories. SDK10.0.401/runtime10.0.12 are pinned. DataProtection10.0.12 comes from the installed shared framework; it is not an archive in manifest37. Blobs1.5.4, Keys1.6.4 and their selected assemblies remain experimental. No application PackageReference, lock, solution, workflow, configuration, schema or IaC changes. Empty-source framework restore supplies no fresh package advisory audit or signature rerun.

## Actual provider mechanics

| Cases | Executed evidence |
|---|---|
| DISP01 |37 original archive and74 compile/runtime DLL entry checks; five critical loaded assembly digests and actual runtime |
| DISP02/07 | Actual Microsoft XmlRepository from public Blob instance/factory registration calls both legacy synchronous overrides; modern/async-only read and write controls each hit the no-network transport once, with modern/async counters zero |
| DISP03 | Actual empty/malformed/missing-fixture content denies through the same test read guard; missing404 becomes a non404 GuardDenied before provider fallback; unvalidated304 denies and previously validated matching fixture304 preserves cached XML |
| DISP04 | Absent, empty or wildcard IfMatch and IfNoneMatch creation deny; missing fixed fixture inventory denies; positive append retains both elements and uses the actual SDK response ETag |
| DISP05 |412 followed by missing reread denies after one attempted upload with zero accepted writes; repeated conflicts propagate after the provider's five attempts; terminal503 propagates; unguarded404 exposes the original conditional-create behavior |
| DISP06 | Actual Microsoft encryptor resolves versionless new wrap and stores concrete kid; different synthetic v1/v2 RSA material supports historical v1 decrypt after v2 wrap; fixed bindings reject invalid historical IDs before resolver and foreign/versionless returned IDs before wrapping |
| DISP08 | Author, independent reviewer and coordinator each executed27 focused cases; author seven and reviewer nine runner negatives PASS. Scratch Release build0 warnings/errors, whole-project format, Python AST, local links, UTF-8, whitespace and scoped secret scan PASS |

Guarded fixture calls report zero fallback transport calls. No credential object exists; printed credential zero is this structural fixture fact, not instrumentation of a live credential. The positive controls and distinct version crypto exercise actual provider calls, not copied Microsoft implementation. The subclass fixtures do not exercise real Azure HTTP errors, ranged downloads, staged uploads, network retries, credential caching or diagnostic export.

## Failures and corrections retained

Run01 failed compilation: the fixture ResolveAsync returned ValueTask instead of the actual Task interface. Runs02/03 failed ETag assertions: a separately constructed representation differed from the SDK response header; the corrected oracle compares the provider's actual response ETag. Run04 passed25 cases; the write control, empty IfMatch case and different synthetic version material produced27 passing checks. Invalid content fixtures were changed to actually pass empty/malformed/missing-key content through validation before the final run. An incidental shasum check failed under the inherited Perl locale; Python performed the same digest check without changing locale or trust. Original failed source/log/exit receipts remain outside Git.

## Formatting diagnostics and executed controls

Independent/coordinator original format commands exited0 with a152-byte diagnostic saying the generated project file could not be formatted because only C# and Visual Basic files are supported. This is preserved. To validate the C# formatting gate, the reviewer used a separate identical scratch copy under normal runtime escalation: empty-source restore0, clean format0, deliberately removed Main indentation rejected with exit2 and Program.cs WHITESPACE diagnostic, restored identical source format0. Those clean control runs had no stderr; the original warning did not reproduce, so its exact cause is not inferred. Original27-case source/log receipts remain unchanged. Independent restore also emitted a macOS CSSM_ModuleLoad diagnostic while exiting0; this supplies no signature, trust or new advisory evidence.

## Remaining work and status

Independent immutable-source review PASS on candidate4ad759727b4700790412cc40ab9e1ce71539ba40; coordinator source8db45d7d9aadc5dd46faa3ef813e96db248a7dfb preserves identical three-file hashes and independently repeats all27 cases. Each run verified37 archive hashes/74 selected entries; reviewer and coordinator each validated12 command raw-log byte/hash receipts and five loaded assembly hashes. [Source-bound execution receipt](evidence/https-key-provider-dispatch-20261003.json) binds exact checks and retained failures. Full KEY-001–018, production guard/parser/limits, trusted inventory/witness and rollback authority, bootstrap/rotation/lifetime/deletion/backup choices, real SDK force-remote403 behavior and transport/diagnostics, composition, two-replica recovery, audit outage and live admission/deployment gates remain open. Synthetic XML/IDs/RSA size/crypto settings are fixtures, not production policy. All six [human tasks](https-production-test-packet.md#human-tasks-and-completion-conditions) remain open.

The prepared BFF/HTTPS private-board delta closes the signature/manual-artifact substep and reports the bounded local diagnostic. Last confirmed BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale: automatic approval review rejected the separate credential/network publisher proxy-bypass. No retry/bypass; unrelated confirmed Cycle14 publication is preserved. No Azure operation, spending/session, grant, production dependency promotion, public source publication, portal admission or release follows from this diagnostic.
