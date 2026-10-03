# HTTPS production contract preparation

Status: PREPARED — proposed specifications ready for human review; implementation blocked
Date: 2026-10-03 UTC
Baseline: `b065b51ad97eda9cd96dc08504802b974e819cd1`
Owner: Coordinator; technical/security, identity/platform and operations reviewers
Authorization: Owner “Proceed” after the published HTTPS packet and production-contract handoff. This permits preparing the review packet, not accepting security choices or implementing/activating production behavior.

## Basis and scope

Follow [the next packet](../../docs/development/https-pilot-portal-next-packet.md), accepted local ADR-0009/0011, the approved product, identity, authorization and audit policy. Preserve permanent diagnostic-host disablement, public authentication contract, private backend boundary and existing retention. Propose concrete alternatives for unresolved durable audit, provider authority, shared keys and ingress evidence. No new runtime, SDK dependency, route, grant, tenant query, protected identifier, migration execution, Azure operation or spending.

## Bounded work packets

| Packet | Owner and isolated scope | Required outcome | State |
|---|---|---|---|
| HTTPS-PC01 | Provider worker, `/tmp/iga-https-provider-spec`, branch `codex/https-provider-spec`; writes only `docs/development/https-production-provider-contract-proposal.md` | Proposed exact Graph retrieval/validation contracts and organizational guest limitations, least-permission alternatives, traceable positive/negative/recovery tests and official sources; no new approvals | VERIFIED — document/source checks and non-author review only |
| HTTPS-PC02 | Key worker, `/tmp/iga-https-key-spec`, branch `codex/https-key-spec`; writes only `docs/development/https-production-key-contract-proposal.md` | Proposed SDK/Blob/Key Vault adapter contract, conditional writes and historical-version recovery tests; preserve retention and exact protected bindings; official sources | VERIFIED — document/source checks and non-author review only |
| HTTPS-PC03 | Coordinator, integration checkout | Integrate reviewed provider/key proposals; production host/admission/audit/ingress technical proposal; conditional implementation sequence and acceptance test packet; prepare named human tasks | VERIFIED — proposed documents only |
| HTTPS-PC04 | Non-author review | Review proposed security choices, exact source/type compatibility, requirement/test mapping, scope, links and absence of protected data; no gate acceptance | VERIFIED — final non-author document/source review PASS |

Workers branch from the same known baseline, retain path ownership, return immutable commits and exact executed document/source checks. Cross-review is independent of the author. The coordinator owns all canonical records, shared configuration and private-board reporting.

## Completion evidence and limits

Execute changed-document link/anchor, JSON where added, whitespace and protected-prose/secret checks. No runtime/build/database/browser/cloud success is inferred from documentary checks. Prior hosted proof remains source-bound to ba2f447; receipt-only repeat checks remain separate. A proposed implementation/test sequence cannot become READY for implementation until human decisions and approved specifications resolve material security ambiguity.

Private BFF/HTTPS site snapshot remains stale under the separately rejected credential/network publishing action; the public GitHub publication approval does not resolve that blocker. Prepare the exact board delta and report publication unavailable. Preserve unrelated confirmed Cycle14 publication.


## Candidate sources and review outcome

Provider author `800ee86b7b8efe1179115edf82df419f383feb3f` (integrated `6787eb3`) passed independent key-worker review against actual closed authority/provider/session types;17 local targets and20 ordered cases plus whitespace/UTF-8/Gitleaks passed independently. Key author `a4c9a2b7364fe56f9bdb52cb691b1e7dec8536a9` (integrated `99ae3ee`) passed independent provider-worker review;17 local targets/anchors,18 ordered cases, whitespace/UTF-8/Gitleaks passed. Neither worker executed an adapter, queried a tenant or called Azure.

Root proposal review identified and corrected exact anonymous receipt target/security-version constraints, earliest challenge/callback/logout producer ownership, required source-occurrence retention interpretation and explicit CA context-without-policy denial. Prior accepted contracts remain intact. The first preintegration link check failed only on the not-yet-integrated worker documents; final combined checks and evidence are recorded after integration.

The output is a reviewable proposal and planned test packet. Canonical G1–G9/Milestone2 stay unverified. Dependent production implementation remains BLOCKED pending exact decisions; this preparation does not leave workers running unattended.


## Executed combined documentation checks

The coordinator checked all changed proposal/canonical Markdown:447 local targets,17 anchors,68 unique planned case IDs (30 host/audit/ingress/live,20 provider,18 key), JSON where present and whitespace/protected UUID/email guards passed. Six existing Microsoft icon embeds remain byte-identical; no diagram changed. Whole-checkout Gitleaks8.30.1 found no leaks. Source guards show no runtime/test/contract/SQL/IaC/workflow/project/dependency change from b065b51. These are documentation checks, not execution of proposed acceptance tests or approval of security decisions. Independent final review by `package_hosting` passed: all14 source digests, both immutable worker blobs, exactly15 documentary paths, empty protected-code scope,68 planned IDs and zero unresolved local targets. No runtime/cloud checks ran. Final independent record closure is indexed with [preparation evidence](../../docs/development/evidence/https-production-contract-preparation-20261003.json).
