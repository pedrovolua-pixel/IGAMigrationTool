# AP-KV: supported-platform signature proof preparation

Status: **SUPPORTED LINUX ARTIFACT AND BOUNDED SDK DISPATCH VERIFIED; production/runtime acceptance open**
Date: 2026-10-03 UTC
Authority: [approved cycle03 AP-KV/AP-KV01](../../plans/active/https-production-local-cycle03.md), following [actual cycle02 host graph evidence](https-key-provider-resolved-graph-evidence.md). This prepares a diagnostic CLI tool. It does not promote package pins/locks, install dependencies, approve a platform execution, grant access or complete KEY runtime acceptance.

## Observed failure and platform diagnosis

The retained macOS run selected exactly37 packages with Blob provider1.5.4 and Keys provider1.6.4. Its normal `dotnet nuget verify --all --verbosity normal` exit was1:36 packages verified, and System.Security.Cryptography.ProtectedData4.5.0 failed. Audited restore exit0 did not prove integrity. No identical verifier retry was performed in this cycle.

Observed platform: macOS15.3.1 build24D70, arm64/osx-arm64; SDK10.0.401 commit `e34a38d2ae`, runtime10.0.12 commit `95017c711e`. Normal logs selected SDK `codesignctl.pem` and `timestampctl.pem` fallback bundles. Read-only SHA256 bindings are:

| Retained item | SHA256 |
|---|---|
| ProtectedData4.5.0 raw signed archive | `67e5f5676944acb2fb627b768c5b3392eebf220ae780edd5d5b49f6530621487` |
| Archive `.signature.p7s` bytes | `a0a99690a8436df1a00ceb962026c85e3c54a3cdf945959728b59469414517ad` |
| Full selected-signatures.log | `e81d3777bd081eddb1878536781622c61af57946dd609eb70d3f0599b7560c57` |
| SDK codesignctl.pem,307 certificates | `aab671f52e5229906b2100727370007ef4b6d2e360b23f2cf12a6d87773be611` |
| SDK timestampctl.pem,327 certificates | `5ccb03367b52f047099f07e1653160e8578a83711cb18560c979febb65f8cb5d` |

The following timestamps and certificate validity values are **the retained log's local display**, not a new UTC/RFC3161 extraction. Both signing certificates expired in2021; both timestamp leaves remain within their displayed validity in2026.

| Signature | Signing certificate SHA256 | Timestamp display | Timestamp leaf / SHA256 |
|---|---|---|---|
| Author: Microsoft Corporation; signing validity2018-02-25→2021-01-27 | `3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE` | 2018-05-15 4:37:19 PM | Symantec SHA256 TimeStamping Signer G3; `C474CE76007D02394E0DA5E4DE7C14C680F9E282013CFEF653EF5DB71FDF61F8`; validity2017-12-22→2029-03-22 |
| Repository: NuGet.org Repository by Microsoft; signing validity2018-04-09→2021-04-14 | `0E5F38F57DC1BCC806D8494F4F90FBCEDD988B46760709CBEEC6F4219AA6157D` | 2018-10-06 2:07:24 PM | Symantec SHA256 TimeStamping Signer G2; `CF7AC17AD047ECD5FDC36822031B12D4EF078B6F2B4C5E6BA41F8FF2CF4BAD67`; validity2017-01-01→2028-04-01 |

Both signing certificates were issued by DigiCert SHA2 Assured ID Code Signing CA; both timestamp leaves by **Symantec SHA256 TimeStamping CA**. The run reported four errors: NU3037 expired author and repository signing validity, and NU3028 `ExplicitDistrust` for each timestamp chain. It reported zero warnings and no NU3042 missing-root warning. Normal output does not reveal the full built chain/root or independently establish host trust settings.

Microsoft's [verification platform guidance](https://learn.microsoft.com/en-us/dotnet/core/tools/nuget-signed-package-verification) distinguishes implicit restore verification from explicit `verify`, which always attempts verification. Windows supports signature verification; Linux support starts at SDK6.0.400. Its current macOS guidance describes unsupported functionality and recommends leaving implicit verification disabled. This project will use a separately approved Windows/Linux proof instead of changing macOS trust or verification settings.

