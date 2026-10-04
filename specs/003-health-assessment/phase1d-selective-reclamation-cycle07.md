# Phase 1D cycle07 selective occupied cursor reclamation verification

Status: VERIFIED LOCALLY — configured hosted checks/private publication pending
Date: 2026-10-03
Baseline: 2a2f732b1ec2b21524afba68119168e81a73723e
Plan: [bounded cycle07](../../plans/active/local-pilot-phase1d-selective-reclamation-cycle07.md)
Authority: [exact local approval](local-phase1d-approval.md), frozen local contract and P1D-T06/07/08/09/10/11/12.

## Selected proof

Independent review found a composed coverage gap in selective customer quota of 512 cursor reclamation. Existing sparse expiry/revocation, one-slot identity/global recovery and cycle05 occupied audit recovery remain preserved. No runtime defect is assumed. Four isolated worlds use the original Findings page size2: NamedUser/Service × partial expiry/one-identity revoke and regrant. All expected fields, original digest and 14 scalar bindings derive from independently preserved contract/fixture expectations.

An external ledger records successful opaque handles and predicts their revision/expiry groups. Original occupancy is103/103/102/102/102 across five identities, with 250 handles created at 0, 250 at 60 and 12 at 120 seconds. A separate raw/effective clock and request-window ledger counts every invocation below unrelated identity/customer rate ceilings; no private registry inspection is used.

Partial expiry predicts 250 oldest handles expire at 300 seconds, while later handles remain. Representative stale handles deny before protected reads; exact 250-slot refill and next allocating Limited distinguish reclamation from incidental success. New allocation after raw-clock rollback uses effective 300 seconds, with a representative refill handle valid at 599.999 seconds, expired at 600 seconds and still denied after rollback. Revision change affects only identity 0: revoke/regrant advances its revision, old handles deny before protected reads, exactly 103 refill allocations complete and the next is Limited. Every unaffected identity has a representative surviving final-page read; new-revision target handles remain valid.

## Execution and review

Author `c51952a` and coordinator `9221771` (identical Git tree to root `f947d38`) execute four worlds / 2,808 invocations / 64,457 assertions. Each expiry world has 774 calls with maximum 53 identity / 259 customer requests per 60 seconds; each revision world has 630 calls with maximum 57 / 250. Exact 250 / 103 refill ceilings and representative survival/stale denial pass. Every invocation accepts one payload-free in-memory audit; operational failures are zero. Every successful envelope retains exact typed fields, original digest and 14 scalar bindings.

Whole-solution locked restore, format and Release build pass with zero warnings/errors. Original integration 2,849, cycle03 10,060 requests / 92,904 assertions, cycle04 24 scenarios / 112 invocations / 1,011 assertions, cycle05 16/5,200/88,562, publication 260, boundary 10,444, Python 14 byte/hash artifacts / 47 denial requests, and architecture 7 policy / 4 project cases pass separately. These counts describe repeated engineering observations, not unique product requirements. Dependency audit covers 102 projects with no reported vulnerable packages; Gitleaks passes on 1,999 tracked textual source/config/document/fixture files (31,946,392 scanned bytes), excluding generated bins and image/PDF/font binaries.

[Execution metadata](../../docs/development/evidence/phase1d-selective-reclamation-cycle07-20261003.json) pins exact source/tree/runtime/oracle and native logs. Final DLL SHA256 is e2b6570cb132287b3abc7d7afbe71330ddb5847b4618a64f4562e162e3ceb98a. The initial author missing assets build failure and first focused execution log remain historical; the final source is bound to the final 64,457-assertion result. Initial oracle hashes identify preserved bytes; its preinspection ordering is the author's declaration, with literal fixture-derived expectations/no production expected-value helper visible for review.

All 326 existing production source paths and 16 original integration fixture paths remain unchanged. Separately 380 initial committed paths and 380 initial working paths are preserved; the four pre-existing dirty UI working files are distinct from committed bytes and remain intact. Only the new test/oracle pair and additive 6-line Main registration change. Independent final DLL execution passes the original/new suites with unchanged DLL/source hashes and no actionable scoped review findings. Its native log SHA256 is `51b78adbcfb80599e17becbc7df2bbf9a366309110a0cbc40389f0117f33d0cd`. The initial wrapper expected adjacent cycle03 count text; parsing was corrected against the unchanged successful log and the original wrapper record is retained. Configured hosted verification and native private publication remain pending.

## Limits and human next actions

Representative rejected/surviving handles and an exact observed refill ceiling do not mean every handle was replayed. No new global quota of 4,096, concurrent reclamation, expiry-during-emission, native publisher/reader/client, real token/grant, durable/distributed cursor/audit or production-scale claim is made. No runtime/configuration/dependency/migration/activation change is permitted. Full Phase1D/Milestone11/UAT/G1–G9 remain NOT VERIFIED.

The [cycle06 owner handoff](phase1d-owner-handoff-cycle06.md) still supplies the next human actions: assign reporting/technical owners for exact immutable publication I01–I04; security/technical/operations complete I05–I07 in parallel; approve/freeze the actual-reader candidate before code. Client/identity/operations I08–I11 remain separate. All 13 existing human tasks stay open. Canonical records and the owner-private board will be updated with executed evidence before this cycle is reported complete.
