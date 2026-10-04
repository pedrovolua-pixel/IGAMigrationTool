# Independent native publication oracle — RR-P03

Owned paths: this directory and the [literal shape addendum](../../../specs/003-health-assessment/native-publication-v1-oracle-addendum.md). Isolated sparse worktree `/private/tmp/iga-release-publication-oracle`, branch `codex/release-publication-oracle`, integrated baseline `b6c806e`. Sparse checkout conserves disk; it changes no repository source or project membership.

The **31 original canonical UTF-8 files and literal SHA-256/length goldens were committed at `51272d2` before the production hash codec**. Three independent bundles cover Completed, warned High AI with a numeric coverage gap, and source-owned CompletedWithGaps with SourceLimitation and zero numeric coverage gap. A separate anonymous denial event carries no actor/scope/resource. Original non-ASCII, astral/ZWJ, newline/tab, quote/backslash, NUL and control characters are included without Unicode normalization.

`fixtures/author_vectors.py` is the retained original authoring program using the Python stdlib JSON encoder. It never imports production code. **Do not run it to refresh expectations after implementation changes.** An intentional contract amendment requires independently reviewed new original fixtures while preserving existing bytes. `oracle/verify_vectors.py` is a separate manual canonical writer/strict shape and linkage verifier; it independently compares all committed byte/digest/length commitments. `oracle/test_oracle.py` exercises malformed and hostile representations, binding substitutions, exact warnings/markers and original uncertainty semantics.

Run from the repository root:

```sh
python3 -B tests/unit/ReportPublication.Tests/oracle/verify_vectors.py
python3 -B tests/unit/ReportPublication.Tests/oracle/test_oracle.py
```

These require only Python's standard library, no restore/install/build, no network and no disposable database. They are contract/oracle checks, **not production codec/store/policy/lease tests**. There is intentionally no .NET test project yet; coordinator owns solution/CI wiring and later production-code integration.

`fixtures/unicode-vectors.json` contains four valid and ten rejected raw-byte cases. Valid surrogate-pair JSON is parsed to the original scalar then independently canonicalized; raw malformed UTF-8 or unpaired/reversed/separated surrogate escapes are refused without replacement hashing. `fixtures/persisted-cases.json` contains18 planned real restricted-PostgreSQL/fence/audit/read-delivery cases; none ran in RR-P03. Guard booleans, a document fixture or the reference script cannot prove these mechanisms.

All source records, policy/hold IDs, users, scopes, timestamps, classifications and digests are fictional local fixture inputs. Native version labels establish no eligible One Identity source, central audit authority, actual customer permission, qualified reviewer, retention policy, provider deployment, renderer, MCP adapter or gate acceptance. No production module/API/configuration/dependency/migration/grant changed. Historical draft/MCP fixtures remain untouched.
