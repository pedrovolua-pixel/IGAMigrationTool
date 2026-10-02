# BFF Azure deployment preparation

Status: INDEPENDENT PREPARATION VERIFIED — D01/D02 local implementation approved and running; production/live decisions deferred
Owner: Azure/BFF coordinator
Started: 2026-10-02

## Authority and outcome

The owner said “Do it” after the next-step proposal: make the BFF deployable, verify its image, then prepare a priced private Azure verification session. Implement approved ADR-0004, IP-HAS-001/002/003, the identity/session design, authorization matrix and current test plan. Exact public routes and new provider/security permissions still require a concrete technical/security-owner decision under AGENTS.md and implementation-plan.md:124. A new paid Azure session needs refreshed costs and its own exact priced/cleanup approval. Existing draft PR2 remains unmerged; no production release follows.

The coordinator preserves completed cycle06 and the independently reviewed BFF foundation in the isolated `codex/bff-integration` checkout. Working packets start from its recorded planning merge commit. No customer data, Graph consent, live trust, database grant, public ingress, sign-in activation or paid resource is introduced by the independent packets.

## Work packets and path ownership

| Packet | Owner | Owned paths | Outcome |
|---|---|---|---|
| BFF-DP01 | Host worker | new `src/server/hosts/BffDevelopmentHost/`; `tests/integration/BffDevelopmentHost/`; new `infra/containers/bff-development.*` files and build helper | Explicit disabled executable composing BFF/session dependencies; reuses exact approved bootstrap GET health/live and health/ready contracts; no login/customer route. Startup refuses live activation/missing trusted configuration. No automatic migrations, provider calls or subject enrollment. Non-root OCI recipe excludes unrelated/customer/secret files. |
| BFF-DP02 | Image evidence worker | `infra/containers/collect-image-evidence.py`, `image-evidence-tools.lock.json`; `tests/infrastructure/ImageEvidence/`; packet README | Pinned official scanner; actual image vulnerability/license inventory and SPDX SBOM, immutable input/source/tool/database linkage and metadata-only evidence summary. Missing acceptance policy/signing/published digest/restricted-store proof remains NOT VERIFIED; collection cannot promote. No raw findings or credentials printed/published. |
| BFF-DP03 | Coordinator | production contract proposal, shared solution/workflows, canonical status/evidence and private board | Prepare exact transport/session-binding/provider/key/authority decisions; request technical/security approval. Implement D01/D02 routes/session-context changes only after their exact approval; production D03/D04 adapters await separate concrete schema/authority/key/proxy/audit decisions; preserve disabled live paths and executed evidence. |
| BFF-DP04 | Coordinator | next priced session proposal/ephemeral Azure inputs | Fresh delayed-charge/capacity check; exact private builder/pull/key/SQL grants and session cleanup scope. Provider/deployment spike starts only after required security/access/spend decisions; no implied approval from local CI. |

Workers use isolated worktrees; shared solution/workflows and canonical records belong to the coordinator. Each writing packet receives non-author review. Agent review does not supply human contract/security/gate acceptance.

## Executed scope review

Two independent read-only reviews confirmed that exact public BFF shapes, provider retrieval/permissions, CA evidence and proxy trust are absent. Subject authority currently returns authentication time/MFA with only a subject input; production composition must instead bind those to a particular validated authentication/session, so one session cannot borrow another's timestamp or MFA. Image acceptance/license policies and production builder/signing trust are also absent. The [concrete contract proposal](../../docs/development/bff-production-contract-proposal.md) records these decisions; missing evidence denies, never creates authority.

## Verification plan

DP01: locked restore, formatting, warning-free build; actual process startup/refusal, request raw-path/method denial, untrusted forwarded-header/configuration denial, disabled callback/challenge zero provider traffic, no cookie/session/data creation, deterministic restricted container context; actual Linux non-root/read-only container smoke in CI. Repeated tests remain synthetic, with no product routes.

DP02: scanner checksum/version/source identity verification; actual image inventory/SBOM scan when a Docker-capable runner is available. Negative fixtures distinguish config/OCI/published digests, tampered inputs/source, missing inventory/tool/database identity, failed subprocess, unsafe metadata/output and absent release authority. Inventory generation is not license/vulnerability acceptance; local unsigned metadata is not accepted signed provenance. Do not invent a freshness duration or severity/license acceptance policy.

Coordinator: integrate reviewed packets, run all applicable configured checks including BFF units/real PostgreSQL HTTPS/architecture and existing affected regressions; freeze exact source/artifact bindings. New migration/authority/public-contract tests await DP03 approval. Raw scanner reports stay in ephemeral ignored output until the restricted evidence intake is available; metadata in Git contains no detailed findings. No gate or Milestone2 is completed by this preparation.

## Human dependencies

