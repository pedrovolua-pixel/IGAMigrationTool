# Pilot status site maintenance

The owner-private [IGA Pilot Progress site](https://iga-pilot-progress.pedro-volu.chatgpt.site/) is a dated summary for the One Identity health-assessment pilot. Its [human tasks board](https://iga-pilot-progress.pedro-volu.chatgpt.site/#human-tasks) points people to the exact repository documents that request information or evidence. It is not the canonical gate or feature-status record.

## Locate the existing site

- Sites project ID: `appgprj_6abd3ea5f20081918f3f6e81099c2a32`.
- Source repository: `https://git.chatgpt-team.site/8adcc9c1-644e-4670-9b83-fa08f920b27b/appgprj_6abd3ea5f20081918f3f6e81099c2a32.git` (`main`).
- The current local checkout, when present, is `.sites-work/pilot-progress/`. It is a separate, locally ignored Git repository; do not add its contents to this repository. Its `.openai/hosting.json` declares `dist/` as the static output.
- If the local checkout is absent, retrieve this existing source repository through Sites with a short-lived source write credential. Do not create another site. Never persist or print the credential.

## Update triggers and source records

Whenever pilot implementation progress updates a canonical plan or feature status, or a milestone, gate, phase, or human dependency changes:

1. Update the applicable canonical records first, especially `specs/003-health-assessment/implementation-plan.md`, `specs/003-health-assessment/status.md`, `specs/003-health-assessment/evidence-index.md`, and `specs/001-data-ingestion/status.md` or the [SME evidence template](../../specs/001-data-ingestion/one-identity-sme-evidence-template.md) when affected. Use executed evidence for gate claims; unrun or unsupported checks remain `NOT VERIFIED`.
2. Bring the site snapshot date, repository commit reference, phase and gate labels, milestone summary, and blockers into line with those records. The human tasks board must show each open dependency's requested role, required information or evidence, completion condition, gate or milestone, and direct links to its current repository instructions. Add, change, or close cards as the canonical records change. Link to the live pilot branch for working templates; use an immutable commit for the dated source snapshot.
3. Keep customer payloads, protected environment identifiers, credentials, permission dumps, raw SQL, and detailed security findings out of the site and Git. Show only non-sensitive status and protected artifact references permitted by the canonical records.
4. Check site links and desktop/mobile rendering. Commit and push the separate site source, then publish the exact pushed commit to the **existing private** Sites project. Confirm deployment success and the published board before reporting the milestone or gate update complete. Preserve owner-only access; changing the audience needs separate authorization.

If the site cannot be updated or published, report the last verified snapshot and the concrete publishing blocker. Do not describe the site as current while it is stale. A site update does not itself pass a gate, authorize source access, or release the product.
