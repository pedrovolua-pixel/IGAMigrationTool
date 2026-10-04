using Npgsql;

internal static class OwnedProvisioning
{
    internal static async Task CheckFreshAbsenceAsync(CancellationToken cancellationToken)
    {
        FixtureConfiguration.CheckReceipt();
        await using var source = FixtureConfiguration.DataSource("postgres", FixtureConfiguration.Provisioner);
        await using var connection = await source.OpenConnectionAsync(cancellationToken);
        await using (var identity = new NpgsqlCommand("SELECT current_user, current_setting('data_directory'), current_setting('server_version_num'), inet_server_port()", connection))
        {
            await using var reader = await identity.ExecuteReaderAsync(cancellationToken);
            Require(await reader.ReadAsync(cancellationToken));
            Require(reader.GetString(0) == FixtureConfiguration.Provisioner);
            Require(reader.GetString(1) == FixtureConfiguration.DataDirectory);
            Require(reader.GetString(2) == "180004");
            Require(reader.GetInt32(3) == FixtureConfiguration.Port);
        }
        await using (var probe = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=ANY($1)", connection))
        {
            probe.Parameters.AddWithValue(FixtureConfiguration.Customers.Select(x => x.Database).ToArray());
            Require((long)(await probe.ExecuteScalarAsync(cancellationToken))! == 0);
        }
        await using (var probe = new NpgsqlCommand("SELECT count(*) FROM pg_roles WHERE rolname=ANY($1)", connection))
        {
            probe.Parameters.AddWithValue(FixtureConfiguration.Customers.SelectMany(x => FixtureConfiguration.Kinds.Select(x.Role)).ToArray());
            Require((long)(await probe.ExecuteScalarAsync(cancellationToken))! == 0);
        }
        Console.WriteLine("PASS: receipt-bound healthy PostgreSQL 18.4/owned port; two database and sixteen role names absent. No setup mutation.");
    }
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Owned PostgreSQL setup prerequisite failed."); }
}
