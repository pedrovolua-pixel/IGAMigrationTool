# ADR-0006: Engineering evidence-store access and purge identities

Status: Pilot implementation choice — milestone review pending  
Date: 2026-09-29  
Decision owners: Technical owner, security owner, operations owner

## Context

The approved implementation plan requires a separate restricted East US 2 store for signed, sanitized engineering gate evidence. The current Bicep module defines private Blob access and versioning but has no role assignments or purge process. ADR-0005 selects the bundle signature algorithm; it leaves signer authority and trusted-key governance open. This ADR addresses only Blob data access and the default evidence lifecycle. It cannot activate a gate or authorize deployment.

## Decision drivers

- A producer must submit a new sanitized, signed bundle without gaining read, overwrite, delete, container-management or role-assignment rights.
- An authorized reviewer must retrieve evidence for digest/signature verification without gaining write or delete rights.
- A separate purge workload must remove current and previous versions on the approved clock, including after restore, without gaining signing authority.
- The approved default clock ends ordinary access 12 months after the gate decision and requires active-store purge within 30 additional days. Holds require a separate scoped, expiring authorized decision.

## Options

### Option A: Built-in Blob data roles

Assign Storage Blob Data Contributor to the producer and Storage Blob Data Reader to reviewers at the container. This is simpler, but Contributor permits deletion of current blobs and broader container operations. It does not express the producer's create-only boundary.

### Option B: Dedicated narrow data roles and identities (recommended)

Use separate Entra managed identities for intake, verification and purge. Define custom Azure Blob data roles with explicit actions, scope assignments to the `gate-evidence` container, and keep role-administration authority in the protected infrastructure deployment path:

| Principal | Proposed data actions | Denied by omission |
|---|---|---|
| Intake | `blobs/add/action` for new objects | Blob read/list, overwrite, delete, version delete, container management, role assignment |
| Verification/reviewer service | `blobs/read` | Blob create/overwrite/delete, container management, role assignment |
| Purge job | `blobs/read`, `blobs/delete`, `blobs/deleteBlobVersion/action` | Blob create/overwrite, signing, role assignment |

The action names above are under `Microsoft.Storage/storageAccounts/blobServices/containers/`. The intake uses digest-derived object names and a conditional create request. The verifier reads the exact object version and checks digest, signature, expected scope, key trust and retention state before a gate decision can use it. Human reviewer access remains through a separately authorized review path; a Blob read grant alone is not reviewer approval.

## Proposed decision

Proceed with Option B as a pilot draft under the repository owner's direction to continue development and review at each milestone. The separate Bicep role module defines custom roles and container-scoped assignments; no Azure grant has been deployed. An Azure integration spike must prove each allowed and denied operation with the exact managed identities, API calls and container scope before the store carries evidence. Do not place signing authority in the purge or reviewer identity. Storage versioning is tamper evidence only when retrieval verifies the signed digest and all versions are accounted for during purge.

The default lifecycle calculator may mark evidence as ordinary-access expired at the 12-month anniversary and purge due 30 days later. It does not perform deletion, recognize holds, or prove storage state. The purge process must start early enough to verify that current versions, previous versions and provider-retained deleted copies meet the active-store deadline. A failed or missing purge leaves evidence inaccessible, alerts operations and keeps the gate unverified if its bundle is expired. Restore must reapply the deletion ledger before access.

## Required validation before acceptance and deployment

- Confirm exact Azure data actions for conditional create, read/version retrieval, current deletion, previous-version deletion and any soft-deleted copy; prove the producer cannot overwrite or delete.
- Prove container-scoped role assignments, propagation/revocation, wrong-identity denial, private endpoint/DNS access and no Shared Key/SAS fallback.
- Prove sanitizer rejection, immutable object naming, signature verification on retrieval, version enumeration, hold authority and retention clock handling.
- Rehearse deletion and restore across the 12-month and 30-day boundaries, including a failed purge and a hold release.
- Review operational ownership and alerting for an inaccessible or overdue bundle.

## References

- `specs/003-health-assessment/implementation-plan.md` — approved evidence storage and retention
- `specs/003-health-assessment/test-plan.md` — gate evidence and promotion tests
- `architecture/decisions/ADR-0005-gate-evidence-signing.md`
- [Azure Blob data actions](https://learn.microsoft.com/en-us/azure/role-based-access-control/permissions/storage)
- [Azure Blob version deletion permissions](https://learn.microsoft.com/en-us/azure/storage/blobs/versioning-overview)
- [Azure Blob custom roles and container scope](https://learn.microsoft.com/en-ca/azure/storage/blobs/assign-azure-role-data-access)
