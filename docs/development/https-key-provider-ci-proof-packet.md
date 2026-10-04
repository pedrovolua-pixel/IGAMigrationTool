# Exact key-provider artifact CI proof packet

Status: **PREPARED LOCALLY; PUBLIC EXPORT AND REMOTE RUN NOT APPROVED OR EXECUTED**
Date: 2026-10-03 UTC
Purpose: obtain supported Linux signature evidence for the unchanged experimental37-archive inventory. This is an artifact prerequisite, not dependency integration, provider behavior or production approval.

## Minimal source disclosure

Prepare a separate branch from the freshly confirmed public default base `31c61eaf17a2abbc3b4b87a674b057d0b2b75651` by copying only these seven files. Reconfirm the base/policies before publication. Do not export unpublished implementation history, update another branch or treat this document as publication approval. Each concrete candidate requires independent review, exact file/byte inventory, secret checks and owner approval before push/PR/run. No public branch or PR is created by local preparation.

| File | Purpose |
|---|---|
| [Signature workflow](../../.github/workflows/https-key-provider-signatures.yml) | One Ubuntu24.04 diagnostic job |
| [Exact37 manifest](../../tests/infrastructure/HttpsKeyProviders/resolved-37-archives.json) | Existing byte-identical package/version/rawSHA256 metadata |
| [Signature verifier](../../tests/infrastructure/HttpsKeyProviders/verify-signatures.py) | Existing normal per-archive verifier and receipts |
| [Verifier safety tests](../../tests/infrastructure/HttpsKeyProviders/test_verify_signatures.py) | Existing mocked subprocess safety checks |
| [Archive downloader](../../tests/infrastructure/HttpsKeyProviders/download-archives.py) | Guarded official NuGet acquisition |
| [Downloader safety tests](../../tests/infrastructure/HttpsKeyProviders/test_download_archives.py) | Mock HTTP and output-isolation checks |
| This proof document | Export, execution and evidence boundaries |

The manifest's SHA256 is `8ed2ffab1478d381cf310b62e7c6b198bf47595e8e7100c16d2738fc6a4fee42`, pinned by the unchanged verifier. It preserves Blob provider1.5.4, Keys provider1.6.4 and all37 original signed archive hashes. Its sourceCommit/experimentalLockSHA256 describe the graph observation; they do not publish that source, install packages or establish a production lock. Existing provider/manifest/test files remain byte-identical.

## Why a supported target is necessary

