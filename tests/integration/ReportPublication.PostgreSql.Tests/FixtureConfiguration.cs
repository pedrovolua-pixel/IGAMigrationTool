using System.Text.Json;
using Npgsql;

internal sealed record FixtureCustomer(string Database, string Suffix)
{
    internal string Role(string kind) => kind switch
    {
        "Publisher" => "rp_publisher_" + Suffix,
        "Reader" => "rp_reader_" + Suffix,
        "Audit" => "rp_audit_" + Suffix,
        "BlobStager" => "rp_blob_stager_" + Suffix,
        "FixtureController" => "rp_fixture_controller_" + Suffix,
        "Migration" => "rp_migration_" + Suffix,
        "PublicationOwner" => "rp_publication_owner_" + Suffix,
        "FixtureOwner" => "rp_fixture_owner_" + Suffix,
        _ => throw new InvalidOperationException("Unknown fixture role kind.")
    };
}

internal static class FixtureConfiguration
{
    internal const string ReceiptPath = "/Users/pedrovolu/Documents/ChatGPT/IGAMigrationTool/work/release-readiness-20261003/native-p05b-owner-receipt.json";
    internal const string DataDirectory = "/private/tmp/iga-rr-ui-pg-20261004";
    internal const int Port = 56284;
    internal const string Provisioner = "iga_rr_ui_runner";
    internal static readonly FixtureCustomer[] Customers =
    [
        new("iga_synthetic_native_publication_p05b_20261004_a1_a", "p05b_20261004_a1_a"),
        new("iga_synthetic_native_publication_p05b_20261004_a1_b", "p05b_20261004_a1_b")
    ];
    internal static readonly string[] Kinds = ["Publisher", "Reader", "Audit", "BlobStager", "FixtureController", "Migration", "PublicationOwner", "FixtureOwner"];
    internal static NpgsqlDataSource DataSource(string database, string role)
    {
        if (database != "postgres" && !Customers.Any(x => x.Database == database)) throw new InvalidOperationException("Unowned fixture database.");
        if (role != Provisioner && !Customers.SelectMany(x => Kinds.Select(x.Role)).Contains(role, StringComparer.Ordinal))
            throw new InvalidOperationException("Unowned fixture identity.");
        var options = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = Port,
            Database = database,
            Username = role,
            Pooling = false,
            Timeout = 5,
            CommandTimeout = 10,
            IncludeErrorDetail = false,
            ApplicationName = "RR-P05B-independent"
        };
        return NpgsqlDataSource.Create(options.ConnectionString);
    }
    internal static void CheckReceipt()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(ReceiptPath));
        var root = document.RootElement;
        // Receipt identity is positive evidence, never a grant from request-selected configuration.
        Require(root.GetProperty("packetCommit").GetString() == "a3e7c5d");
        Require(root.GetProperty("port").GetInt32() == Port);
        Require(root.GetProperty("dataDirectory").GetString() == DataDirectory);
        Require(root.GetProperty("provisioner").GetString() == Provisioner);
        var databases = root.GetProperty("databases").EnumerateArray().Select(x => x.GetString()).ToArray();
        Require(databases.SequenceEqual(Customers.Select(x => x.Database)));
        var roles = root.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).Order(StringComparer.Ordinal).ToArray();
        Require(roles.SequenceEqual(Customers.SelectMany(x => Kinds.Select(x.Role)).Order(StringComparer.Ordinal)));
    }
    private static void Require(bool condition)
    { if (!condition) throw new InvalidOperationException("Owned fixture receipt mismatch."); }
}
