# Synthetic publication unit harness

Run from the repository or test directory with the pinned .NET SDK:

```sh
dotnet restore tests/unit/SyntheticMcpPublication.Tests/SyntheticMcpPublication.Tests.csproj --locked-mode
dotnet run --project tests/unit/SyntheticMcpPublication.Tests/SyntheticMcpPublication.Tests.csproj --no-restore
```

Fixtures are deliberately fictional canonical UTF-8, authored with Python `json.dumps(sort_keys=True, ensure_ascii=False, separators=(',', ':'))` and Python SHA-256 independently of the C# codec. The manifest hash is pinned literally in Program.cs. Test mutations recompute hash commitments to distinguish closed-shape/canonical validation from simple hash mismatch. The coordinator's independently authored integration oracle remains separate.

The harness covers P1D-T01/02/05/11: six source schemas and projections, named-user/service projections, exact decimal/unavailable/frozen bindings, digest integrity, scope/version/state and canonical-byte denial, closed text including nested prose, trusted schema field denial, category minimization/linkage and current availability overlays. Policy/rate/audit/races and actual production publication/authentication are outside this unit harness.