Official [NuGet/Home#11986](https://github.com/NuGet/Home/issues/11986), open when checked2026-10-03, describes macOS contextual distrust overriding explicit timestamp root trust for **VeriSign Universal Root Certification Authority → Symantec SHA256 TimeStamping CA → Signer G2/G3**. The retained leaf/issuer names and error strongly match this documented limitation. That is an **inference**, not a newly observed root chain. A macOS explicit verification command remains possible;36 prior successes do not resolve this failing chain. No supported safe local remedy was established within this packet's constraints.

An expired signing certificate can remain valid through a trusted timestamp; [NU3037 guidance](https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu3037) explains this distinction. The simultaneous timestamp distrust matters. This evidence establishes neither package tampering nor signature acceptance. Offline revocation, ignored errors, custom trust, publisher re-signing or dependency upgrades are not remedies authorized here. Exact detailed chain evidence on a supported platform remains missing.

## Immutable metadata and tool boundary

The [committed manifest](../../tests/infrastructure/HttpsKeyProviders/resolved-37-archives.json) preserves the actual37 selected archives as metadata, **not a production lock**. It contains closed schemaVersion1, sdkVersion10.0.401, graphKind `experimental-host-graph-not-production-lock`, sourceCommit `b186d121419236e15e7fa184a22c6bac4d2db939`, experimentalLockSHA256 `2d726d936d5cb3b5d647ef77a3f62d56e16fb0a70b49738f483ab3c7b5eebe9f`, and exactly37 id/version/fileName/archiveSHA256 records. Manifest rawSHA256, pinned in the script: `8ed2ffab1478d381cf310b62e7c6b198bf47595e8e7100c16d2738fc6a4fee42`. Every entry matches the retained actual graph and prior document inventory; it contains no packages or generated production lock.

[verify-signatures.py](../../tests/infrastructure/HttpsKeyProviders/verify-signatures.py) uses only Python's standard library. Inputs are `--archives` (flat, preobtained exact inventory), `--dotnet` (trusted absolute existing native dotnet executable) and `--evidence` (new nonexistent directory whose parent exists). Linux/Windows are permitted; every other platform, including macOS, is denied **before any subprocess**. The CLI has no platform override, fake verification, alternate manifest or bypass option.

Preflight denies malformed, missing, duplicate or unknown JSON fields, manifest hash changes, wrong bindings/count/package names/digests, missing/extra/nested/symlink archives and raw digest mismatch. It rejects trust/signature/revocation/roll-forward override environment variables even if empty; values are never printed or silently cleared. Runtime injection variables in the declared guard set also deny. This guard is not an attestation of all host configuration or the supplied executable; separately approved host/SDK provenance and normal system trust remain necessary.

The tool creates a new evidence snapshot, copies/rehashes all archives, writes explicit `<configuration />` NuGet configuration and global.json selecting10.0.401 with rollForward=disable/allowPrerelease=false. It invokes `dotnet --version` in that directory and aborts unless exit0 and exact10.0.401. All process arguments use arrays with shell=False. Per archive it invokes:

```text
<absolute-dotnet> nuget verify <absolute-snapshot-archive> --all --verbosity detailed --configfile <absolute-explicit-config>
```

The [official CLI reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify) documents these options and detailed chain output. Coordinator separately checked pinned `nuget verify --help`, exit0. That benign metadata command and normal repository regressions are distinct from package signature proof; no actual supported-platform verification ran here.

Each SDK/package subprocess records exact argv, start/finish UTC, direct exitCode or TIMEOUT/LAUNCH_FAILED, timeout guard, and raw stdout/stderr files with byte count/SHA256. Partial receipts persist after each command. Normal package verification failures, timeouts and launch failures continue through37 attempts; changed snapshot/config preflight denies that attempt. Final validation rehashes snapshot, original inventory and configuration. Aggregate statusPASS requires exactly37 actual exits0 and unchanged hashes/configuration; any failure returns1. No successful restore, mock result or warning suppression substitutes for that condition. The180-second subprocess bound is a tool guard, not production deadline policy.

The runner performs no downloads, restore, builds, key calls, installation, credential use, trust-store modification or remote execution. Normal NuGet verification may contact public certificate/revocation endpoints; this is **not offline verification**. Preserve the approved initialized SDK environment; this packet does not authorize first-run certificate setup, trust repair or host provisioning. Snapshot/rehash guards detect byte changes around commands; they are not an adversarial filesystem attestation or protection from an actor controlling the approved host/executable.

## Executed local checks and exact limits

```text
PYTHONDONTWRITEBYTECODE=1 python3 tests/infrastructure/HttpsKeyProviders/test_verify_signatures.py
```

**18 mocked safety tests PASS.** They cover manifest pin/closed bindings, hostile metadata and inventory, archive/directory symlinks, macOS CLI zero subprocesses, forbidden environment presence/value non-disclosure, native absolute executable/output destination, wrong SDK/nonzero/timeout/launch, exact argv/config/SDK pin, raw log hash fidelity, all37 attempts despite one package failure, copy races and post-verification archive/config mutations. Synthetic test records and subprocess injection exist only in the Python test interface; their simulated PASS is not evidence of any package signature. The Windows branch is mocked on macOS; no Windows behavior or root store has been exercised.

Manifest cross-check against retained JSON and prior37-row document, Python syntax, UTF-8/local links, whitespace and scoped Gitleaks checks are recorded in the immutable worker handoff. No production source, migration, role, package reference, dependency lock, workflow or Azure template changed. Coordinator owns canonical status, independent review and private-board publication.

## Next separately approved proof packet

Choose an existing approved Windows or supported Linux build host and an approved initialized SDK10.0.401. Bind platform/version/architecture, SDK provenance and actual normal trust source; prepare the exact preobtained37 archives independently and confirm this manifest hash. This document grants no execution or acquisition authority. After fresh approval, the prepared command shape is:

```text
python3 tests/infrastructure/HttpsKeyProviders/verify-signatures.py --archives <exact-flat-directory> --dotnet <trusted-absolute-native-dotnet> --evidence <new-evidence-directory>
```

Retain manifest/source bindings, every direct process/log receipt, detailed chains and final37 hashes. Any supported-platform failure remains blocked; preserve evidence without automatically changing trust, versions or revocation mode. CLI signature success would only satisfy this artifact prerequisite. The actual platform restore/audit/build, dependency integration review and provider guard/SDK dispatch harness require their own authorized evidence. No full KEY, production readiness, Azure binding/grant, lifecycle/witness/deadline/outage or portal acceptance is implied.

## Supported Linux artifact review complete — 2026-10-03 UTC

Owner supplied the original ZIP and extracted diagnostic folder for run37145824111. Original ZIP85352bytes/SHA256fbf6baee29e156edcfac7e4f0ca4454bc188ae5fedd88cd5f071b65687e3b395 matches GitHub artifact11281169753. Coordinator and non-author independently checked all85 safe allowlisted ZIP entries, corresponding folder bytes,37 original archive-hash/official-download receipts,37 direct normal verifier exits0,78 raw log byte/hash bindings, SDK10.0.401/runtime10.0.12/Linux-x64, explicit empty NuGet config/global pin,7 approved source hashes/exact run/merge/head/base and actual Setup runner2.337.0/Ubuntu24.04.5/image20260927.320.1. All37 logs select the normal SDK code-signing/timestamp fallback bundles.

ProtectedData4.5.0 now verifies normally on Linux with the unchanged original archiveSHA25667e5f5676944acb2fb627b768c5b3392eebf220ae780edd5d5b49f6530621487. Its detailed timestamps build through Symantec to the observed VeriSign Universal Root SHA2562399561127A57125DE8CEFEA610DDF2FA078B5C8067F4E828290BFB860E84B3C. Historical macOS failure remains recorded; no package/trust/verification/revocation change or tampering inference. Original archives/trust-bundle contents are intentionally absent from diagnostics; this is authenticated runner receipt evidence, not a local repeat of signature verification.

[Manual artifact review receipt](evidence/https-key-provider-linux-proof-manual-20261003.json) binds the completed proof. Finder added .DS_Store after initial folder inspection; it is excluded from the authenticated ZIP/member review. Initial shell here-document inspection failed under disk pressure, then argument-based checks succeeded; no user files/evidence were deleted.

This closes only the supported Linux artifact prerequisite. Owner “continue” starts the [bounded local SDK dispatch diagnostic](../../plans/active/https-key-provider-dispatch-diagnostic.md), preserving the experimental37 graph and no production dependency promotion. No full KEY, provider guard/lifecycle/witness/inventory policy, Azure roles/resources/spending/session, admission or release follows. The six [human tasks](https-production-test-packet.md#human-tasks-and-completion-conditions) remain open; the signature/manual-artifact substep is complete, and actual guard/SDK/inventory/lifecycle inputs remain. Prepared private-board delta changes that substep only; BFF/HTTPS snapshot4346e0f remains stale under the separate rejected credential/network publisher action, with no bypass and unrelated Cycle14 changes preserved.

## Bounded SDK diagnostic verified — 2026-10-03 UTC

[Actual Microsoft provider diagnostic](https-key-provider-dispatch-evidence.md) now independently verifies27 cases on SDK10.0.401/runtime10.0.12 with37 archive/74 asset-entry checks, pinned scratch build0 warnings/errors and meaningful formatting controls. Earlier statements about unexecuted SDK/source-only feasibility describe their historical checkpoint. This closes only the [DISP01–08 plan](../../plans/active/https-key-provider-dispatch-diagnostic.md) mechanics; production graph/guard/parser/inventory/witness/lifecycle,403/real transport/diagnostics/composition/fullKEY/live and release remain open. No Azure/dependency promotion/policy/grant change; six human tasks and separately blocked private publisher persist.
