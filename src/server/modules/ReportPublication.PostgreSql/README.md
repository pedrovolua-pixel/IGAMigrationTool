# Native publication PostgreSQL fixture adapter

Status: strict metadata reader and original validity lease checkpoint only. SQL/session/store implementation, migration execution, host activation and persisted-case success remain unproved.

RR-P05A owns this separate assembly and `migrations/report-publication/001-native-fixture.sql` under coordinator packet `a3e7c5d79f9dba694eebd007d1a5c0a0a78fe5c6`. The accepted [fixture contract](../../../../docs/development/native-publication-postgresql-fixture-contract.md) at `fdefd8e79fa108bb099e5922e9a49b87a6a1a50e` and [implementation plan](../../../../docs/development/native-publication-v1-implementation-plan.md) govern every dependent implementation. RR-P05B independently freezes the 18 persisted cases and 35 exact SQL signatures/grants first.

The assembly references the existing ReportPublication core and exactly Npgsql10.0.3. It will expose only the four reviewed public constructor surfaces, with test-only internal friendship to ReportPublication.Tests for fictional authority/source/reference/clock and read-only reconciliation adapters. It adds no ordinary host registration, customer/provider access, HTTP/MCP route, PDF renderer, central audit claim or native schema change.

The reviewed fixture requires immutable original bytes and relational metadata, original canonical command/request binding, minimized preload then full category/17field admission, stable actor-first session locks, same-xid finalization, proof of actual commitment before retained READ ONLY methods, server-owned audit reservations, branch-specific historical replay and bounded original UTC/monotonic cancellation. Deferred constraints establish atomic linkage rather than physical-COMMIT clock freshness. Unknown outcomes reconcile through a separate fresh READ ONLY branch with no source/blob/business/context/new outcome writes.

Migration is explicit and additive; the module never applies it during startup or constructs roles/passwords. The independent verifier owns fresh database receipts and actual restricted-login PostgreSQL18.4 execution on coordinator loopback56284. Preserve old56283 and other55433 clusters. Normal close/discard or explicitly owned PID/backend_start cleanup only; no broad backend signal role or other fixture termination.

Coordinator owns solution/CI/shared friendship/canonical status and private site. Original31goldens/core8881c1/157codec+58scripted checks remain unchanged; actual persisted18 and real pilot gate acceptance remain separate evidence.

## First implementation checkpoint

The internal control reader owns an input copy, rejects malformed UTF-8, non-ASCII control bytes, duplicate decoded keys, numbers, excess depth/size, unknown shapes and noncanonical metadata. Manifest, publication/read receipt and audit records are checked against the frozen core canonical codec. Protected fixture source metadata is separate from minimized admission; this reader neither grants access nor constructs an owning source. The original validity lease retains its admission UTC and monotonic origin, only narrows the ceiling, links caller/invalidation/deadline cancellation, checks around waits and observes ignored late faults without returning a late result. These are prerequisites rather than a database fence.

This checkpoint follows independently frozen P05B expectations6232bbf and refinement70a1dc, coordinator governing refinements2fa7772/cab65d1 and actual-Migration setup3c02e54. The only core import is coordinator-owned fixed parameterless integrity marker2a5a293; core source construction and original golden bytes are unchanged. Adapter-only test friendship is ReportPublication.Tests.

Executed locally: scoped .NET format and Release build with pinned .NET10.0.401/Npgsql10.0.3, zero warnings/errors. Independent metadata/lease tests and all18 persisted cases remain the verifier's next evidence; compilation alone does not satisfy them. No database was connected or modified.
