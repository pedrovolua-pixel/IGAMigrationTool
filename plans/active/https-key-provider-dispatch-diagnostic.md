# Exact Microsoft key-provider dispatch diagnostic

Status: PLANNED — Linux artifact prerequisite verified; bounded local execution next
Date: 2026-10-03 UTC
Owner: Coordinator
Baseline: e0747de
Authority: owner “review it ... and continue”, accepted PC-D02 local strategy and [KEY local cases](../../docs/development/https-production-key-contract-proposal.md), [prior experimental graph](../../docs/development/https-key-provider-resolved-graph-evidence.md), [artifact audit](../../docs/development/https-key-provider-artifact-audit.md). [Linux proof](https-key-provider-linux-proof-run.md) now has coordinator and non-author exact37 receipt/ZIP/platform review PASS. No production composition/dependency promotion or new policy.

## Scope

A test-only diagnostic proves selected package APIs and actual framework/provider dispatch against deterministic BlobClient/resolver fixtures. It observes supported interception mechanics, not a production adapter. No real credential, network, Azure, key/tenant access, grants, schema, production key policy or public admission. No current application PackageReference, lock, solution or workflow change. No further public push or remote run.

Writing worker owns only tests/infrastructure/HttpsKeyProviderDispatch/Program.cs, run-experimental.py and README.md in an isolated worktree from this plan. Coordinator owns canonical records, shared configuration and existing private board. Non-author reviewer owns executed independent evidence under /tmp only. Generated experimental project/references/build output remain under a new worker-owned /tmp directory. Derive package compile/runtime asset paths from the retained actual NuGet-generated project.assets.json, never invent a version/TFM resolver or silently restore a new graph. Verify all37 retained archive hashes and selected assembly bytes against their archive entries before loading them. SDK10.0.401/runtime10.0.12; no new trust/verification/revocation override, package restore/source fallback or dependency upgrade. Framework-only scratch project may restore using explicit empty sources; this supplies no fresh package-audit claim. Prior package advisory evidence is dated.

## Traceable cases

| Case | Required diagnostic evidence |
|---|---|
| DISP01 | Exact approved manifest37 archive hashes, actual experimental lock/assets, selected DLL/raw entry hashes, SDK/runtime and harness source bindings |
| DISP02 | Public BlobClient/factory registration resolves actual Microsoft IXmlRepository; both supplied synchronous hidden legacy DownloadTo/Upload signatures are dispatched; modern/async-only overrides do not prove interception; fallback network transport traps all attempts |
| DISP03 | Test-only existing-mode read guard rejects404/empty/malformed/missing fixture inventory before provider fallback and denies unvalidated304; known valid fixture preserves stream/ETag |
| DISP04 | Test-only existing-mode write guard requires concrete non-wildcard IfMatch and denies absent/IfNoneMatch creation before transport; validates fixture preservation without claiming monotonic production inventory |
| DISP05 | Deterministic412 then missing read denies without automatic recreation; repeated conflicts/terminal errors propagate; unguarded control exposes original missing-object create behavior |
| DISP06 | Actual Keys provider/resolver injection dispatches new versionless wrap and stored concrete-version unwrap; supplied fixed synthetic same-key bindings reject foreign/versionless historical/resolved IDs |
| DISP07 | Per-call counters and no-network transport show denied cases perform no base HTTP or credential call; outputs contain only case labels/counts/digests |
| DISP08 | Pinned scratch Release build zero warnings/errors, whole scratch-project formatting, executed focused cases, Python syntax/local links/whitespace/scoped secret check and independent non-author review |

DISP03–06 use closed synthetic fixture bindings and minimum invariants already described in the accepted preferred strategy. No parser/size/retry/deadline/rotation/key-size/lifetime/readiness default becomes production policy. No privileged rollback/witness authority,403 force-remote resolver guard, ranged/staged transport, diagnostic export, full KEY case or outage/acknowledgment proof is inferred from subclass fixtures. Test crypto/fixture parameters are test values only. If API dispatch cannot be safely intercepted, preserve the failure and propose architecture review.

Coordinator closes only signature/platform prerequisite and these explicitly executed diagnostic slices. Production guards, graph/pin review/audit, inventory/witness/lifecycle/backup policy, real SDK transport/diagnostics and composed/live KEY/ingress/identity/audit gates remain unresolved. All six human tasks remain, with signature/manual-artifact substep closed only after canonical receipt update.

## Verification and constraints

Execute only after read-only non-author plan review. Writing worker reports exact commands/exits/source/file/assembly hashes and initial failures/corrections, no secrets/raw protected XML. Independent reviewer repeats affected harness and checks meaningful negative controls/overload dispatch. Coordinator integrates reviewed bounded source and verifies script/docs/source bindings. Unchanged product C#/PG/build/format tests are not repeated; scratch project checks are separately labeled. Do not install Docker/Podman, grant access, bypass blocked browser downloads or retry the separately rejected status publisher.

Initial here-document read failed from disk pressure; Python argument-based evidence checks succeeded, and later free space increased to about1.9GiB. No user files or retained evidence were deleted. Preserve original ZIP/folder; Finder .DS_Store added after initial inventory is excluded from authenticated85-entry ZIP proof and never executed.

Private BFF/HTTPS snapshot4346e0f at2026-10-02T19:50:45.775370+00:00 remains stale under the automatic-review-rejected credential/network publisher proxy-bypass; prepare six-task delta and preserve unrelated Cycle14 publications.
