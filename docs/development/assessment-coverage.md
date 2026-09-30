# Assessment coverage reconciliation (local foundation)

`AssessmentCoverage` is an internal Phase 1A foundation for FR-HAS-2, FR-HAS-8 and TP-HAS-001. Given a trusted plan of inventory/evidence-category keys and terminal results, it requires exactly one result for every planned key. It rejects missing, duplicate, unexpected or malformed keys; invalid states; and gap states without a structured reason code and responsible stage. It never infers a pass from absent evidence.

The allowed terminal states match the approved technical specification: `pass`, `finding`, `not_applicable`, `not_assessed`, `insufficient_evidence`, `excluded`, `inaccessible`, `redacted`, `unsupported`, and `error`. Gap reason and responsible stage must be present; their exact format is left to the approved evidence contract. The result contains issue codes and internal keys for a caller to handle under authorization; callers must not put keys or protected evidence into ordinary logs.

This component does not build the expected inventory, decide applicability, access the feature-001 baseline, authorize a user, score health/quality, activate a run or publish a result. The full TP-HAS-001 and G2/G3/G4 gates remain `NOT VERIFIED`. Thirteen local synthetic cases exercise completeness, duplicates, unexpected results, typed gaps, invalid states/keys and empty-plan behavior.

`CoverageCountProjector` snapshots its inputs and exposes unweighted counts for each terminal state only after the same reconciliation passes. It returns issues and no projection for missing, duplicate, unexpected or malformed results. It calculates no score or percentage. Four additional synthetic cases include a 100,000-item count exercise; this is a local fixture, not an end-to-end scale or performance claim.
