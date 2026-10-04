using System.Text.Json;
using Npgsql;

internal static class SqlSurfaceVerifier
{
    internal static async Task<int> VerifyAsync(FixtureCustomer customer, CancellationToken cancellationToken)
    {
        using var expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "sql-surface.json")));
        var functions = expected.RootElement.GetProperty("functions").EnumerateArray().ToArray();
        await using var source = FixtureConfiguration.DataSource(customer.Database, customer.Role("Publisher"));
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        var observed = new Dictionary<string, ObservedFunction>(StringComparer.Ordinal);
        await using (var query = new NpgsqlCommand("""
            SELECT p.oid::bigint,n.nspname||'.'||p.proname,pg_get_function_identity_arguments(p.oid),
                   pg_get_function_result(p.oid),p.prosecdef,r.rolname,p.proconfig,
                   EXISTS(SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a
                          WHERE a.grantee=0 AND a.privilege_type='EXECUTE')
            FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace JOIN pg_roles r ON r.oid=p.proowner
            WHERE n.nspname IN ('report_publication','report_publication_fixture') ORDER BY n.nspname,p.proname,p.oid
            """, connection))
        {
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var signature = reader.GetString(1) + "(" + reader.GetString(2).Replace(", ", ",", StringComparison.Ordinal) + ")";
                observed.Add(signature, new(reader.GetInt64(0), reader.GetString(3), reader.GetBoolean(4), reader.GetString(5),
                    reader.IsDBNull(6) ? [] : reader.GetFieldValue<string[]>(6), reader.GetBoolean(7)));
            }
        }
        var checks = 0;
        foreach (var item in functions)
        {
            var signature = item.GetProperty("signature").GetString()!;
            Require(observed.TryGetValue(signature, out var actual)); checks++;
            Require(actual!.ReturnShape == item.GetProperty("returns").GetString()); checks++;
            Require(actual.SecurityDefiner && actual.Settings.SequenceEqual(new[] { "search_path=pg_catalog" })); checks++;
            Require(actual.Owner == customer.Role(signature.StartsWith("report_publication_fixture.", StringComparison.Ordinal) ? "FixtureOwner" : "PublicationOwner")); checks++;
            Require(!actual.PublicExecute); checks++;
            var allowed = item.GetProperty("granted").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            foreach (var login in FixtureConfiguration.Customers.SelectMany(c => FixtureConfiguration.Kinds.Take(6).Select(k => (Customer: c, Kind: k, Role: c.Role(k)))))
            {
                await using var check = new NpgsqlCommand("SELECT has_function_privilege($1,$2::oid,'EXECUTE')", connection);
                check.Parameters.AddWithValue(login.Role); check.Parameters.AddWithValue(actual.Oid);
                var canExecute = (bool)(await check.ExecuteScalarAsync(cancellationToken))!;
                Require(canExecute == (login.Customer == customer && allowed.Contains(login.Kind))); checks++;
            }
        }
        var closed = functions.Select(x => x.GetProperty("signature").GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var helper in observed.Where(x => !closed.Contains(x.Key)))
        {
            Require(!helper.Value.PublicExecute); checks++;
            foreach (var role in FixtureConfiguration.Customers.SelectMany(c => FixtureConfiguration.Kinds.Take(6).Select(c.Role)))
            {
                await using var query = new NpgsqlCommand("SELECT has_function_privilege($1,$2::oid,'EXECUTE')", connection);
                query.Parameters.AddWithValue(role); query.Parameters.AddWithValue(helper.Value.Oid);
                Require(!(bool)(await query.ExecuteScalarAsync(cancellationToken))!); checks++;
            }
        }
        foreach (var type in expected.RootElement.GetProperty("compositeReturns").EnumerateArray())
        {
            var name = type.GetProperty("name").GetString()!;
            var fields = new List<(string Name, string Type)>();
            await using var query = new NpgsqlCommand("""
                SELECT a.attname,format_type(a.atttypid,a.atttypmod)
                FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace JOIN pg_attribute a ON a.attrelid=t.typrelid
                WHERE n.nspname||'.'||t.typname=$1 AND a.attnum>0 AND NOT a.attisdropped ORDER BY a.attnum
                """, connection);
            query.Parameters.AddWithValue(name);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) fields.Add((reader.GetString(0), reader.GetString(1)));
            Require(fields.SequenceEqual(type.GetProperty("attributes").EnumerateArray().Select(x => (x.GetProperty("name").GetString()!, x.GetProperty("type").GetString()!)))); checks++;
        }
        return checks;
    }
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Frozen independent SQL surface mismatch."); }
    private sealed record ObservedFunction(long Oid, string ReturnShape, bool SecurityDefiner, string Owner, string[] Settings, bool PublicExecute);
}
