# Cycle04 engineering validation addendum

Status: implemented; bounded engineering within the original frozen policy.
Date: 2026-10-03

The [original engineering freeze](../../docs/development/evidence/m08-evaluation-workflow-engineering-freeze-20261003.json) remains immutable. These corrections preserve its accepted outcomes, grants, source/cohort and transaction rules:

- Review/correction strings must be well-formed UTF16. Unpaired surrogates deny rather than allowing replacement encoding to collapse distinct command bodies into one digest. Valid pairs and literal replacement characters remain permitted. Escaped malformed HTTP strings return the specified closed 400 response.
- A changed trusted registry record advances its individual revision exactly once. Unchanged records retain their revisions. Current action/context compatibility is still rechecked before commit and before replay disclosure.
- Transport timestamps use exact UTC round-trip format with seven fractional digits, matching the supplied canonical envelopes. This corrects serialization interoperability without changing time or authority.
- Existing migration namespaces are verified before DDL. Metadata has exactly the four required NOT NULL/no-default columns and exactly the named primary key among non-NOTNULL constraints. PostgreSQL18 catalog NOT NULL constraints are recognized. Extra defaults, CHECK/FK/UNIQUE constraints or untracked/incompatible baselines deny without adoption or repair.
- The isolated Vite build uses platform-aware builtin fileURLToPath. The browser verifier uses the existing relative pinned Playwright dependency and explicitly selected or default installed Chromium.

Nonauthor reviews and native initial failures/final checks are indexed in the cycle04 evidence receipt. No approval receipt or preimplementation expectation was rewritten. Limits of1000 aggregate changes/1001 versions and16MiB responses are source reviewed; maximum-capacity/maximum-response and clock rollback stress are not executed. Manual accessibility, real source/identity/assignment and queued operations remain outside this bounded proof.
