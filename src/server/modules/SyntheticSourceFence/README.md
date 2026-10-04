# Synthetic run source fence

Framework-only parameterized transaction advisory-lock helper for the approved Cycle13 local source capture. It owns no domain tables or authority. Assessment mutation, finding seed/mutation and artifact read/mutation acquire it before row locks and hold it through their own transaction. Ordinary source reads do not reacquire it. The host must use the same guarded database and trusted fixed scope for every participant.

The exact key recipe and lock order are in [the frozen implementation contract](../../../../specs/003-health-assessment/local-artifact-review-implementation-contract.md#shared-source-fence-and-capture). This adds no external dependency, migration or production configuration. It is serialization within the existing modular transaction boundaries, not a distributed lock or production concurrency policy.