The retained macOS verification attempted37 archives:36 succeeded and ProtectedData4.5.0 failed with expired signing certificates and `ExplicitDistrust` on both timestamp chains. This establishes neither tampering nor acceptance. Microsoft documents [platform support and normal trust selection](https://learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification); explicit verification attempts differ from implicit restore behavior. Official [NuGet issue11986](https://github.com/NuGet/Home/issues/11986) describes the matching Symantec G2/G3 macOS timestamp distrust limitation. No certificate/trust change, verification bypass or package upgrade is selected.

## Workflow and platform boundary

The workflow uses exact-path `pull_request` and `workflow_dispatch`, with **contents:read** only. It never uses `pull_request_target`. Manual dispatch requires the workflow on the default branch; the prepared new branch alone does not provide that capability. See [GitHub trigger documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch). The confirmed default base had no workflows, so this minimal branch adds only this job. Re-review if the base or other trigger configuration changes.

Ubuntu24.04 is the prepared target. Action pins are:

- checkout v4.2.2: `11bd71901bbe5b1630ceea73d27597364c9af683`, persist-credentials:false.
- setup-dotnet v4.3.1: `67a3573c9a986a3f9c594539f4ab511d57bb3ce9`, explicit SDK10.0.401, cache:false; [immutable action documentation](https://github.com/actions/setup-dotnet/tree/67a3573c9a986a3f9c594539f4ab511d57bb3ce9) supports exact versions.
- upload-artifact v7.0.0: `bbbca2ddaa5d8feaa63e36b76fdaad77386f024f`; its [immutable documentation](https://github.com/actions/upload-artifact/blob/bbbca2ddaa5d8feaa63e36b76fdaad77386f024f/README.md) requires Node24 and runner>=2.327.1.

A later authorized run may set up the exact SDK on its ephemeral runner; none is installed or executed by this local packet. `DOTNET_GENERATE_ASPNET_CERTIFICATE=false` prevents first-run development certificate generation under [Microsoft's documented environment contract](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_generate_aspnet_certificate). It does not weaken package trust. Normal TLS/proxy/revocation settings are preserved; forbidden trust/signature/revocation/runtime-injection overrides cause denial, with no silent clearing. No restore/build, provider harness, Azure/OIDC/custom-secret access or trust commands exist. GitHub checkout/artifact service tokens are platform internals; execution is not described as token-free.

The150-minute job guard permits the existing37 sequential180-second verifier guards plus acquisition/setup time. These are diagnostic bounds, not service deadlines, production policy or a free-compute/spending guarantee. SDK pin global.json is generated inside temporary evidence directories, so no repository SDK/config/dependency change is needed.

## Official acquisition contract

Downloader inputs are new nonexistent `--archives` and `--receipts` directories with existing parents. The closed/hash-pinned manifest and existing environment override guard are validated **before HTTP**; forbidden values are never printed or cleared. Output aliases/symlink ancestry, existing directories/files and destination collisions deny. Use canonical directory paths.

Only `https://api.nuget.org/v3/index.json` is fetched for resource discovery. Require one PackageBaseAddress/3.0.0 resource with exact `https://api.nuget.org/v3-flatcontainer/` base. Construct lowercased pinned IDs/versions/file names according to [Microsoft's package content API](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource). There is no endpoint/mirror/auth/version override. Default verified HTTPS and proxy handlers remain in use; redirects, non200/final-URL substitution and encoded bodies deny.

Stream bytes with declared-length, empty/truncation and size checks: index1MiB, archive64MiB, urllib socket timeout30seconds. The network timeout is an I/O guard, not a guaranteed total wall-clock request deadline; the overall job remains bounded. Hash before atomically publishing a final file using same-filesystem hard links that refuse overwrite. A staging file is never executed or extracted. Stop at first failure; retain metadata-only receipts, including retained byte counts/digests. A complete success has exactly37 verified files in a flat directory; incomplete acquisition cannot launch the subsequent verifier. No retry, fallback, automatic upgrade or bypass occurs.

The unchanged verifier rejects unsupported platforms, unsafe inventory and hash changes, selects exact SDK10.0.401, and uses normal `dotnet nuget verify --all --verbosity detailed --configfile` for each archive. See [Microsoft CLI reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify). Its direct exit, stdout/stderr byte-count/hash and timeout/launch receipts are preserved. A verification failure attempts the remaining packages and returns failure; no mocked result or restore success counts as signature evidence.

## Evidence and acceptance

Provenance includes only public imageOS/version, runner OS/architecture, release, event/run/attempt, checkout/event SHA, PR head/base where applicable, the seven source file hashes, manifest hash, SDK --info command/raw logs/exit, and read-only presence/SHA256 of normal SDK code-signing/timestamp bundles and the Linux system code-signing candidate. Bundle contents are not uploaded. Detailed verifier logs determine the actually selected trust path; merely finding a candidate does not establish selection or trust.

Actual runner version must be retained and independently reviewed from the **Setup job log**, confirming>=2.327.1. An optional RUNNER_VERSION value is recorded as optional/unobserved and is not proof. Node24 action success is not an exact-version observation. Machine evidence can report `SIGNATURES_VERIFIED_PENDING_PLATFORM_REVIEW`; it cannot alone complete platform acceptance. Repository diagnostic artifact retention uses its existing default with no override; effective days remain unobserved here. This creates no product retention policy.

The `always()` upload uses eleven explicit paths/patterns under the new temporary proof directory: provenance/machine evidence, fixed SDK/config pins, downloader/verifier receipts, SDK --info logs, and verifier stdout/stderr logs. It excludes both package directories, partial download staging, repository/home trees, environment dumps, raw certificates and customer material. Missing uploads fail; partial failure artifacts remain diagnostic. Hard cancellation/runner loss can prevent upload and leaves proof incomplete.

Acceptance requires all37 original hashes, all37 direct normal verifier exits0, complete checked-out source/SDK/platform/log/config bindings and unchanged inputs. The machine validator rechecks these receipts, package inventories, source/config and log hashes. Human review additionally validates actual Setup job version and selected normal trust chains. Failed/missing/incomplete evidence blocks dependent integration. Successful artifact verification does not establish provider guard dispatch, restart/outage/ETag/deletion/restore behavior or production readiness.

## Local verification and next action

| ID | Local check | Result |
|---|---|---|
| AP-CI01 |16 mocked HTTP tests: exact37 success, manifest-before-network, resources/endpoints/redirect/status/length/hash/truncation/limits | PASS; no actual HTTP |
| AP-CI02 | Same suite: output/symlink/collision safety, partial bytes/hash receipts, no extraction/execution;18 existing mocked verifier tests | PASS; no actual SDK/signatures |
| AP-CI03 | Ruby/Psych YAML parse, closed event/permission/action/upload controls, five bash syntax checks, two inline Python AST checks, Python source syntax | PASS; no workflow run |
| AP-CI04 | UTF-8/local links/whitespace, scoped Gitleaks and unchanged three-file bindings; coordinator prepares/reviews seven-file export separately | Checks in worker handoff; public export/run NOT EXECUTED |

No actionlint/shellcheck is installed; syntax/static checks do not replace actual GitHub workflow execution. The packet contains no production source, migration, dependency lock or Azure configuration change. Required next action is review of the immutable seven-file public-base candidate, followed by exact owner export/run approval. If approved, retain downloaded public diagnostic artifact hashes/IDs and raw job logs, record actual platform/retention observations and independently compare all37 receipts before any dependent SDK integration.
