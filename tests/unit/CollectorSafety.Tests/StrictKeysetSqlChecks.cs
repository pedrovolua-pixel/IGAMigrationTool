using CollectorSafety;

internal static class StrictKeysetSqlChecks
{
    internal static int Run()
    {
        const string sql = "SELECT TOP (@PageSize) [UID], [State] FROM [dbo].[FictionalArtifact] WHERE [UID] > @After ORDER BY [UID]";
        var policy = new StrictKeysetSqlPolicy("dbo", "FictionalArtifact", ["UID", "State"], "UID", "@PageSize", "@After");
        var count = 0;
        Check("implicit ascending", sql, StrictKeysetSqlDecision.StructurallyReady);
        Check("explicit ascending", sql + " ASC", StrictKeysetSqlDecision.StructurallyReady);
        Check("predicate parentheses", sql.Replace("[UID] > @After", "(([UID]) > (@After))"), StrictKeysetSqlDecision.StructurallyReady);
        Check("unbracketed", sql.Replace("[", "").Replace("]", ""), StrictKeysetSqlDecision.StructurallyReady);
        Check("table qualified", sql.Replace("[UID]", "[FictionalArtifact].[UID]").Replace("[State]", "[FictionalArtifact].[State]"), StrictKeysetSqlDecision.StructurallyReady);
        Check("aliased qualifier", sql.Replace("[UID]", "[a].[UID]").Replace("[State]", "[a].[State]")
            .Replace("FROM [dbo].[FictionalArtifact]", "FROM [dbo].[FictionalArtifact] AS [a]"), StrictKeysetSqlDecision.StructurallyReady);
        Check("TOP parentheses", sql.Replace("TOP (@PageSize)", "TOP ((@PageSize))"), StrictKeysetSqlDecision.StructurallyReady);
        Check("projection order", sql.Replace("[UID], [State]", "[State], [UID]"), StrictKeysetSqlDecision.StructurallyReady);
        Check("valid surrogate pair", sql + " --\uD83D\uDE00", StrictKeysetSqlDecision.StructurallyReady);
        Check("lone high surrogate", sql + " --\uD800", StrictKeysetSqlDecision.InvalidInput);
        Check("lone low surrogate", sql + " --\uDC00", StrictKeysetSqlDecision.InvalidInput);

        foreach (var (name, predicate) in new[]
        {
            ("OR tautology", "[UID] > @After OR 1 = 1"),
            ("AND filter", "[UID] > @After AND [State] > 0"),
            ("wrong key", "[State] > @After"),
            ("wrong qualifier", "[unknown].[UID] > @After"),
            ("inclusive", "[UID] >= @After"),
            ("reversed", "@After < [UID]"),
            ("descending predicate", "[UID] < @After"),
            ("equal predicate", "[UID] = @After"),
            ("arithmetic", "[UID] > @After + 1"),
            ("parameter null", "@After IS NULL"),
            ("function", "LEN([UID]) > @After"),
            ("literal", "[UID] > 0"),
            ("global", "[UID] > @@VERSION"),
            ("unknown parameter", "[UID] > @Wrong"),
            ("scalar subquery", "[UID] > (SELECT @After)")
        }) Check(name, sql.Replace("[UID] > @After", predicate), StrictKeysetSqlDecision.UnsafeSql);

        foreach (var (name, candidate) in new[]
        {
            ("descending order", sql + " DESC"),
            ("multiple order keys", sql + ", [State]"),
            ("mismatched order", sql.Replace("ORDER BY [UID]", "ORDER BY [State]")),
            ("wrong order qualifier", sql.Replace("ORDER BY [UID]", "ORDER BY [unknown].[UID]")),
            ("no order", sql.Replace(" ORDER BY [UID]", "")),
            ("order ordinal", sql.Replace("ORDER BY [UID]", "ORDER BY 1")),
            ("absent projected key", sql.Replace("[UID], [State]", "[State]")),
            ("duplicate projected key", sql.Replace("[UID], [State]", "[UID], [UID], [State]")),
            ("alias projected key", sql.Replace("[UID], [State]", "[UID] AS [Renamed], [State]")),
            ("unknown projection qualifier", sql.Replace("[UID], [State]", "[unknown].[UID], [State]")),
            ("missing descriptor field", sql.Replace("[UID], [State]", "[UID]")),
            ("extra field", sql.Replace("[UID], [State]", "[UID], [State], [Password]")),
            ("DISTINCT", sql.Replace("SELECT TOP", "SELECT DISTINCT TOP")),
            ("ALL", sql.Replace("SELECT TOP", "SELECT ALL TOP")),
            ("multiple statements", sql + "; DELETE FROM [dbo].[FictionalArtifact]"),
            ("SELECT INTO", sql.Replace(" FROM", " INTO [dbo].[Copy] FROM")),
            ("EXEC", "EXEC [dbo].[ReadAnything]"),
            ("UPDATE", "UPDATE [dbo].[FictionalArtifact] SET [State] = @After"),
            ("dynamic SQL", "EXEC(@After)"),
            ("wildcard", sql.Replace("[UID], [State]", "*")),
            ("unbounded", sql.Replace("TOP (@PageSize) ", "")),
            ("literal TOP", sql.Replace("TOP (@PageSize)", "TOP (100)")),
            ("TOP percent", sql.Replace("TOP (@PageSize)", "TOP (@PageSize) PERCENT")),
            ("TOP ties", sql.Replace("TOP (@PageSize)", "TOP (@PageSize) WITH TIES")),
            ("wrong table", sql.Replace("[FictionalArtifact]", "[WrongTable]")),
            ("cross database", sql.Replace("[dbo].[FictionalArtifact]", "[Other].[dbo].[FictionalArtifact]")),
            ("table hint", sql.Replace(" WHERE", " WITH (NOLOCK) WHERE")),
            ("FOR XML", sql + " FOR XML PATH"),
            ("offset", sql + " OFFSET 0 ROWS"),
            ("grouping", sql.Replace(" ORDER BY", " GROUP BY [UID], [State] ORDER BY")),
            ("join", sql.Replace(" WHERE", " JOIN [dbo].[Other] b ON [UID] = b.[UID] WHERE")),
            ("malformed", "SELECT TOP (")
        }) Check(name, candidate, StrictKeysetSqlDecision.UnsafeSql);

        Check("null SQL", null, StrictKeysetSqlDecision.InvalidInput);
        Check("empty SQL", "", StrictKeysetSqlDecision.InvalidInput);
        CheckPolicy("null policy", null);
        CheckPolicy("null columns", policy with { ProjectedColumns = null! });
        CheckPolicy("empty columns", policy with { ProjectedColumns = [] });
        CheckPolicy("duplicate columns", policy with { ProjectedColumns = ["UID", "UID"] });
        CheckPolicy("case duplicate columns", policy with { ProjectedColumns = ["UID", "uid"] });
        CheckPolicy("null column", policy with { ProjectedColumns = ["UID", null!] });
        CheckPolicy("unprojected key", policy with { StableKey = "Other" });
        CheckPolicy("empty table", policy with { Table = "" });
        CheckPolicy("identifier punctuation", policy with { Schema = "dbo;secret" });
        CheckPolicy("oversized identifier", policy with { StableKey = new string('a', 129) });
        CheckPolicy("invalid parameter", policy with { PageSizeParameter = "@1" });
        CheckPolicy("duplicate parameters", policy with { BoundaryParameter = "@PageSize" });
        CheckPolicy("case duplicate parameters", policy with { BoundaryParameter = "@pagesize" });
        if (policy.ToString() != nameof(StrictKeysetSqlPolicy)) throw new Exception("Protected policy string disclosure.");
        count++;
        return count;

        void Check(string name, string? candidate, StrictKeysetSqlDecision expected)
        {
            if (StrictKeysetSqlValidator.Evaluate(candidate, policy) != expected)
                throw new Exception($"Strict SQL fixture {name} failed.");
            count++;
        }
        void CheckPolicy(string name, StrictKeysetSqlPolicy? candidate)
        {
            if (StrictKeysetSqlValidator.Evaluate(sql, candidate) != StrictKeysetSqlDecision.InvalidInput)
                throw new Exception($"Strict policy fixture {name} failed.");
            count++;
        }
    }
}
