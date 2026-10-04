# Microsoft key-provider dispatch diagnostic

Status: **author bounded local mechanics verified; independent acceptance is tracked in the linked canonical plan/evidence**. This test-only packet implements [DISP01–08](../../../plans/active/https-key-provider-dispatch-diagnostic.md) for the accepted [preferred local key strategy](../../../docs/development/https-production-key-contract-proposal.md). It does not configure the BFF or promote a production dependency graph.

## Inputs and execution

`run-experimental.py` consumes the retained actual NuGet assets/lock and expanded cache. It pins their hashes, the shared verifier source, [manifest37](../HttpsKeyProviders/resolved-37-archives.json), SDK10.0.401 and installed runtime10.0.12. Before any subprocess it checks all37 raw archives and all74 selected compile/runtime DLL entries against raw ZIP bytes. References come from `net10.0` in that exact assets file; there is no TFM/version resolver. Compile and runtime assets remain distinct, including ProtectedData's reference/runtime assemblies.

The runner creates a new scratch directory under `/tmp`, explicit assembly references and `Microsoft.AspNetCore.App` framework reference. Its empty-source restore has no PackageReference or new package graph. It binds and copies both `Program.cs` and `ResolverPipelineCases.cs`, builds Release with warnings as errors, verifies whole-project formatting with diagnostic output, copies verified runtime DLLs and executes the harness. Every subprocess command, exit/timeout, raw log hash and input/DLL hash is retained in `receipt.json`. A framework-only restore supplies no fresh package advisory or signature evidence; the separately reviewed [Linux signature proof](../../../plans/active/https-key-provider-linux-proof-run.md) remains the artifact prerequisite.

From this repository root, with those independently retained inputs:

```sh
python3 -B tests/infrastructure/HttpsKeyProviderDispatch/run-experimental.py \
  --assets /tmp/iga-key-resolved-copy/src/server/hosts/BffFoundation/obj/project.assets.json \
  --lock /tmp/iga-key-resolved-copy/src/server/hosts/BffFoundation/packages.lock.json \
  --packages /tmp/iga-key-resolved-packages \
  --dotnet /tmp/iga-dotnet-10.0.401/dotnet \
  --output /tmp/iga-key-provider-dispatch-scratch-new
```

The output must not exist. No downloader, credential source, SDK installation, graph restore fallback or network target is exposed by this runner. Trust/runtime override variables are rejected through the unchanged shared guard. The executable is the supplied existing native SDK, not a wrapper. This is an explicitly authorized local diagnostic, not a portable production bootstrap script.

## Actual dispatch and negative controls

The harness resolves Microsoft's internal repository/encryptor through **public provider registration** and uses the returned decryptor type through framework `ActivatorUtilities`. It does not copy or replace Microsoft implementation code. Blob instance and factory registrations both dispatch the supplied legacy synchronous overrides:

- `DownloadTo(Stream, BlobRequestConditions, StorageTransferOptions, CancellationToken)`.
- `Upload(Stream, BlobHttpHeaders, IDictionary<string,string>, BlobRequestConditions, IProgress<long>, AccessTier?, StorageTransferOptions, CancellationToken)`.

The modern synchronous and asynchronous override-only control reaches the fallback HTTP transport on **both read and write**, with modern/async counters zero. That transport throws before any network request. Guards and positive fixture paths report transport zero; no credential object exists. These observations prove dispatch, not SDK HTTP error/credential/diagnostics behavior.

| Cases | Executed mechanics |
|---|---|
| DISP01 |37 archive /74 selected DLL checks; loaded provider/Blob/Core/framework assembly hashes and runtime reported |
| DISP02/07 | Instance/factory legacy dispatch; modern/async read and write controls each hit trap once |
| DISP03 | Actual empty bytes, malformed XML and missing fixture key pass through the same read content guard and deny; simulated missing404 throws a non404 `GuardDenied` in guarded mode; unvalidated304 denies, previously observed matching fixture304 preserves cached XML |
| DISP04 | Existing write denies absent/empty/wildcard IfMatch and IfNoneMatch creation; missing original fixture key denies; positive append retains old and new elements and SDK response ETag |
| DISP05 |412 then missing reread denies with one attempted upload and zero accepted writes; repeated conflicts propagate after five attempts; terminal503 propagates; unguarded404 control accepts original conditional create |
| DISP06 | Actual encryptor resolves versionless new wrap and stores concrete kid; v2 new wrap plus historical v1 decrypt uses different synthetic RSA material; fixed bindings reject versionless/foreign/unknown historical IDs before resolver and foreign/versionless returned IDs before wrapping |
| DISP08 | Scratch Release build0 warnings/errors, whole-project format and27 actual case checks pass; Python AST/docs/whitespace/secret checks recorded separately |

