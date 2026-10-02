# Cycle11 fictional fix-package preview contract

Status: Settled coordinator implementation detail under the owner-approved local synthetic pilot direction; not a public API, production remediation contract, or human acceptance.

## Authority and outcome

FR-HAS-35/37/42, IP-HAS-008/Milestone7 inert-artifact subset, AC-HAS-18/14 and TP-HAS-018/014 authorize a bounded local, always-unverified fictional artifact preview. Preserve TP-HAS-002 provenance and TP-HAS-003/017 invariance; TP-HAS-009 denial and TP-HAS-013 presentation are engineering subsets. Approved product/technical/implementation/test specs govern. ADR-0001 rendering isolation remains required. This contract settles reversible internal fixture shapes only. Actual artifact review authority/history/invalidation, priority/effort, desired objectives, task conversion/workflow and CSV/export require their later exact contracts and are excluded.

## Entry points and closed immutable shape

New framework-only module `SyntheticFixPackages` references only existing `RecommendationGuidance`. No application host/UI integration, database, migration, route, run-input/profile/version change, provider, customer data channel, resolver, interpreter, execution, file download, task or review mutation is introduced.

`FixPackageBuilder.Build(GuidanceSnapshot? guidance)` returns `FixPackageResult` with `Succeeded`, immutable `Snapshot`, or payload-free `FixPackageIssue` (MissingGuidance, InvalidGuidance, IntegrityMismatch, OutputTooLarge). Rebuild existing guidance via `RecommendationGuidanceBuilder.Build` using the complete source/findings mapped back to GuidanceInput. Accept only exact rebuilt canonical payload bytes AND ContentDigest equal to the supplied snapshot; this rechecks fixed schema/status/warnings/unavailable sections, source locks, option IDs/status and all originals, rather than trusting hash-shaped strings. Reject default/null/malformed nested records without throwing. Rebuilt source JsonElements are independently cloned; preserve every guidance field and unchanged upstream canonical digest. This is fixture integrity, not customer authorization or factual validation.

`FixPackageSnapshot` is a sealed class with internal constructor and get-only properties: SchemaVersion=`synthetic-fix-package-preview-v1`, Status=`Unverified`, Disclaimer, Guidance (complete rebuilt GuidanceSnapshot), TemplateVersion=`fictional-fix-templates-v1`, TemplateDigest, Templates, Packages, Warnings, UnavailableSections, CanonicalJson, ContentDigest. Output records are immutable: `FixTemplate(TemplateId, Kind, Text)`; `FixArtifact(ArtifactId, TemplateId, Kind, Status, Text)`; `FixOption(ScopedOptionId, Artifacts)`; `FixPackage(PackageId, FindingId, Options)`. Collections use ImmutableArray. Empty guidance yields empty Packages plus an explicit no-findings/no-action warning. No mutable deserialization or public snapshot factory.

One package per EXISTING grouped FindingId; never merge by root-cause prose. One option per existing ScopedOptionId, and the same three fixed fictional templates per option. This deliberately demonstrates traceability, not rule-specific supported fixes or semantically generated remediation. Finding confirm/reject/defer never changes artifact Unverified status. Stable ordinal IDs/order do not mean priority/effort/quick-win ranking.

## Exact fixed templates and warnings

Templates sorted ordinal by TemplateId, each text without trailing newline:
- `fictional-config-v1`, Kind=`Configuration`, Text=`{\n  "fixtureOnly": true,\n  "reviewRequired": true\n}` (the documented \n means actual LF).
- `fictional-script-v1`, Kind=`Script`, Text=`# Fictional review-only example. No customer-system action.\nWrite-Output 'Fixture review required'`.
- `fictional-sql-v1`, Kind=`Sql`, Text=`-- Fictional review-only example. No customer database or object.\nSELECT 'Fixture review required' AS FixtureMessage;`.

Disclaimer exactly: `Fictional fix-package preview. Every artifact is unverified and review-only; these generic examples are not supported One Identity remediation. No execution or approval is authorized.`
Warnings exactly, in order:
1. `Finding confirmation, rejection or deferral does not review artifacts or validate remediation.`
2. `Package identity ordering is not priority or effort. Existing finding groups are preserved without root-cause merging.`
3. `Source guidance is retained verbatim as historical input; its unavailable sections describe that upstream projection.`
4. Only when guidance.Findings empty: `No findings were supplied; no fix packages or actions are available.`
UnavailableSections exactly, in order:
1. `Consultant artifact review, approval history and content invalidation are unavailable.`
2. `Priority, effort, customer objectives, task conversion/workflows and CSV/export are unavailable.`
3. `Customer-system execution, external connectors, validated recovery/remediation and report publication are unavailable.`

