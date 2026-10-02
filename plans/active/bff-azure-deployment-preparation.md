# BFF Azure deployment preparation

Status: RUNNING — disabled executable/image preparation authorized; production contract decisions pending
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
| BFF-DP03 | Coordinator | production contract proposal, shared solution/workflows, canonical status/evidence and private board | Prepare exact transport/session-binding/provider/key/authority decisions; request technical/security approval. Implement dependent host/adapters only after approval; preserve disabled live paths and executed evidence. |
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