The implementation binding is Microsoft's [Blob repository](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Blobs/src/AzureBlobXmlRepository.cs), [Blob registration](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Blobs/src/AzureStorageBlobDataProtectionBuilderExtensions.cs), [Key encryptor](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureKeyVaultXmlEncryptor.cs), [Key decryptor](https://github.com/Azure/azure-sdk-for-net/blob/c88fa3e69af1cec1bcdf3302338fcd642ac0baa5/sdk/extensions/Azure.Extensions.AspNetCore.DataProtection.Keys/src/AzureKeyVaultXmlDecryptor.cs). Candidate archives are Blobs1.5.4/Keys1.6.4; DataProtection10.0.12 is supplied by the installed shared framework, not an archive in manifest37. Source/archive metadata correspondence is not a reproducible source-to-binary rebuild.

## Evidence and limits

Author receipts are retained under `/tmp/iga-key-provider-dispatch-author-evidence`; original scratch runs remain alongside it. Run01 failed compilation because the fixture resolver's async return was initially `ValueTask` rather than the actual `Task` interface. Runs02/03 exposed an ETag assertion that used a separately constructed representation; the corrected oracle compares the actual SDK response header ETag. Run04 passed25 cases; adding the write negative control, an empty IfMatch negative and distinct version key material produced27 passing checks. The first formatting checks already passed; no formatter suppression or package/runtime upgrade was used. Initial failures remain recorded, not relabeled successes.

Fixture XML, allowed IDs, RSA size and crypto values are solely synthetic test choices. The Keys provider's observed `RSA-OAEP` dispatch is not approval of a production key type/size/lifetime/rotation choice. The one-version decryptor result verifies historical selection under the fixture, not real Key Vault cache/revocation behavior.

The original dispatch slice did not exercise a real SDK HTTP pipeline or403 guard; the bounded resolver-pipeline slice below extends those local fixture mechanics. No production adapter, credential, Azure, real network/ranged/staged transfer, monotonic inventory/witness, rollback authority, bootstrap operator, role assignment, deadline/outage decision, production diagnostic export, backup/lifecycle/deletion policy, admission, two-replica composition or full KEY-001–018 completion is established. No application dependency/configuration/lock/solution/workflow/schema changed. The coordinator owns canonical status and the existing private status site; this worker did not publish it. Independent review and production/live gates remain separate.

## Resolver pipeline slice

The [approved RP01–08 packet](../../../plans/active/https-key-resolver-pipeline-diagnostic.md) adds `ResolverPipelineCases.cs`, using the actual selected `Azure.Security.KeyVault.Keys4.10.0` KeyResolver and CryptographyClient. Its supplied transport returns deterministic JSON responses and throws on unexpected destinations/methods. It never opens a socket or calls a base send method. Its TokenCredential throws outside the explicitly expected synthetic token context; ordinary challenge-resource verification remains enabled. Every negative reports metadata, crypto, transport and credential counters.

The public `KeyResolver(TokenCredential, CryptographyClientOptions)` constructor shares options with returned crypto clients. A test-only `HttpPipelineSynchronousPolicy` installed through `AddPolicy(..., PerRetry)` throws non-RequestFailed `GuardDenied` on metadata403, before KeyResolver's private parser can construct a force-remote client. Fixed input/resolved/historical guards use only the closed synthetic path/version; no production URI grammar, status policy or exporter is defined here.

