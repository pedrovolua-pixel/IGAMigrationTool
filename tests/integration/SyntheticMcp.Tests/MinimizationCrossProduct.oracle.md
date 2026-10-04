# Cycle03 independent minimization oracle

Pinned before reading production Projection/McpHarness: baseline cb2d66c5609152738f95bf5cbceca15f27bbee78, approved internal contract and Python-authored existing fixture bytes. Manifest SHA256: 613f1bca6039cddee6d19aae6e3ae23ff92bbda8027fafa08dcb79d53128291d.

Rules used by the new test (P1D-T03/05/11):

- Enumerate both NamedUser and Service; all six exact resource kinds; all 16 subsets of Summary, Configuration, Identity, Operations; and every subset of each kind's optional schema fields.
- Source manifest descriptors select rows by exact resource kind and category grant before any content read. For this fixed fixture, every kind has descriptors: an empty selected subset returns Unavailable with no content, cursor or item reads. This is not a general rule for genuinely empty manifest collections, covered separately by existing tests.
- Each selected row always contains itemId and category, including when the grant omits one or both. Every other output field requires its exact name in the field grant.
- Source optional fields by kind: PublishedStatus assessmentState/approvalState/limitations; Coverage assessed/gap/unavailable/label/limitations; Scores health/quality/maturity/reason; Findings title/summary/severity/reviewState/confidence/mandatoryReview/referenceIds; Recommendations findingId/summary/options/priority/effort/reviewLabel; ProtectedReferences availability/reason/currentAvailability/availabilityReason.
- Values come from the immutable fixture payload JSON. Findings referenceIds keep only references whose descriptor category is allowed; recommendation findingId is absent if its finding descriptor category is denied. Link minimization never reads referent content.
- The ProtectedReferences currentAvailability/availabilityReason overlay is a separate policy value, projected only under its own field grant and only for selected authorized references. Historical availability/reason remain fixture values. Exercise Available/None, Unavailable/Expired and Redacted/Redacted overlays.
- Success envelope has exactly contractVersion/resourceKind/manifestDigest/bindings/items/nextCursor, with no total counts. Digest is the pinned original manifest digest. Bindings contain every original manifest scalar except collections/items, unchanged. The fixture fits one page, hence nextCursor is null.
- Returned audit field names equal the union of actual row schema fields. Redacted names equal the union of the per-row full schema minus the actual row fields, plus referenceIds whenever its values are partially or wholly filtered (a linked field can therefore appear in both unions). This partial-link audit interpretation was made explicit after implementation inspection; the initial pre-inspection note is preserved in external evidence. Names never include values. Denied empty projections return no scoped audit schema fields.
- Check the exact ordered selected (kind,itemId) source-read sequence and one manifest read for a nonempty category grant; the empty category grant denies before any source read. Business writes/raw resolution have no port in this composed harness and are not newly instrumented by this guard. Unknown grant field names must deny with DependencyUnavailable and never echo that raw name into audit.

The oracle must not call production Projection.Fields, Project or Envelope or use hashes computed from a production projection. The existing Program.World/Request/User helpers supply harness context only. Expected schema, values, descriptor selection, links, bindings and schema-name unions are built directly from the fixed rules and original fixture bytes above. This guard verifies fictional fixture minimization only, not actual integration, production authorization or free-text sanitization.

## Execution and integration handoff

The writing verifier executed the new class alone through an external `MinimizationProbe.cs` startup wrapper, injected by an external conditional `MinimizationProbe.targets` using `CustomAfterMicrosoftCommonTargets`. Neither file was tracked; no existing Program/project/lock/fixture/runtime file was edited. The coordinator must call `await MinimizationCrossProduct.RunAsync()` in the approved console entry point before ordinary console/CI runs execute this guard.

Pinned SDK: `/private/tmp/iga-dotnet-10.0.401/dotnet`. Worker commands, from its isolated checkout:

```sh
/private/tmp/iga-dotnet-10.0.401/dotnet restore tests/integration/SyntheticMcp.Tests/SyntheticMcp.Tests.csproj --locked-mode
/private/tmp/iga-dotnet-10.0.401/dotnet format tests/integration/SyntheticMcp.Tests/SyntheticMcp.Tests.csproj --no-restore --verify-no-changes --include tests/integration/SyntheticMcp.Tests/MinimizationCrossProduct.cs
/private/tmp/iga-dotnet-10.0.401/dotnet build tests/integration/SyntheticMcp.Tests/SyntheticMcp.Tests.csproj -c Release --no-restore -p:CustomAfterMicrosoftCommonTargets=/private/tmp/iga-p1d-cycle03-minimization-evidence/MinimizationProbe.targets -p:OutputPath=/private/tmp/iga-p1d-cycle03-minimization-evidence/probe-bin
/private/tmp/iga-dotnet-10.0.401/dotnet /private/tmp/iga-p1d-cycle03-minimization-evidence/probe-bin/SyntheticMcp.Tests.dll
python3 tests/integration/SyntheticMcp.Tests/fixtures/author_oracle.py --verify
/private/tmp/iga-dotnet-10.0.401/dotnet build tests/integration/SyntheticMcp.Tests/SyntheticMcp.Tests.csproj -c Release --no-restore
/private/tmp/iga-dotnet-10.0.401/dotnet run --project tests/integration/SyntheticMcp.Tests/SyntheticMcp.Tests.csproj -c Release --no-build --no-restore
```

Final results: locked restore exit0; scoped format verification exit0; probe and ordinary builds exit0 with zero warnings/errors; Python original oracle exit0 (14 exact byte/hash artifacts, 47 denial requests); standalone guard exit0 (10,060 distinct invocations and 92,904 assertions); unchanged original console exit0 (2,849 assertions, counted separately). Partition: 8,448 optional-field subset ×16 category subset ×2 identity combinations over six kinds; 576 full-optional grants explicitly omitting one or both mandatory labels; 1,024 additional reference-overlay combinations; 12 unknown-field controls. The mandatory omission masks are 0 (neither granted), 1 (itemId only), and 2 (category only); the main matrix grants both.

Original format verification failed exit2 on four whitespace diagnostics, then only the owned new class was formatted. No runtime probe failed and no production defect was observed. An incidental `shasum` command failed exit9 because the host Perl locale was unavailable; SHA256 provenance was subsequently computed with Python. Initial oracle note SHA256 `ffa1a80fc3d3b3226e34f84d659eaa6d480fcd7241b22b15f20129f1f11eac10` is preserved externally with the before-production contract/fixture digest snapshot. The partial-reference audit amendment and empty-category pre-manifest clarification are explicit above.

External evidence directory: `/private/tmp/iga-p1d-cycle03-minimization-evidence`. New class SHA256 `aaab221cb3e0f66b7e9fc7fb63b80d2bff644ca13b6a19f9712d8ef43a5865eb`; startup wrapper SHA256 `70f0dde2dd8c5294f8db6aa8623b08e46b2fd41d9ae6b21f14423ae4f21d61ba`; targets SHA256 `bdba4e513a5025611f123953e2e85f831157cb3e199af4e422b95ac0346f22ce`; standalone probe assembly SHA256 `0474aea6bb4bb03f612781c4baf60b92651f297eb22873d84e515a418e3f89ea`.

Worker limits: no coordinator combined-source/CI, other unit suites, whole-solution checks, site publication, actual protocol/client integration or customer/transport/network operations were performed. Source preservation and architecture checks remain coordinator-owned. No new dependency, configuration, migration or production behavior is included. Non-author review and entry-point integration remain required before the packet is VERIFIED.