## Canonical identity, bounds and ordering

Use UTF-8 System.Text.Json Web/default escaping, recursive ordinal object keys, ordered arrays, decimal G29 when numbers occur, lowercase UUID D, no BOM or trailing LF. Guidance payload follows its existing recipe. Full snapshot payload includes every listed property except CanonicalJson/ContentDigest; complete Guidance INCLUDED with its ContentDigest. CanonicalJson is UTF8 decoded canonical payload; ContentDigest lowercase SHA256 thereof.

TemplateDigest hashes canonical JSON `{templateVersion,templates}`. PackageId hashes canonical JSON `{findingId,runId,scope}` (identical scope shape to guidance); ScopedOptionId remains upstream unchanged. ArtifactId hashes canonical JSON `{packageId,scopedOptionId,templateId,templateVersion}`. IDs intentionally stable across presentation/review changes; entire snapshot/source digest changes when content does. No supplied arbitrary template registry. Packages sorted by FindingId; Options sorted by ScopedOptionId; Templates/Artifacts sorted by TemplateId. MaximumCanonicalBytes=32MiB, MaximumRecords=100000 across package+option+artifact counts; denial occurs before oversized canonical serialization where possible. Preserve upstream 16Ki UTF16 text/32MiB/100000 limits.

`FixPackageBuilder.CanonicalPayload(FixPackageSnapshot snapshot)` returns a new caller-owned byte[] of the complete current property payload (except CanonicalJson/ContentDigest), not cached CanonicalJson bytes. Renderer must compare these actual current fields to rebuilt bytes AND verify cached CanonicalJson/ContentDigest; checking only cached digest/json is insufficient for forged internal instances. Builder constants expose SchemaVersion, TemplateVersion, MaximumCanonicalBytes and MaximumRecords.

`FixPackageHtmlRenderer.Render(FixPackageSnapshot? snapshot)` returns `FixPackageRenderResult`, immutable `FixPackageHtmlSnapshot(Html, ContentDigest, PackageDigest)` or `FixPackageRenderIssue` (MissingSnapshot, InvalidSnapshot, OutputTooLarge). Strictly verify snapshot integrity/closed relationships before rendering (rebuild from Guidance and compare complete canonical/digest). HTML digest SHA256 UTF8 exact output, no unstable timestamp. MaximumHtmlBytes=64MiB. Framework HtmlEncoder.Default encodes every supplied prose/identifier/code text. Deterministic complete output shows source bindings/frozen versions/locks/digests, all finding originals/current states/context/occurrences/options/guidance/references/assumptions/limitations and every artifact/template ID/kind/Unverified/code text, with progressive disclosure. Preserve upstream unavailable text, clearly labeled historical upstream boundary; new layer warnings define current boundary.

Fixed CSP `default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'`; fixed inline style only, no scripts/event handlers/forms/iframes/images/resources/user-supplied URLs/raw links/download/copy/action controls. Fixed local fragment anchors permitted. Code is pre/code encoded data. Keyboard/native details, readable mobile wrapping and no horizontal viewport overflow. This separate fixture-only renderer/test process receives no application/PG/provider credentials and serves no customer values. Browser test host only serves fixed generated fixtures on ephemeral loopback and denies other requests; browser blocks external requests. These checks do NOT establish the deployed ADR sandbox/customer security or full manual/browser/accessibility matrix.

## Verification and ownership

A11 owns builder/contracts + unit builder tests, B11 renderer + renderer unit tests, V11 independent portable integration/oracles/browser fixtures and non-author review. Coordinator owns all csproj/locks/solution/workflow/canonical records/Site. Independent literal canonical oracles must not consume production canonicalization; expected HTML/layout coverage must inspect actual browser, malicious markup/shell/network/formula-like prose, zero external fetches/actions, provenance membership, empty/permuted/multi-group and forged snapshots. Existing scores/review/draft/guidance/AI-disabled inputs remain unchanged; existing regression suite runs on the final combined source. Test template changes require version/digest change; do not regenerate expected files to hide failures.
