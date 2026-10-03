# Exact Linux signature proof — failed attempt and correction

Status: FAILED BEFORE SIGNATURE EXECUTION; reviewed local correction pending public approval
Date: 2026-10-03 UTC
Authority: [exact export approval](https-key-provider-public-export-approval.md), [execution plan](../../plans/active/https-key-provider-linux-proof-run.md), frozen [proof contract](https-key-provider-ci-proof-packet.md).

## Actual public execution

[Draft PR3](https://github.com/pedrovolua-pixel/IGAMigrationTool/pull/3) has approved head4fadf7ced8b6c6b978e326e2fae6d925001fdb26 and base31c61eaf17a2abbc3b4b87a674b057d0b2b75651, one commit/seven exact paths. The branch was pushed normally and the draft PR attached to the current task. The connector could not create the PR (integration403); the authorized signed-in browser created it. No default/existing pilot branch change or merge occurred.

[Run37139568768](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37139568768), job111251011962, attempt1, checked out synthetic mergeeb6e70a69a2287c3ce40ef72d78fa78adbcb2ea9. Setup logs observe runner2.337.0, Ubuntu24.04.5/image20260927.320.1 and Contents:read. These prove runner setup only. SDK setup, provenance capture, actual HTTP acquisition, all37 signature verifications and machine validation were skipped. The always-upload step failed because no diagnostic receipt files existed; artifact listing is empty. No artifact ZIP, normal selected trust-chain or package acceptance proof exists.

Eighteen verifier mocks passed. Fifteen of16 downloader mocks passed. The sole failure was test_default_opener_preserves_tls_and_proxy_handlers: it assumed HTTPSHandler._context is None, but the runner supplied an explicit SSLContext. This is a test representation defect; it establishes no package-integrity result and no TLS validation failure. The supported-platform signature prerequisite stays NOT VERIFIED; prior macOS36PASS/1FAIL remains unresolved. Retained decoded job log SHA256268fd1096fb0c84d680273156ed215770a5bc7bfcc813e227726527a78a3b443 is a hash of connector-decoded UTF-8 text, not raw downloadable log bytes. Metadata/artifact hashes and independent review are indexed in the [execution receipt](evidence/https-key-provider-linux-proof-run-20261003.json); raw log and readbacks are retained locally.

## Bounded correction

Only [test_download_archives.py](../../tests/infrastructure/HttpsKeyProviders/test_download_archives.py) changes. It captures the real HTTPS handler connection arguments without opening a socket, constructs HTTPSConnection, and requires effective CERT_REQUIRED and hostname validation. It checks default and explicit secure contexts, rejects hostname-disabled/CERT_NONE contexts, and checks normal configured proxy and NoRedirect preservation. The production downloader, verifier, workflow, manifest, SDK and six other public files are unchanged. Python's [SSL documentation](https://docs.python.org/3/library/ssl.html#ssl.create_default_context) specifies the verified default; [CPython's urllib source](https://github.com/python/cpython/blob/3.12/Lib/urllib/request.py) is the implementation reference.

Coordinator17 corrected downloader+18 unchanged verifier mocked tests pass. Python AST and whitespace pass. These remain local no-network checks, not actual Linux signatures. The non-author review receipt supplies separate reproduction, unsafe-context and no-socket evidence. No full C#/PostgreSQL regression rerun applies because application code/configuration/dependencies/schema/IaC remain unchanged.

## Exact next publication requested

A local candidatecefd8354d69e00eb372f8efff457e9b175d3a6eb has sole parent4fadf7ced8b6c6b978e326e2fae6d925001fdb26 and changes only that existing public test file (34 additions/2 deletions). Corrected file SHA2564683991a12264e8b34e96cb8062db5019f17100bf89870c5ac1d25453a707c56; binary diff SHA2561d3db54d8d64d73c163b3f14bda9508ede0f338131a1fb379e01f0712d520fe0. No coordinator ancestry enters this candidate.

Approve a normal fast-forward of the existing codex/https-cycle04-public-proof branch in the public repository to that exact candidate, updating draft PR3 and triggering the same sole diagnostic job. This discloses only the revised test, ordinary commit metadata and normal diagnostic logs/artifacts under existing repository retention. No new PR, merge, default/PR2 update, trust bypass, package upgrade, provider harness, lock promotion, Azure spending/resources/permissions or portal activation. Reconfirm exact public head/default before push. Earlier approval binds4fadf7c bytes; repository control rule8 and the frozen public source-disclosure contract require approval for this changed public packet. No changed bytes have been published or rerun.

## Canonical and private status

KV-L01 publication PASS. KV-L02 actual signatures NOT VERIFIED. KV-L03 runner setup observed only; SDK/architecture/selected trust/artifact integrity NOT VERIFIED. KV-L04 honest failure/canonical reporting and KV-L05 local correction review are recorded in the execution receipt. All six linked [human tasks](https-production-test-packet.md#human-tasks-and-completion-conditions) remain open; shared-key task now requests the exact one-file correction publication/run before supported-platform proof. Production decisions/ARL cases/G1–G9/Milestone2 remain open.

The prepared private-board delta is this failed attempt, reviewed local correction and those six linked roles/completion conditions. BFF/HTTPS site remains stale: last confirmed snapshot4346e0f at2026-10-02T19:50:45.775370+00:00. Automatic approval review rejected the separate credential/network publisher proxy-bypass action; no retry/bypass occurred, and unrelated confirmed Cycle14 updates remain preserved.
