# Parallel pilot development

Status: Approved by repository owner on 2026-10-01

The repository owner approved one coordinator and three workers for bounded local pilot development. This is a development workflow within the [approved local build](../../plans/active/one-identity-local-pilot-build.md), not a change to product scope, architecture, security authority or release gates.

## Responsibilities

| Role | Owns |
|---|---|
| Coordinator | Work packets and dependencies, shared contracts/configuration, integration, canonical plans/status/evidence, human reporting, and the existing private pilot site |
| Collector worker | Assigned collector implementation and focused synthetic recovery tests |
| Assessment worker | Assigned assessment implementation and focused synthetic tests |
| Verification/platform worker | Independent review, integration fixtures and explicitly assigned CI/platform work |

Roles may rotate between bounded packets. An agent reviewer provides engineering review, not an independent human security, operations, SME or accessibility approval required by a gate.

## Assign and isolate work

1. Read the canonical specifications and applicable repository skill. Select a small vertical outcome with requirement/test IDs, exact permitted paths, dependencies, acceptance cases and disabled live paths. Record it in an active execution plan before coding.
2. Give each writing worker a separate worktree and branch from the same known commit. Use the `codex/` branch prefix. Confirm checkout and branch before edits; subagents do not receive isolated worktrees automatically.
3. Keep shared solution/project/workflow configuration and canonical records under coordinator ownership unless a packet explicitly delegates a particular file. Workers report needed changes rather than modifying another owner's files.
4. Settle consequential shared contracts through the existing specification/ADR decision process before dependent implementation. Continue independent approved packets while a contract or human input is pending.
5. Use synthetic, versioned fixtures and the pinned toolchain. Keep customer evidence, credentials and protected identifiers out of worktrees, tests, logs and status summaries.

## Handoff and integration

Workers return a patch or commit reference, touched paths, requirement/test coverage, exact executed checks/results, unmet acceptance cases, migration/configuration implications and proposed documentation updates. The coordinator confirms ownership and scope, obtains review from a worker other than the author, integrates small patches and runs every applicable repository check on the combined source.

Working states are `READY`, `RUNNING`, `REVIEW`, `VERIFIED`, and `BLOCKED`; they describe packets only. `VERIFIED` means the packet's stated local checks were executed successfully and review completed. It does not complete a feature test plan, milestone or G1–G9 gate.

On completion of a work cycle, update canonical feature plans, status and evidence metadata first. Then update and publish the existing owner-private site according to [pilot site maintenance](pilot-status-site.md), including the human tasks board. Confirm deployment before reporting the site current. An unavailable check remains `NOT VERIFIED`; a publication failure is reported with the last verified snapshot.

The human update states what became runnable, which checks passed, unresolved blockers/risks, the next integration outcome, decisions requiring human authority and whether the private site reflects the canonical records. Human tasks retain their requested role, source-document link and completion condition.

## Session lifecycle

Use subagents for bounded packets coordinated in the active chat. Separate persistent chats and recurring schedules require an explicit request; this workflow does not create them. Save execution state in the repository so later work can resume from records. At a completed checkpoint, preserve reviewable integrated work and remove clean temporary worker worktrees; preserve unfinished changes before cleanup. Never leave a final report implying that completed subagents continue running unattended.
