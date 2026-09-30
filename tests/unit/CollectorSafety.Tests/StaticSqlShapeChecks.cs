using CollectorSafety;

internal static class StaticSqlShapeChecks
{
    internal static int Run()
    {
        const string safe = "SELECT TOP (@PageSize) [UID] FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]";
        var policy = new StaticSqlShapePolicy("dbo", "Person", ["UID"], "@PageSize", "@After");
        var count = 0;

        Check("bounded SELECT", StaticSqlShapeDecision.StructurallyReadOnlyPage, safe, policy);
        Check("multiple statements", StaticSqlShapeDecision.MultipleStatements,
            safe + "; DELETE FROM [dbo].[Person]", policy);
        Check("SELECT INTO", StaticSqlShapeDecision.UnsafeShape,
            "SELECT TOP (@PageSize) [UID] INTO [dbo].[Copy] FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]", policy);
        Check("stored procedure", StaticSqlShapeDecision.UnsafeShape,
            "EXEC [dbo].[GetPeople]", policy);
        Check("update", StaticSqlShapeDecision.UnsafeShape,
            "UPDATE [dbo].[Person] SET [UID] = @After", policy);
        Check("dynamic SQL", StaticSqlShapeDecision.UnsafeShape,
            "EXEC(@After)", policy);
        Check("wildcard projection", StaticSqlShapeDecision.UnsafeShape,
            "SELECT TOP (@PageSize) * FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]", policy);
        Check("unbounded SELECT", StaticSqlShapeDecision.UnsafeShape,
            "SELECT [UID] FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]", policy);
        Check("literal TOP", StaticSqlShapeDecision.UnsafeShape,
            "SELECT TOP (1000) [UID] FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]", policy);
        Check("TOP percent", StaticSqlShapeDecision.UnsafeShape,
            "SELECT TOP (10) PERCENT [UID] FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]", policy);
        Check("TOP ties", StaticSqlShapeDecision.UnsafeShape,
            "SELECT TOP (@PageSize) WITH TIES [UID] FROM [dbo].[Person] WHERE [UID] > @After ORDER BY [UID]", policy);
        Check("wrong table", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("[Person]", "[Secret]", StringComparison.Ordinal), policy);
        Check("cross-database table", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("[dbo].[Person]", "[other].[dbo].[Person]", StringComparison.Ordinal), policy);
        Check("unapproved field", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("SELECT TOP (@PageSize) [UID]", "SELECT TOP (@PageSize) [Password]", StringComparison.Ordinal), policy);
        Check("function in WHERE", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("[UID] > @After", "LEN([UID]) > @After", StringComparison.Ordinal), policy);
        Check("global variable", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("[UID] > @After", "[UID] > @@VERSION", StringComparison.Ordinal), policy);
        Check("unknown variable", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("@After", "@Other", StringComparison.Ordinal), policy);
        Check("missing boundary", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("[UID] > @After", "[UID] > 0", StringComparison.Ordinal), policy);
        Check("missing ORDER BY", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace(" ORDER BY [UID]", "", StringComparison.Ordinal), policy);
        Check("ORDER BY ordinal", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("ORDER BY [UID]", "ORDER BY 1", StringComparison.Ordinal), policy);
        Check("table hint", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("FROM [dbo].[Person]", "FROM [dbo].[Person] WITH (NOLOCK)", StringComparison.Ordinal), policy);
        Check("FOR XML aggregation", StaticSqlShapeDecision.UnsafeShape,
            safe + " FOR XML PATH", policy);
        Check("offset addition", StaticSqlShapeDecision.UnsafeShape,
            safe + " OFFSET 0 ROWS", policy);
        Check("grouping", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace(" ORDER BY [UID]", " GROUP BY [UID] ORDER BY [UID]", StringComparison.Ordinal), policy);
        Check("join", StaticSqlShapeDecision.UnsafeShape,
            safe.Replace("FROM [dbo].[Person]", "FROM [dbo].[Person] p JOIN [dbo].[Other] o ON p.[UID] = o.[UID]", StringComparison.Ordinal), policy);
        Check("malformed SQL", StaticSqlShapeDecision.ParseFailure, "SELECT TOP (", policy);
        Check("empty SQL", StaticSqlShapeDecision.InvalidInput, "", policy);
        Check("empty column policy", StaticSqlShapeDecision.InvalidInput, safe,
            policy with { AllowedColumns = [] });

        return count;

        void Check(string name, StaticSqlShapeDecision expected,
            string? sql, StaticSqlShapePolicy? candidatePolicy)
        {
            var actual = StaticSqlShapeValidator.Evaluate(sql, candidatePolicy);
            if (actual != expected)
            {
                throw new Exception($"{name}: expected {expected}, got {actual}.");
            }

            count++;
        }
    }
}
