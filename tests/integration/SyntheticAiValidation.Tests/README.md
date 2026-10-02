# Independent offline AI fixture composition

V8 independently verifies the bounded cycle08 portions of TP-HAS-008 and related002/003/009/017. It does not call a real provider, network, tools, storage, raw resolver, historical run, profile, scoring engine, database, host or UI. `FakeProvider` is test-only: it returns a fixed immutable response string and exposes a call count.

`packet-input.json` and `provider-output.json` are separately authored fictional primitives. Before A8/B8 implementation handoff, standalone standard Python generated explicit sorted-key contract envelopes and documented set ordering. Expected golden bytes never call `FixtureJson`, builder/validator canonical helpers or implementation hash code. The complete pinned oracles are:

- Packet SHA256 `39c0b00c1443ddbeb42e17e02ec2f0f5ea897988d0e01de14478d0fb71a8ccaf`.
- Proposed snapshot SHA256 `a4051d78ca8683c08a822f5fb3d8b13af9db0cf3466551b9f981878adc671334`.

The golden fixes every schema/status/source/evidence/classification/redaction/configuration/rule/typed statement/citation/conflict/uncertainty/missing-context field. Two records, two rules and two proposals deliberately differ in input identity order. A declared conflict retains both evidence IDs, nonblank uncertainty and ordered missing context. Independent SHA checks bind the fixed files to literal expected digests; full canonical byte comparisons bind the implementation to these separate expectations.

Composed cases verify the fixed provider result remains Proposed, explicit zero proposals never assert health, frozen source/data changes alter identity and stale/cross-run outputs fail, syntactically valid citations omitted from the current packet fail membership, logical property/set permutations retain identity while statement/context order matters, repeated validation has no side effects and caller-owned objects cannot mutate accepted strings/collections. Public constructors/setters are unavailable and module assembly references contain no network/provider SDK.

The denial matrix independently removes/nulls/wrong-types every required property at every known object level, adds raw/tool/fetch/state/approval/severity/policy/storage/secret/execution fields, and duplicates ordinary/Unicode-escaped keys. Additional malformed/trailing/comment, wrong scope/version/digest/classification/allowlist/run, empty/duplicate/foreign citations, unsupported IDs, no-statement and declared-conflict failures return typed payload-free codes. String/UTF8/depth/record/statement bounds and malformed escaped/literal surrogate cases are tested. Encoded instructions, fake system roles, HTML/Markdown/images, SQL/shell/formula text remain exact inert values; the fixture supplies no executor or renderer.

```sh
dotnet restore tests/integration/SyntheticAiValidation.Tests/SyntheticAiValidation.Tests.csproj --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low
dotnet format tests/integration/SyntheticAiValidation.Tests/SyntheticAiValidation.Tests.csproj --verify-no-changes --no-restore
dotnet build tests/integration/SyntheticAiValidation.Tests/SyntheticAiValidation.Tests.csproj -c Release --no-restore
dotnet run --project tests/integration/SyntheticAiValidation.Tests/SyntheticAiValidation.Tests.csproj -c Release --no-build
python3 tests/integration/SyntheticAiValidation.Tests/bind-artifacts.py
```

`execution.log` and `verification.log` contain group names/counts/outcomes and tool diagnostics, with no supplied payload or provider response. Failure reporting suppresses exception text and stacks. `artifacts.json` binds authored definitions/oracles, exact reviewed A8/B8/shared source, executed test/module DLLs and transcripts through separate `{path,sha256}` records. The binder rejects a failing execution and intentionally excludes the artifact manifest from recursive evidence binding.

Declared classification/redaction, scope IDs and digests do not authenticate an actor or prove semantic redaction. Citation membership cannot prove inference correctness, detect undeclared semantic contradiction or establish real-model injection resistance. Real provider/ZDR/US controls, authorization/revocation, budgets/reservation/reconciliation, retry/outage/deletion/recovery, model quality and actual rendering remain NOT VERIFIED. Full TP-HAS-008/012, Milestone6, SEC-PILOT-004 and G1–G9 remain open. No migration, dependency version or public contract changes are required.

Final executed V8 evidence:978 independent composed assertions/four groups on sealed A8`dc16212403351111eac2103631b2a563674ff846` and B8`c4c12a0c9259ec4b43adf45e73a000b78f5a9c45`. Pinned SDK10.0.401/runtime10.0.12, locked audited restore, scoped formatting and Release0warnings/0errors passed. Non-author read-only reviews of both entire patches/shared helper/value and coordinator CI wiring closed with no actionable findings;60 exact author source/artifact/Git comparisons matched. No module source, provider, host, database, saved run or scoring change was authored by V8.
