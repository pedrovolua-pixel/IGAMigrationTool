using CollectorSafety;
using System.Security.Cryptography;
using System.Text;

internal static class QueryPackPreflightChecks
{
    internal static int Run()
    {
        const string sql = "SELECT TOP (@PageSize) [UID], [State] FROM [dbo].[FictionalArtifact] WHERE [UID] > @After ORDER BY [UID]";
        // Independently computed by Python hashlib, not the production digest routine.
        const string digest = "6760ac8cda36efcf707dbffb967f85abc923e95fabd00118fea644dd90e4e54f";
        var packId = Guid.Parse("976667e2-6525-4cd3-aa71-5347e9537001");
        var fields = new List<QueryPackField>
        {
            new("UID", "uniqueidentifier", false, FieldClassification.ApprovedReference),
            new("State", "int", true, FieldClassification.ApprovedReference)
        };
        var parameters = new List<QueryPackParameter> { new("@PageSize", "int", false), new("@After", "uniqueidentifier", false) };
        var builds = new List<string> { "10.0.0.100" };
        var versions = new List<string> { "10.0.0.100" };
        var modules = new List<InstalledModule> { new("FIC", "10.0.0.100") };
        var included = new HashSet<FieldKey> { new("FictionalCategory", "UID"), new("FictionalCategory", "State") };
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var policy = new FieldPolicySnapshot("fictional-policy", "1.0.0", new string('b', 64), included, excluded);
        var candidate = new QueryPackDescriptor(packId, "1.0.0", "fictional-query", "1.0.0", sql, digest,
            new("fictional-query", builds, "FIC", versions), "dbo", "FictionalArtifact", "FictionalCategory", fields,
            new(policy.PolicyId, policy.Version, policy.Sha256), "fictional-minimum-read", "1.0.0",
            "UID", "fictional-key-review", "@PageSize", "@After", parameters, 100, 1000, TimeSpan.FromMinutes(1), "fictional-query-review");
        var expected = new QueryPackExpectedBindings(packId, "1.0.0", "fictional-query", "1.0.0", digest,
            new("10.0.0.100", modules), policy, "fictional-minimum-read", "1.0.0");
        var count = 0;
        Check("valid", candidate, expected, QueryPackPreflightDecision.StructurallyReady);
        Check("upper hex", candidate with { SqlSha256 = digest.ToUpperInvariant() }, expected, QueryPackPreflightDecision.StructurallyReady);
        Check("no optional module", candidate with { Applicability = candidate.Applicability with { ModuleId = null, SupportedExactModuleVersions = [] } }, expected,
            QueryPackPreflightDecision.StructurallyReady);
        Check("exact row budget", candidate with { MaximumRows = 100 }, expected, QueryPackPreflightDecision.StructurallyReady);
        var labeledPolicy = policy with { Version = "fixture-policy-v1" };
        Check("explicit exact version labels", candidate with
        {
            Policy = candidate.Policy with { Version = labeledPolicy.Version },
            MinimumReadSetVersion = "exact-read-v1"
        }, expected with { Policy = labeledPolicy, MinimumReadSetVersion = "exact-read-v1" },
            QueryPackPreflightDecision.StructurallyReady);

        foreach (var (name, item) in new[]
        {
            ("empty pack ID", candidate with { PackId = Guid.Empty }),
            ("empty query", candidate with { QueryId = "" }),
            ("wildcard pack version", candidate with { PackVersion = "1.x" }),
            ("empty version component", candidate with { PackVersion = "1..0" }),
            ("unsupported version label", candidate with { PackVersion = "v1" }),
            ("short semantic version", candidate with { QueryVersion = "1.0" }),
            ("leading zero semantic version", candidate with { PackVersion = "01.0.0" }),
            ("prerelease semantic version", candidate with { QueryVersion = "1.0.0-alpha" }),
            ("unicode version digit", candidate with { PackVersion = "1.\u0661" }),
            ("wildcard query version", candidate with { QueryVersion = "1.*" }),
            ("wildcard policy version", candidate with { Policy = candidate.Policy with { Version = "?" } }),
            ("empty policy", candidate with { Policy = candidate.Policy with { Id = "" } }),
            ("invalid policy digest", candidate with { Policy = candidate.Policy with { Sha256 = "bad" } }),
            ("empty minimum read", candidate with { MinimumReadSetId = "" }),
            ("wildcard minimum version", candidate with { MinimumReadSetVersion = "1.x" }),
            ("missing uniqueness review", candidate with { KeyUniquenessReviewReference = "" }),
            ("missing query review", candidate with { QueryReviewReference = "" }),
            ("control in review", candidate with { QueryReviewReference = "opaque\nsecret" }),
            ("whitespace category", candidate with { Category = " leading" }),
            ("invalid schema", candidate with { Schema = "dbo;drop" }),
            ("invalid table", candidate with { Table = "" }),
            ("invalid key", candidate with { StableKey = "bad.key" }),
            ("null fields", candidate with { Fields = null! }),
            ("null parameters", candidate with { Parameters = null! }),
            ("null policy", candidate with { Policy = null! }),
            ("null applicability", candidate with { Applicability = null! }),
            ("null builds", candidate with { Applicability = candidate.Applicability with { SupportedExactBuilds = null! } }),
            ("duplicate builds", candidate with { Applicability = candidate.Applicability with { SupportedExactBuilds = ["10.0.0.100", "10.0.0.100"] } }),
            ("wildcard build", candidate with { Applicability = candidate.Applicability with { SupportedExactBuilds = ["10.x"] } }),
            ("null versions", candidate with { Applicability = candidate.Applicability with { SupportedExactModuleVersions = null! } }),
            ("duplicate versions", candidate with { Applicability = candidate.Applicability with { SupportedExactModuleVersions = ["10.0.0.100", "10.0.0.100"] } }),
            ("wildcard module version", candidate with { Applicability = candidate.Applicability with { SupportedExactModuleVersions = ["10.*"] } }),
            ("empty SQL", candidate with { Sql = "" }),
            ("null SQL", candidate with { Sql = null! }),
            ("invalid SQL digest", candidate with { SqlSha256 = new string('z', 64) })
        }) Check(name, item, expected, QueryPackPreflightDecision.InvalidInput);

        foreach (var (name, item) in new[]
        {
            ("pack mismatch", candidate with { PackId = Guid.Parse("976667e2-6525-4cd3-aa71-5347e9537002") }),
            ("pack version mismatch", candidate with { PackVersion = "1.0.1" }),
            ("query mismatch", candidate with { QueryId = "other" }),
            ("query version mismatch", candidate with { QueryVersion = "1.0.1" }),
            ("applicability query mismatch", candidate with { Applicability = candidate.Applicability with { QueryId = "other" } }),
            ("policy mismatch", candidate with { Policy = candidate.Policy with { Id = "other" } }),
            ("policy version mismatch", candidate with { Policy = candidate.Policy with { Version = "1.0.1" } }),
            ("policy digest mismatch", candidate with { Policy = candidate.Policy with { Sha256 = new string('c', 64) } }),
            ("minimum mismatch", candidate with { MinimumReadSetId = "other" }),
            ("minimum version mismatch", candidate with { MinimumReadSetVersion = "1.0.1" })
        }) Check(name, item, expected, QueryPackPreflightDecision.BindingMismatch);

        Check("SQL changed", candidate with { Sql = sql + " ASC" }, expected, QueryPackPreflightDecision.SqlDigestMismatch);
        // Python hashlib over the explicit U+FFFD UTF-8 bytes calculates this
        // replacement-byte digest independently. Neither invalid UTF-16 string
        // may be silently repaired to the signed SQL text those bytes describe.
        const string replacementDigest = "f89d54232f907e7750038eebb2a533f1c136145a07ab9fbd7be30ab6dbdaf412";
        foreach (var malformedSql in new[] { sql + " --\uD800", sql + " --\uDC00" })
            Check("unpaired surrogate", candidate with { Sql = malformedSql, SqlSha256 = replacementDigest },
                expected with { SqlSha256 = replacementDigest }, QueryPackPreflightDecision.InvalidInput);
        const string pairedDigest = "7fc61d50160c39d142a787fa3d066bb6b6461cd3e20c68d6b8647ce0d99f2fe5";
        Check("valid surrogate pair", candidate with { Sql = sql + " --\uD83D\uDE00", SqlSha256 = pairedDigest },
            expected with { SqlSha256 = pairedDigest }, QueryPackPreflightDecision.StructurallyReady);
        Check("digest changed", candidate with { SqlSha256 = new string('a', 64) }, expected, QueryPackPreflightDecision.SqlDigestMismatch);
        Check("trusted digest mismatch", candidate, expected with { SqlSha256 = new string('a', 64) }, QueryPackPreflightDecision.SqlDigestMismatch);
        Check("different build", candidate, expected with { Source = expected.Source with { ExactBuild = "10.0.0.101" } }, QueryPackPreflightDecision.UnsupportedSource);
        Check("absent module", candidate, expected with { Source = expected.Source with { InstalledModules = [] } }, QueryPackPreflightDecision.UnsupportedSource);
        Check("different module version", candidate, expected with { Source = expected.Source with { InstalledModules = [new("FIC", "10.0.0.101")] } }, QueryPackPreflightDecision.UnsupportedSource);

        foreach (var (name, rejectedFields) in new (string, QueryPackField[])[]
        {
            ("no fields", []), ("null field", [null!]),
            ("duplicate field", [fields[0], fields[0], fields[1]]),
            ("case duplicate field", [fields[0], fields[0] with { Name = "uid" }]),
            ("key absent", [fields[1]]),
            ("nullable key", [fields[0] with { Nullable = true }, fields[1]]),
            ("unknown field", [fields[0], new("Other", "int", false, FieldClassification.ApprovedReference)]),
            ("unknown classification", [fields[0], fields[1] with { Classification = FieldClassification.Unknown }]),
            ("invalid classification", [fields[0], fields[1] with { Classification = (FieldClassification)999 }]),
            ("prohibited", [fields[0], fields[1] with { Classification = FieldClassification.Prohibited }]),
            ("redacted", [fields[0], fields[1] with { Classification = FieldClassification.Redacted }]),
            ("null type", [fields[0], fields[1] with { SqlType = null! }]),
            ("empty type", [fields[0], fields[1] with { SqlType = "" }]),
            ("MAX type", [fields[0], fields[1] with { SqlType = "nvarchar(max)" }]),
            ("unsupported type", [fields[0], fields[1] with { SqlType = "sql_variant" }]),
            ("malformed name", [fields[0], fields[1] with { Name = "secret.name" }])
        }) Check(name, candidate with { Fields = rejectedFields }, expected, QueryPackPreflightDecision.FieldRejected);
        Check("excluded category", candidate, expected with { Policy = policy with { ExcludedCategories = new HashSet<string> { "FictionalCategory" } } }, QueryPackPreflightDecision.FieldRejected);
        Check("nonallowlisted key", candidate, expected with { Policy = policy with { IncludedFields = new HashSet<FieldKey> { new("FictionalCategory", "State") } } }, QueryPackPreflightDecision.FieldRejected);

        foreach (var (name, rejectedParameters) in new (string, QueryPackParameter[])[]
        {
            ("no parameters", []), ("missing boundary", [parameters[0]]), ("null parameter", [parameters[0], null!]),
            ("extra parameter", [parameters[0], parameters[1], new("@Other", "int", false)]),
            ("duplicate parameter", [parameters[0], parameters[0]]),
            ("case duplicate parameter", [parameters[0], parameters[0] with { Name = "@pagesize" }]),
            ("nullable page", [parameters[0] with { Nullable = true }, parameters[1]]),
            ("nullable boundary", [parameters[0], parameters[1] with { Nullable = true }]),
            ("page type mismatch", [parameters[0] with { SqlType = "bigint" }, parameters[1]]),
            ("boundary type mismatch", [parameters[0], parameters[1] with { SqlType = "int" }]),
            ("unbound boundary", [parameters[0], parameters[1] with { Name = "@Other" }]),
            ("invalid parameter", [parameters[0], parameters[1] with { Name = "@1" }])
        }) Check(name, candidate with { Parameters = rejectedParameters }, expected, QueryPackPreflightDecision.ParameterRejected);
        Check("same parameter", candidate with { BoundaryParameter = "@PageSize" }, expected, QueryPackPreflightDecision.ParameterRejected);

        foreach (var (name, item) in new[]
        {
            ("zero pages", candidate with { MaximumPageSize = 0 }), ("negative pages", candidate with { MaximumPageSize = -1 }),
            ("zero rows", candidate with { MaximumRows = 0 }), ("negative rows", candidate with { MaximumRows = -1 }),
            ("inconsistent rows", candidate with { MaximumRows = 99 }),
            ("zero duration", candidate with { MaximumDuration = TimeSpan.Zero }),
            ("negative duration", candidate with { MaximumDuration = TimeSpan.FromTicks(-1) })
        }) Check(name, item, expected, QueryPackPreflightDecision.BudgetRejected);

        foreach (var type in new[] { "int", "bigint", "smallint", "tinyint", "uniqueidentifier", "nvarchar(1)", "nvarchar(4000)", "varchar(8000)", "binary(8000)", "varbinary(1)" })
        {
            Check("supported type", candidate with
            {
                Fields = [fields[0] with { SqlType = type }, fields[1]],
                Parameters = [parameters[0], parameters[1] with { SqlType = type }]
            }, expected, QueryPackPreflightDecision.StructurallyReady);
        }
        foreach (var type in new[] { "INT", "varchar(0)", "varchar(8001)", "nvarchar(4001)", "nvarchar(01)", "varchar(+1)", "varchar( 1)", "varchar(999999999999)", "varbinary(max)", "float", "int);EXEC" })
            Check("unsupported type syntax", candidate with { Fields = [fields[0] with { SqlType = type }, fields[1]] }, expected, QueryPackPreflightDecision.FieldRejected);

        var unsafeSql = sql.Replace("[UID] > @After", "[UID] > @After OR 1 = 1");
        var unsafeDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(unsafeSql)));
        Check("bound unsafe SQL", candidate with { Sql = unsafeSql, SqlSha256 = unsafeDigest }, expected with { SqlSha256 = unsafeDigest }, QueryPackPreflightDecision.UnsafeSql);
        Check("null candidate", null, expected, QueryPackPreflightDecision.InvalidInput);
        Check("null expected", candidate, null, QueryPackPreflightDecision.InvalidInput);
        Check("null expected source", candidate, expected with { Source = null! }, QueryPackPreflightDecision.InvalidInput);
        Check("null expected policy", candidate, expected with { Policy = null! }, QueryPackPreflightDecision.InvalidInput);
        Check("null expected modules", candidate, expected with { Source = expected.Source with { InstalledModules = null! } }, QueryPackPreflightDecision.InvalidInput);
        Check("duplicate source module", candidate, expected with { Source = expected.Source with { InstalledModules = [modules[0], modules[0]] } }, QueryPackPreflightDecision.InvalidInput);
        Check("null source module", candidate, expected with { Source = expected.Source with { InstalledModules = [null!] } }, QueryPackPreflightDecision.InvalidInput);
        Check("wildcard source", candidate, expected with { Source = expected.Source with { ExactBuild = "10.x" } }, QueryPackPreflightDecision.InvalidInput);
        Check("malformed policy key", candidate, expected with { Policy = policy with { IncludedFields = new HashSet<FieldKey> { null! } } }, QueryPackPreflightDecision.InvalidInput);
        Check("null policy field set", candidate, expected with { Policy = policy with { IncludedFields = null! } }, QueryPackPreflightDecision.InvalidInput);
        Check("null policy category set", candidate, expected with { Policy = policy with { ExcludedCategories = null! } }, QueryPackPreflightDecision.InvalidInput);
        Check("bad trusted digest", candidate, expected with { SqlSha256 = "bad" }, QueryPackPreflightDecision.InvalidInput);
        Check("bad trusted version", candidate, expected with { QueryVersion = "*" }, QueryPackPreflightDecision.InvalidInput);
        if (fields.Count != 2 || parameters.Count != 2 || builds.Count != 1 || versions.Count != 1 || modules.Count != 1 ||
            included.Count != 2 || excluded.Count != 0 || candidate.Sql != sql || candidate.SqlSha256 != digest)
            throw new Exception("Caller collections changed.");
        count++;
        foreach (var protectedObject in new object[] { candidate, expected, candidate.Policy, fields[0], parameters[0] })
        {
            if (protectedObject.ToString() != protectedObject.GetType().Name) throw new Exception("Protected string disclosure.");
            count++;
        }
        return count;

        void Check(string name, QueryPackDescriptor? item, QueryPackExpectedBindings? bindings, QueryPackPreflightDecision decision)
        {
            var result = QueryPackPreflight.Evaluate(item, bindings);
            if (result.Decision != decision || result.StructurallyReady != (decision == QueryPackPreflightDecision.StructurallyReady))
                throw new Exception($"Query preflight fixture {name} failed: {result.Decision}.");
            if (result.ToString().Contains("SELECT", StringComparison.Ordinal) || result.ToString().Contains("fictional", StringComparison.Ordinal))
                throw new Exception("Decision string disclosure.");
            count++;
        }
    }
}
