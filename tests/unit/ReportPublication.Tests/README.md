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

## Compiled checkpoint

The package-free `ReportPublication.Tests.csproj` references the repository module by default. In this sparse worker checkout only, use `-p:ReportPublicationProject=/private/tmp/iga-release-native-publication/src/server/modules/ReportPublication/ReportPublication.csproj` with locked restore and run. The property is a local compilation path override, not a runtime configuration or authority grant. The pinned executable used is `/private/tmp/iga-dotnet-10.0.401/dotnet`.

```sh
dotnet restore tests/unit/ReportPublication.Tests/ReportPublication.Tests.csproj --locked-mode
dotnet run --project tests/unit/ReportPublication.Tests/ReportPublication.Tests.csproj --configuration Release --no-restore
```

The current compiled packet checks157 independent assertions:31 original hash/length commitments;31 exact typed canonical output vectors; three strict production projection parser roundtrips;14 original Unicode cases at the strict test scalar layer and separately at the production projection parser;12 raw parser refusals;33 typed source/projection refusals;15 malformed manifest/audit/receipt cases; one adversarial typed set-order check; and two defensive-ownership plus one nonpublic-source-factory checks. Counts are executable assertions rather than scenario or milestone acceptance. Fourteen Unicode production parser cases insert valid canonical scalar bytes or original hostile bytes into a known valid projection, ensuring the actual decoder handles them. Source envelopes are supplied only through the trusted friend typed source constructor; the test loader accepts no real source data and proves no source JSON admission boundary.

Initial harness failures are retained in `compiled-execution-evidence.json`: constructor record parameter casing and two ineffective counter string mutations were corrected before acceptance; attempted same-category rules across independently classified linked records were removed after checking the approved contract. The replacement source-category tests enforce membership in the frozen requiredCategories without inventing category equality across records. No production failure was observed in this checkpoint. Persistence18 cases remain planned and did not run.

Final executed source was frozen from tracked core commit `7ded2dc418acf0d6ab9451b562af3d67ebf61b82` into the disposable minimal snapshot `/private/tmp/iga-rp03-source-7ded2dc` (module plus build properties only), then referenced through the same property override. This prevents concurrent author work from changing the reviewed build inputs. This packet tests codec/parser behavior only; the core checkpoint's initial publisher is compiled but not independently accepted by these assertions.