Source bindings are the [immutable KeyResolver](https://github.com/Azure/azure-sdk-for-net/blob/6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692/sdk/keyvault/Azure.Security.KeyVault.Keys/src/Cryptography/KeyResolver.cs), [RemoteCryptographyClient](https://github.com/Azure/azure-sdk-for-net/blob/6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692/sdk/keyvault/Azure.Security.KeyVault.Keys/src/Cryptography/RemoteCryptographyClient.cs) and [ChallengeBasedAuthenticationPolicy](https://github.com/Azure/azure-sdk-for-net/blob/6662fe91f2d1f7230d7a8fed2b6db2a17a0bd692/sdk/keyvault/Azure.Security.KeyVault.Shared/src/ChallengeBasedAuthenticationPolicy.cs). The scripted operation paths are exact camel-case `/wrapKey` and `/unwrapKey`. The authentication policy deliberately removes initial content until the supported401 challenge restores it. No request replacement, recovery of private stashed content, cache clearing or challenge verification disable is used. The reviewed scope clarification is commit9211df1; its source-only receipt is `/tmp/iga-key-resolver-pipeline-coordinator-sources/sources-receipt.json`, SHA256 `4ba3f2d73b914a4bf08fc7b290412a0cfc6ef59964fd2a512ea1e20e10a63566`.

In each fresh executable process, all unchallenged metadata200 and guarded403/input/result/historical negatives run **before** auth priming. Their guarded403 credential-zero observation is a cold fixture result, not a universal warm-cache claim. The subsequent first remote wrap receives a scripted401; the allowed synthetic context must have the exact scope `https://invalid/.default`, synthetic tenant, no claims and CAE enabled. The fixture host's `.invalid` suffix satisfies the SDK's ordinary resource-domain check. The token is never printed and has only a fixture expiry. The actual SDK static challenge cache then causes later clients on this same fixed authority to authenticate without another challenge. This cold/warm order is recorded; fixtures do not claim independent authorities or modify SDK caches.

| Cases | Final executed evidence |
|---|---|
| RP01 | Actual sync/async metadata200 return concrete version; Microsoft provider wraps locally with downloaded public RSA metadata and unwraps through the stored version with one scripted crypto request |
| RP02 | Actual unguarded403 returns versionless force-remote client; first sync case: metadata1, crypto attempts2, challenge1, token call1, accepted crypto1; later async cache case: metadata1, crypto1, challenge0, token call1 |
| RP03 | Cold sync/async and actual Microsoft provider refuse metadata403: metadata1, crypto0, credential0, policy denial1 |
| RP04/07 | Invalid fixed input denies before transport, foreign/versionless result denies before crypto, versionless historical kid denies before resolver; unexpected unguarded destination is trapped; per-case counters and sync/async transport dispatch asserted |
| RP05 | Normal options: Activity4, DiagnosticListener8, EventSource4; binding/protected-field presence true; body-field/token/crypto marker presence false under these bounded observations |
| RP06 | Supported diagnostics-off options: observed0/0/0; raw normal observations are rejected16 times by sink; each harmless label/count control accepted1; hostile binding/body controls rejected; protected fields reaching sink false |
| RP08 | Prior27 plus new18 =45 actual case checks;37 archives/74 selected DLL entries verified; Keys DLL loaded version/hash reported; Release build0 warnings/errors and whole scratch formatting pass |

The normal default content logging flag is not deliberately enabled. Supported diagnostics-off options are exercised only in this fixture, with no production telemetry configuration change.

Activity, DiagnosticListener and EventSource observations remain memory-only. Inspectors count callbacks/emissions and record whether fixed binding strings, body-field markers or protected tag/field names were seen. They never output payloads, key IDs, XML, tokens or crypto bytes. The test allowlist sink refuses raw observations and accepts only a closed harmless label/count pair, with hostile and positive controls. This is not a production OpenTelemetry exporter/redaction contract; absence in these bounded observations is not universal leakage proof.

RP author receipts and failed initial runs are retained under `/tmp/iga-key-resolver-pipeline-author-evidence`. Run01 exposed fixture API assumptions: CryptographyClient.KeyId is string and public WrapKey returns WrapResult directly. Runs02–04 rejected the incorrect lowercase operation path; runs05–06 exposed the missing initial request content. Source review confirmed ordinary authentication policy behavior, not a request getter defect or SDK defect. The reviewed synthetic401 fixture corrected that assumption, and run07 passed45 checks. A scratch-only formatting negative control deliberately changed valid C# spacing; `dotnet format whitespace --verify-no-changes --verbosity diagnostic` returned2 with WHITESPACE errors, and the scratch source was restored. No runtime/version/dependency upgrade or suppression was used.

This slice contributes only local prerequisites for KEY-005/011/012/013. No actual Azure403/RBAC, credential/challenge/TCP behavior, production resolver/diagnostic guard coverage, new role/retention/readiness/deadline policy, inventory/witness/lifecycle/backup authority, composed BFF admission or full KEY acceptance follows.