- Technical/security owner: accept/replace/reject exact BFF contract decisions in the proposal; complete when decisions are attributed and the approved contract/tests updated.
- Security/operations owners: exact image vulnerability/license acceptance, signing/restricted evidence and private builder authority; complete when exact digest-bound required checks and reviewed promotion authority exist.
- Platform/identity owner: exact named pilot users, effective licensing, reviewed CA authentication strength/scope and production proxy/key/SQL/registry bindings before live use.
- Repository owner: a refreshed priced Azure session within the remaining $50 monthly allowance, exact scope and disposal; complete when approved and verified runtime evidence preserved before cleanup.

Canonical records remain authoritative. Update and publish the existing owner-private board as these packets and dependencies change; retain completed cycle06 and existing live-source tasks.


## Execution progress — 2026-10-02

DP01 author commit `a20d9157180ee9cd32bf13875fda43eefe38a02b` received non-author source/input review and an independently repeated 254-assertion process run. Coordinator integration passed pinned 43-project locked restore, whole-solution formatting and Release build (zero warnings/errors), 72 transport, 27 ticket, 2546 human-policy assertions, dependency boundaries, exact container context and the actual 254-assertion disabled host run with no SQL/proxy trap connections. These were pre-CI results. Actual Docker build/run subsequently passed in the hosted package verification below; no input check substitutes for that executed proof.

DP02 initial commit `e6d2784bf0f85d7a6245e551e563031806227ccd` passed 13 author and independently repeated negative/composition fixture methods. Review found its initial SPDX validation did not bind the described container to the scanned config ID; worker correction and non-author re-review are required before integration. Correction `b77b7120533d84eb386c752b47fdb18345ebe57e` now binds exactly one described CONTAINER root and unique ImageID annotation to the scanned config ID; 14 author, independent reviewer and coordinator fixture methods passed. The reproduced unrelated-document case now denies. No blocking review finding remains. Actual scan subsequently passed in the hosted package verification below. Acceptance, source-to-image build provenance, signer/restricted-store and promotion remain NOT VERIFIED.

The independently reviewed D01/D02 exact local contract has been presented to the technical/security owner; approval is pending. D03/D04 production schemas/authority/audit/proxy/keys remain explicitly deferred. A read-only [Azure preflight](../../docs/development/azure-bff-next-session-preflight.md) observed delayed current-month cost/budget readings; capacity, rates, exact image/bindings and fresh priced session approval remain open. No resource or grant was created.

## Hosted package verification and concurrent pilot merge

The actual Linux package run [37015069054](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37015069054) passed against combined code `c7b071ada291a0573534e96f88a89c3cd4786b1d`:268 process assertions, non-root/read-only Docker smoke,14 fixture methods, pinned scanner checksums and real vulnerability/license/SPDX collection. Docker config ID is `sha256:1797d20af863f2b9df38da33e03bb428ea7173b4f33f828f66726158a9574087`; this is not a published manifest digest. Raw inventories remained private ephemeral runner outputs and were destroyed by cleanup. No release acceptance, restricted preservation, signed provenance or source-to-image build proof follows.

Initial whole-branch CI exposed formatting drift in imported generated types, then a required nullable sibling absent from the partially imported pilot runtime. Reviewed deterministic regeneration and a temporary inert sibling repaired those issues. The pilot base concurrently advanced to `6c53e66`; an independently reviewed additive merge retained its complete approved recommendation implementation exactly, superseding the temporary sibling. The46-project combined source retains all BFF and pilot projects; the BFF runtime/collector are unchanged from reviewed packets. Full configured regression run [37015069129](https://github.com/pedrovolua-pixel/IGAMigrationTool/actions/runs/37015069129) passed all configured Linux, Windows2022/2025, PostgreSQL/HTTPS, browser, secret and infrastructure checks. Combined46-project restore/format/Release build passed with zero warnings/errors. No failed or unexecuted check is recorded as passing.

[Sanitized execution bindings](../../docs/development/evidence/bff-deployment-preparation-20261002.json) preserve packet/source/file hashes, executed checks, initial failures and remaining human dependencies.

Independent executable/container/inventory preparation is verified and published for draft review. DP03 D01/D02 implementation is waiting for the exact human contract decision. DP04 cannot form a live-provider deployment until production bindings/authority exist; fresh rates/capacity and a separate priced session approval remain required. This plan stays active; no milestone, gate, draft merge or live release is accepted.

Owner-private board publication confirmed2026-10-02: source `a9b36e92ef3a2d1bac5262d16f1596e89bb16833`, deployment `appgdep_6abfba5758448191b6d30816512e0ce8`; [current board](https://iga-pilot-progress.pedro-volu.chatgpt.site) preserves completed pilot work and11open human tasks, including exact D01/D02 review. Source/archive reference was refreshed after concurrent-site reconciliation; no history or access was overwritten.

Owner approval update2026-10-02: exact D01/D02 accepted for local implementation. [The bounded authentication packet](bff-authentication-contract-cycle.md) is RUNNING. Earlier pending-approval observations above are historical; production/live decisions remain deferred.
