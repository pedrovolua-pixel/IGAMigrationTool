# Independent native publication oracle — RR-P03

Owned paths: this directory and the [literal shape addendum](../../../specs/003-health-assessment/native-publication-v1-oracle-addendum.md). Isolated sparse worktree `/private/tmp/iga-release-publication-oracle`, branch `codex/release-publication-oracle`, integrated baseline `b6c806e`. Sparse checkout conserves disk; it changes no repository source or project membership.

The **31 original canonical UTF-8 files and literal SHA-256/length goldens were committed at `51272d2` before the production hash codec**. Three independent bundles cover Completed, warned High AI with a numeric coverage gap, and source-owned CompletedWithGaps with SourceLimitation and zero numeric coverage gap. A separate anonymous denial event carries no actor/scope/resource. Original non-ASCII, astral/ZWJ, newline/tab, quote/backslash, NUL and control characters are included without Unicode normalization.

`fixtures/author_vectors.py` is the retained original authoring program using the Python stdlib JSON encoder. It never imports production code. **Do not run it to refresh expectations after implementation changes.** An intentional contract amendment requires independently reviewed new original fixtures while preserving existing bytes. `oracle/verify_vectors.py` is a separate manual canonical writer/strict shape and linkage verifier; it independently compares all committed byte/digest/length commitments. `oracle/test_oracle.py` exercises malformed and hostile representations, binding substitutions, exact warnings/markers and original uncertainty semantics.

Run from the repository root:

```sh
python3 -B tests/unit/ReportPublication.Tests/oracle/verify_vectors.py
python3 -B tests/unit/ReportPublication.Tests/oracle/test_oracle.py
```

These require only Python's standard library, no restore/install/build, no network and no disposable database. They are contract/oracle checks, **not production codec/store/policy/lease tests**. Coordinator owns solution/CI wiring. The compiled test project below uses no additional packages and loads fictional fixtures into the actual typed module API; its fixture loader is test-only and establishes no external JSON admission contract.

`fixtures/unicode-vectors.json` contains four valid and ten rejected raw-byte cases. Valid surrogate-pair JSON is parsed to the original scalar then independently canonicalized; raw malformed UTF-8 or unpaired/reversed/separated surrogate escapes are refused without replacement hashing. `fixtures/persisted-cases.json` contains18 planned real restricted-PostgreSQL/fence/audit/read-delivery cases; none ran in RR-P03. Guard booleans, a document fixture or the reference script cannot prove these mechanisms.

All source records, policy/hold IDs, users, scopes, timestamps, classifications and digests are fictional local fixture inputs. Native version labels establish no eligible One Identity source, central audit authority, actual customer permission, qualified reviewer, retention policy, provider deployment, renderer, MCP adapter or gate acceptance. No production module/API/configuration/dependency/migration/grant changed. Historical draft/MCP fixtures remain untouched.

## Frozen compiled scenarios (before implementation)

The compiled packet must verify all 31 original SHA-256/length commitments and exact typed reserialization for every supported codec (source/projection/score/command/read request first; manifest/audit/receipts when available). Original expectations remain immutable. Four valid Unicode vectors must preserve their canonical scalar bytes through a typed display field; ten malformed vectors must be refused by strict test decoding and the production parser when available. Typed lone-surrogate inputs must refuse before hashing.

The 19 reference refusal groups remain executed independently. Compiled equivalents additionally exercise source/projection scope and input bindings, source-only provenance ownership, exact warning category/kind/record targets, severe unreviewed false flags with and without warnings, marker section/ID/field substitution, root-cause/reference/category links, coverage arithmetic/reasons, unavailable/null pairs, actor/revision/digest validation, token/text bounds, duplicate IDs and unknown enum values. Constructor inputs and returned bytes are mutated after capture to prove defensive ownership. Command invocation/correlation exclusion is tested independently. All source factory calls are through the explicitly trusted test friend; no public source constructor may exist.

Persistence and lease delivery cases remain **PLANNED_NOT_EXECUTED**. Raw parser-only refusals remain distinct from typed codec checks until the actual parser exists. This separation prevents a strict test loader from being counted as a production admission check.
