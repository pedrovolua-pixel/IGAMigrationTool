# Synthetic human policy checks

Dependency-free executable checks for the human subset of TP-HAS authorization and the approved human-role matrix. Run with the pinned .NET SDK using `dotnet run --project tests/unit/IdentityPolicy.Tests/IdentityPolicy.Tests.csproj --no-restore` after locked restore.

Fixture families:

- `IDPOL-MATRIX`: independently enumerated allowed/denied role/action cells, with role-specific read projections.
- `IDPOL-SCOPE`: every allowed cell repeated with identity, customer/project/environment/assessment/resource/category/revision, role, policy and lifecycle denial mutations.
- `IDPOL-CONDITION`: each additional condition removed independently; an unrelated role cannot supply it.
- `IDPOL-PUBLISHED`: published content denies mutation while acknowledged/link/deletion lifecycle operations require the exact supported resource action.
- `IDPOL-REAUTH`: exact 15-minute boundary, stale/future authentication, wrong subject/session/version/action and missing MFA/Conditional Access verifier.
- `IDPOL-GUEST`: 90-day expiry and 30-day review bounds, exact expiry denial, sponsor/engagement changes and missing onboarding state.
- `IDPOL-REFUSAL`: null/unknown/unsupported actors, enums, snapshot fields and failing authority adapters.
- `IDPOL-OVERLAP`: independent roles, no borrowed authority, consultant/risk/evidence separation and executive projection limits.

All identities and opaque identifiers are synthetic. These tests execute the real public authorizer with fake trusted adapters and a fixed clock; they do not prove provider authentication, actual authoritative data resolution, persistent/session concurrency, field serialization, worker/share/MCP authorization or deployed access.
