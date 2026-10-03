# Microsoft key-provider dispatch diagnostic

Status: **author bounded local mechanics verified; independent acceptance is tracked in the linked canonical plan/evidence**. This test-only packet implements [DISP01–08](../../../plans/active/https-key-provider-dispatch-diagnostic.md) for the accepted [preferred local key strategy](../../../docs/development/https-production-key-contract-proposal.md). It does not configure the BFF or promote a production dependency graph.

## Inputs and execution

`run-experimental.py` consumes the retained actual NuGet assets/lock and expanded cache. It pins their hashes, the shared verifier source, [manifest37](../HttpsKeyProviders/resolved-37-archives.json), SDK10.0.401 and installed runtime10.0.12. Before any subprocess it checks all37 raw archives and all74 selected compile/runtime DLL entries against raw ZIP bytes. References come from `net10.0` in that exact assets file; there is no TFM/version resolver. Compile and runtime assets remain distinct, including ProtectedData's reference/runtime assemblies.

The runner creates a new scratch directory under `/tmp`, explicit assembly references and `Microsoft.AspNetCore.App` framework reference. Its empty-source restore has no PackageReference or new package graph. It builds Release with warnings as errors, verifies whole-project formatting, copies verified runtime DLLs and executes the harness. Every subprocess command, exit/timeout, raw log hash and input/DLL hash is retained in `receipt.json`. A framework-only restore supplies no fresh package advisory or signature evidence; the separately reviewed [Linux signature proof](../../../plans/active/https-key-provider-linux-proof-run.md) remains the artifact prerequisite.

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

No production adapter, credential, Azure, real HTTP pipeline/ranged/staged transfer, force-remote403 guard, monotonic inventory/witness, rollback authority, bootstrap operator, role assignment, deadline/outage decision, diagnostic export, backup/lifecycle/deletion policy, admission, two-replica composition or full KEY-001–018 completion is established. No application dependency/configuration/lock/solution/workflow/schema changed. The coordinator owns canonical status and the existing private status site; this worker did not publish it. Independent review and production/live gates remain separate.
