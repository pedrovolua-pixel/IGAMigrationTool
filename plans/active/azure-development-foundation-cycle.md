# Azure development foundation continuation

Status: Infrastructure preparation complete; authorized Azure execution awaits unlocked Mac
Owner: Azure setup coordinator
Date: 2026-10-01

The repository owner requested continued Azure configuration until remaining items are resolved. This cycle implements the missing infrastructure inputs under accepted ADR-0004, Milestone 1 / G1, approved product/technical/test plans and the USD 50 monthly boundary. It does not change retention, identity policy, customer isolation or enable gated live paths.

## Work packets

| Packet | Owner | Permitted paths | Outcome and checks |
|---|---|---|---|
| AZ-01 | Registry/private-services worker | New `infra/bicep/modules/pilot-container-registry.bicep`, `pilot-key-vault.bicep`, `pilot-private-dns.bicep`; new focused policy script under `tests/infrastructure/AzureFoundation/` for these modules | Private ACR Premium and Key Vault with managed-identity/RBAC-only closed defaults; DNS links; pinned build/lint and mutation checks; no data grants or secrets |
| AZ-02 | Database worker | New `infra/bicep/modules/pilot-postgresql.bicep`; new focused database policy script under `tests/infrastructure/AzureFoundation/` | Major 18, no HA/geo backup, private connectivity, no password/secret outputs; explicit administrator and retention inputs; pinned build/lint and drift checks; flag unresolved policy inputs |
| AZ-03 | Compute/observability worker | New `infra/bicep/modules/pilot-container-apps-environment.bicep`, `pilot-observability.bicep`, `pilot-monitor-private-link.bicep`; new focused policy script under `tests/infrastructure/AzureFoundation/` | Consumption workload-profile environment with existing delegated/NAT subnet; payload-free monitoring scaffold; explicit approved retention inputs; build/lint and drift checks. No application image, public ingress or sign-in activation |
| AZ-04 | Coordinator | Composition entry point, non-secret session proposal, canonical records and private site | Independently review each packet, compose/verify templates, price complete session, Azure preview/validation where actual inputs are available, configure authorized prerequisites, record concrete input/cost/runtime blockers |

Writing workers use separate worktrees from the recorded base commit. They do not modify shared workflow/solution configuration, canonical plans or the site. The coordinator preserves concurrent local pilot cycle changes in the main checkout. Each worker supplies official Microsoft source links, executed checks and a patch/commit for independent review. Production and customer data remain disabled; no speculative endpoint, identity, retention policy or public contract is invented.

## Completion criteria

- Missing modules are concrete, compile/lint and pass relevant negative configuration checks.
- Azure changes fit a complete cost estimate and authorization; permissions and irreversible cleanup receive required confirmation against exact scope.
- Every remaining configuration item has executed evidence or a specific external/product dependency. G1 is not marked passed without the complete required deployed tests.
- Canonical status/evidence and the existing private board reflect this cycle and its remaining dependencies.

## Executed packet evidence and review

Workers started from `6aa5aeb829660c68098e0b7f0adbfc683a7960cb` in separate `/tmp/iga-azure-private-services`, `/tmp/iga-azure-database` and `/tmp/iga-azure-compute` worktrees. The coordinator preserved concurrent cycle 02 work. AZ-01 commits a0527ff/6715466, AZ-02 faeb037, AZ-03 a2536ec/78e3568 were integrated. Database worker independently reviewed private/compute; private worker reviewed database/coordinator policy; compute worker reviewed composition. Review corrected the ACA API pin and reproduced secret-output/manual-connection/tenant/consent guard gaps. Current templates have no reported material defect; runtime gates are unrun.

Coordinator executed pinned Bicep 0.47.16 formatting equality/build/lint for seven modules, entry-point build/lint and policies: 66 private-services mutations, 27 database template mutations plus 14 invalid input cases, 41 compute/monitor mutations, 9 composition and 9 inert Entra mutations rejected. Database retention endpoints 7/35 days accepted. No new packages. Compiled foundation SHA-256 `d586c2f92999ad56eb9d9e3560dfc656454c84f7d81fcaa850d13f060ad20347`. Source comments were clarified after compilation without changing compiled behavior. CI includes these checks; remote run status must be recorded after push, not inferred.

The four-role inactive Entra manifest SHA-256 is `e40fc0c8f4a11c365b50c8211e6895c646796de47f2ca3eacae97dde7a32a27d`. Registration management readback showed zero service principals/federations/credentials/redirects/consent. Aggregate budget readback passed for one combined USD 50 monthly limit covering the two explicit groups, preserved three alert thresholds/recipients, reported zero spend with delayed costs. No paid resources created in this continuation.

Owner approved the concrete USD 25 / maximum 24-hour session, synthetic retention and scoped access. Template upload is blocked by the locked Mac; the owner has been asked to unlock it. This does not expire or revoke approval. Azure what-if/validate/create/configuration reads/cleanup are pending; G1 is NOT VERIFIED. After browser access resumes, execute the exact approved proposal and preserve results before resource cleanup. Actual hosted BFF/images, pilot users/licensing/Conditional Access scope and live gate tests remain external/product dependencies.

Final local whitespace check passed. Gitleaks 8.30.1 initially flagged an ordinary sentence in the concurrent cycle-02 record (`ignored/private` prose following “secrets”); inspection showed no credential. Rewording that sentence preserved its meaning and the subsequent whole working-tree redacted scan passed with no leaks. Full feature/E2E and deployed security/recovery tests were not run because this packet changes infrastructure preparation only and no hosted workloads exist. Remote bootstrap results remain pending until push/run completion.
