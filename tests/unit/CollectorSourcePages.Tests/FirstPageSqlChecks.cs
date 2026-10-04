using CollectorSafety;
using CollectorSourcePages;

namespace CollectorSourcePages.Tests;

internal static class FirstPageSqlChecks
{
    internal static int Run()
    {
        var count = 0;
        var policy = new StrictKeysetSqlPolicy("fixture", "NativeFixture", ["K", "U", "V"], "K", "@PageSize", "@After");
        const string valid = "SELECT TOP (@PageSize) K, U, V FROM fixture.NativeFixture ORDER BY K ASC";
        Check("first valid", FirstPageSqlValidator.Evaluate(valid, policy) == StrictKeysetSqlDecision.StructurallyReady);
        Check("first table qualifier", FirstPageSqlValidator.Evaluate("SELECT TOP (@PageSize) NativeFixture.K, NativeFixture.U, NativeFixture.V FROM fixture.NativeFixture ORDER BY NativeFixture.K", policy) == StrictKeysetSqlDecision.StructurallyReady);
        Check("first declared alias", FirstPageSqlValidator.Evaluate("SELECT TOP ((@PageSize)) n.K, n.U, n.V FROM fixture.NativeFixture n ORDER BY n.K", policy) == StrictKeysetSqlDecision.StructurallyReady);
        var negatives = new[]
        {
            valid.Replace(" ORDER BY", " WHERE K > @After ORDER BY"), valid.Replace(" ORDER BY", " WHERE K > 0 ORDER BY"),
            valid.Replace("TOP (@PageSize) ", ""), valid.Replace("@PageSize", "2"), valid.Replace("@PageSize", "@Other"),
            valid.Replace("K, U, V", "U, K, V"), valid.Replace("K, U, V", "K AS Renamed, U, V"),
            valid.Replace("K, U, V", "K, U, V, K"), valid.Replace("K, U, V", "K, U"), valid.Replace("K, U, V", "*"),
            valid.Replace("SELECT TOP", "SELECT DISTINCT TOP"), valid.Replace("SELECT TOP", "SELECT ALL TOP"),
            valid.Replace("(@PageSize)", "(@PageSize) PERCENT"), valid.Replace("(@PageSize)", "(@PageSize) WITH TIES"),
            valid.Replace("FROM fixture.NativeFixture", "FROM fixture.OtherFixture"),
            valid.Replace("FROM fixture.NativeFixture", "FROM other.NativeFixture"),
            valid.Replace("FROM fixture.NativeFixture", "FROM db.fixture.NativeFixture"),
            valid.Replace("FROM fixture.NativeFixture", "FROM fixture.NativeFixture WITH (NOLOCK)"),
            valid.Replace("FROM fixture.NativeFixture", "FROM fixture.NativeFixture JOIN fixture.OtherFixture o ON K = o.K"),
            valid.Replace("FROM fixture.NativeFixture", "FROM fixture.NativeFixture FOR SYSTEM_TIME ALL"),
            valid.Replace("FROM fixture.NativeFixture", "FROM fixture.NativeFixture TABLESAMPLE (10 ROWS)"),
            valid.Replace("K, U, V", "ABS(K), U, V"), valid.Replace("K, U, V", "K, (SELECT U), V"),
            valid.Replace("ORDER BY K ASC", "ORDER BY K DESC"), valid.Replace("ORDER BY K ASC", "ORDER BY U"),
            valid.Replace("ORDER BY K ASC", "ORDER BY K, U"), valid + " OFFSET 1 ROWS", valid + " OPTION (RECOMPILE)",
            valid + " FOR XML AUTO", valid + "; SELECT 1", "WITH c AS (SELECT 1 k) " + valid,
            "UPDATE fixture.NativeFixture SET K = 1", "DELETE FROM fixture.NativeFixture", "EXEC fixture.ReadFixture", "broken sql",
            valid.Replace("K, U, V", "foreign.K, U, V"), valid.Replace("@PageSize", "@@ROWCOUNT"),
            valid.Replace("(@PageSize)", "(@PageSize + 0)"), valid.Replace(" ORDER BY", " GROUP BY K, U, V ORDER BY")
        };
        foreach (var sql in negatives) Check("first closed negative", FirstPageSqlValidator.Evaluate(sql, policy) != StrictKeysetSqlDecision.StructurallyReady);
        foreach (var sql in new[] { null, "", valid + " --\ud800", valid + " --\udfff" })
            Check("first invalid UTF input", FirstPageSqlValidator.Evaluate(sql, policy) == StrictKeysetSqlDecision.InvalidInput);
        Check("first paired UTF input", FirstPageSqlValidator.Evaluate(valid + " --\ud83d\ude00", policy) == StrictKeysetSqlDecision.StructurallyReady);
        Check("first bad declaration", FirstPageSqlValidator.Evaluate(valid, policy with { ProjectedColumns = ["K", "k"] }) == StrictKeysetSqlDecision.InvalidInput);
        var ports = new ScriptedSourcePorts();
        Check("pair valid", SourceQueryPairPreflight.Evaluate(ports.Pair, ports.Binding) == SourcePageReason.None);
        Check("pair no implicit revision", SourceQueryPairPreflight.Evaluate(ports.MakePair(packRevision: 2), ports.Binding) == SourcePageReason.RegistryMismatch);
        Check("pair no implicit policy revision", SourceQueryPairPreflight.Evaluate(ports.MakePair(policyRevision: 2), ports.Binding) == SourcePageReason.RegistryMismatch);
        Check("pair first digest", SourceQueryPairPreflight.Evaluate(ports.MakePair(firstHash: new string('B', 64)), ports.Binding) != SourcePageReason.None);
        var reordered = ports.Descriptor.Sql.Replace("K, U, V", "U, K, V");
        var reorderDescriptor = ports.Descriptor with { Sql = reordered, SqlSha256 = ScriptedSourcePorts.Hash(reordered) };
        var reorderExpected = ports.Expected with { SqlSha256 = ScriptedSourcePorts.Hash(reordered) };
        Check("strict historical allows unordered declaration", QueryPackPreflight.Evaluate(reorderDescriptor, reorderExpected).StructurallyReady);
        Check("new pair demands ordered projection", SourceQueryPairPreflight.Evaluate(ports.MakePair(descriptor: reorderDescriptor), ports.MakeBinding(expected: reorderExpected)) == SourcePageReason.InvalidQueryPair);
        var badSource = ports.Expected with { Source = new("unknown-fixture-build", []) };
        Check("exact unsupported source", SourceQueryPairPreflight.Evaluate(ports.Pair, ports.MakeBinding(expected: badSource)) != SourcePageReason.None);
        Check("pair semantic version", SourceQueryPairPreflight.Evaluate(ports.MakePair(descriptor: ports.Descriptor with { PackVersion = "1" }), ports.Binding) != SourcePageReason.None);
        Check("pair schema version binding", SourceQueryPairPreflight.Evaluate(ports.MakePair(schemaVersion: "changed"), ports.Binding) == SourcePageReason.RegistryMismatch);
        Check("pair normalization version binding", SourceQueryPairPreflight.Evaluate(ports.MakePair(normalizationVersion: "changed"), ports.Binding) == SourcePageReason.RegistryMismatch);
        Check("no boundary rewrite of historical validator", StrictKeysetSqlValidator.Evaluate(valid, policy) != StrictKeysetSqlDecision.StructurallyReady);
        return count;
        void Check(string name, bool passed) { if (!passed) { Console.Error.WriteLine(name); throw new InvalidOperationException(); } count++; }
    }
}
