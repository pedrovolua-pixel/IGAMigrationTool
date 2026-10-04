# Independent structured guidance verification

This bounded synthetic suite tests TP-HAS-002/003/009/013/014/017/018 guidance and inert-text portions. It grants no recommendation review, approval, task/export, artifact execution or customer authority. All options remain Unverified, including when their finding is Confirmed or Rejected; all runs remain Scoring and unpublished.

`InputFixture` is separately authored data. `canonical-golden.json` was written with standalone standard Python sorted JSON before executing the builder: schema/status, every fixed source field, two independently defined finding groups, three complete original options, three original occurrences, ordered validation/references/assumptions/limitations, warnings and unavailable sections. SHA256 `8cb3d8874c104b5175e1d84a942306a3d93cdb20d9eedd13965120db071bd4c7` is fixed in provenance. Scoped identity expected bytes are explicit literal sorted-key JSON; expected hashes never call the implementation's canonical/hash helpers. Complete snapshot bytes are compared against the literal file. The existing draft-v1 independent golden remains `a48a9aefe6efcfa864812f6ba7b84211877a73df9e519704a114561dc31d07e9`.

Saved-run fixtures use typed engine/analysis/review output only as input. Expected counts (five groups, ten occurrences/options), all option prerequisite/risk/recovery text, ordered validation and authoritative fixture references are literal. Actual PostgreSQL cases confirm/reject/edit through existing authorized finding operations, retain original options and scoped identities, compare earlier detached values, reproduce current bytes with fresh engine/store reads, deny cross-scope resolutions and preserve the four pre-cycle literal input envelopes/digests. Healthy/all-gap runs yield zero options. No new persistence schema is introduced. The tests do not claim the pure module recomputes originals from absent raw evidence; host upstream validation remains required.

```sh
dotnet restore tests/integration/RecommendationGuidance.Tests/RecommendationGuidance.Tests.csproj --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low
dotnet format tests/integration/RecommendationGuidance.Tests/RecommendationGuidance.Tests.csproj --no-restore --verify-no-changes
dotnet build tests/integration/RecommendationGuidance.Tests/RecommendationGuidance.Tests.csproj -c Release --no-restore
dotnet run --project tests/integration/RecommendationGuidance.Tests/RecommendationGuidance.Tests.csproj -c Release --no-build
IGA_GUIDANCE_TEST_DATABASE='Host=127.0.0.1;Port=55433;Database=iga_synthetic_v7;Username=iga_synthetic' \
dotnet run --project tests/integration/RecommendationGuidance.Tests/RecommendationGuidance.Tests.csproj -c Release --no-build -- --postgres
```

The PostgreSQL opt-in permits only loopback and dedicated disposable database `iga_synthetic_v7`; initialize it once before the suite. It never drops/restarts the shared cluster or customer/historical demo databases. Other historical helper fixture files are linked, hashed and preserved. The portable run explicitly reports actual PostgreSQL NOT VERIFIED. `execution.log` records final actual checks; `verification.log` records restore/format/build evidence. Exact authored definitions, borrowed source, host/test binaries and transcript hashes are in the browser packet's `artifacts.json`.

Initial execution exposed a sandbox-only stalled build/restore; the identified verifier process was stopped and pinned checks rerun with the existing package cache and approved runtime access. A build attempted before pending restore completed correctly refused missing assets, then passed after completed restore. No assertion, expected golden, implementation arithmetic or package version changed to pass. The final browser rerun includes the exact formatted sealed module contract; the earlier pass is superseded by its final bound evidence.

Full supported Windows/NVDA/Narrator/manual WCAG, customer evidence, live pilot, CSV/PDF, overall feature/milestone and production acceptance remain NOT VERIFIED.
