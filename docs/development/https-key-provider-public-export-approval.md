# Exact public signature-proof export approval

Status: OWNER APPROVED — publication and Linux execution authorized; results pending
Date: 2026-10-03 UTC
Authority: [cycle04](../../plans/active/https-production-local-cycle04.md); [self-contained proof contract](https-key-provider-ci-proof-packet.md)

## Exact action requested

Publish only local commit `4fadf7ced8b6c6b978e326e2fae6d925001fdb26` on a new `codex/https-cycle04-public-proof` branch of the public `pedrovolua-pixel/IGAMigrationTool` repository. Its sole parent is freshly confirmed public default `31c61eaf17a2abbc3b4b87a674b057d0b2b75651`. Open a separate draft PR targeting main, attach it to this task, and let its exact Ubuntu24.04 signature-only job run. Keep the existing pilot PR/branches and default branch unchanged. No merge or release is requested.

Repository control rule8 and the cycle03/cycle04 source-disclosure boundary require explicit approval of this new public packet. Earlier local implementation approval did not authorize this disclosure. This request includes the seven new public files, ordinary Git commit metadata, draft PR description and public diagnostic run logs/artifacts under existing repository retention. No coordinator/worker implementation ancestry, production audit proposal, private intake, credentials, customer evidence or Azure resource/grant is exported. Exact default/public policy drift requires re-review.

The proposed PR is “Verify exact shared-key package signatures on Linux”. Its description states artifact-prerequisite scope, fixed37 archive/SDK bindings, locally executed mocked/static checks, and that actual signature/platform proof is pending. PR creation triggers the sole added workflow; workflow_dispatch on this branch alone cannot run before default-branch presence. No default merge is authorized to enable it.

## Seven-file byte inventory

| File | Raw SHA256 |
|---|---|
| [https-key-provider-signatures.yml](../../.github/workflows/https-key-provider-signatures.yml) | `5f784c86d30013bcfb9a0e6675371fd584084644d738bb448a64f9859cf6f412` |
| [https-key-provider-ci-proof-packet.md](../../docs/development/https-key-provider-ci-proof-packet.md) | `89f1b27e0ca92144f0112e4693e43fe27f9f0c81053ef26e5b55baaf2c9256ec` |
| [download-archives.py](../../tests/infrastructure/HttpsKeyProviders/download-archives.py) | `c1b7d61452946d7b662e973b67535d7d75090a0f30d570fc1c4785840251fff6` |
| [resolved-37-archives.json](../../tests/infrastructure/HttpsKeyProviders/resolved-37-archives.json) | `8ed2ffab1478d381cf310b62e7c6b198bf47595e8e7100c16d2738fc6a4fee42` |
| [test_download_archives.py](../../tests/infrastructure/HttpsKeyProviders/test_download_archives.py) | `aea7ebddf8215148f5139f8ce9ad9d6638ba89c575148aa15104d2edaa9d4fe7` |
| [test_verify_signatures.py](../../tests/infrastructure/HttpsKeyProviders/test_verify_signatures.py) | `5aaee4517f702b349a3ffbf98909c8e70fe132feb9894f17fadb26459bbc9d37` |
| [verify-signatures.py](../../tests/infrastructure/HttpsKeyProviders/verify-signatures.py) | `65d8256ed7f7d4d127838c89565efeeb6c2c6c864860108e64517e40db4b5af9` |

Exact binary-capable diff SHA256: `ab907096ada28aeb7866c445a956b6e5dc5061cae7f87bf4372e00ce2a7501ba`. Existing manifest/verifier/verifier tests are byte-identical to previously reviewed preparation. This separate branch includes none of the unpublished coordinator history. Public base has no other workflows, so the reviewed closure adds one diagnostic job.

## Executed review and limits

Author and independent reviewer executed16 downloader+18 verifier mocked safety tests; reviewer additionally executed10 independently constructed hostile HTTP/output cases. Coordinator repeated the34 supplied tests on integrated source, parsed YAML with duplicate-key/alias checks, checked five bash bodies/two inline Python programs and four Python source files, and verified permissions/action pins/SDK/cache/upload controls. Independent export review confirmed seven paths/hashes, sole public parent, no coordinator ancestry, clean checkout, six relative proof links, whitespace and exact-export Gitleaks. No real HTTP acquisition, SDK setup, supported-platform signature or workflow run has occurred.

If approved, use normal TLS/proxy/signature/revocation trust and unchanged37 archive hashes/SDK10.0.401. Preserve direct exits/raw-log hashes and partial failures; inspect actual Setup job runner version and selected normal trust chains, obtain artifacts and compare receipts independently. Do not change trust, silently retry/upgrade, promote dependencies or run the provider harness to make a failure pass. Standard Ubuntu runner only; no larger runner, account/billing changes, Azure spending/session, cloud permissions or portal activation. Artifact retention uses existing repository default; effective days remain unobserved until protected account/run evidence supplies them. Missing or failed platform evidence blocks dependent key-provider integration.

This approval closes only the public export/run prerequisite. The separately reviewed [production audit proposal](https-production-audit-receipt-lifecycle-proposal.md) and [ARL tests](https-production-audit-receipt-lifecycle-test-packet.md) still require their precise production decisions/inputs. The six linked [human tasks](https-production-test-packet.md#human-tasks-and-completion-conditions), live Azure gates and BFF status-site publisher remain open.

## Exact owner authorization — 2026-10-03 UTC

Owner replied “Approved” after the exact seven-file packet publication, separate draft PR and Linux verification request. Candidate4fadf7ced8b6c6b978e326e2fae6d925001fdb26 and all seven hashes were rechecked; public default/main31c61ea and existing pilot branchb065 remain unchanged; destination branch absent and repository still public. This authorizes only the stated export/run and evidence review. Production audit decisions and Azure activation remain open.
