using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using SyntheticEvaluation;
using SyntheticEvaluationWorkflow;
using SyntheticEvaluationWorkflowFixtures;

internal static class Program
{
    private static int checks;
    private static readonly EvaluationWorkflowActor Actor = new("synthetic-reviewer");
    private static readonly string Connection = Environment.GetEnvironmentVariable("IGA_EVALUATION_TEST_CONNECTION")
        ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_evaluation_wf04_domain;Username=iga_synthetic";
    private static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    private static async Task<int> Main()
    {
        try { await Run(); Console.WriteLine($"PASS {checks} synthetic evaluation workflow PostgreSQL18 assertions."); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static EvaluationWorkflowCommand Command(EvaluationWorkflowSnapshot workspace, string member,
        EvaluationReviewOutcome outcome = EvaluationReviewOutcome.Confirmed, EvaluationOriginClassification? origin = null, EvaluationWorkflowCorrection? correction = null)
    {
        var m = workspace.Members.Single(m => m.Original.MemberId == member);
        return new(Guid.NewGuid(), member, EvaluationWorkflowCommandKind.Review, workspace.AggregateRevision, m.Revision,
            workspace.RegistryVersionId, workspace.SourceDigest, workspace.SampleDigest, outcome, origin,
            "Independent fictional reviewed conclusion.", m.Original.EvidenceReferenceIds, correction);
    }
    private static async Task<EvaluationWorkflowSnapshot> Read(SyntheticEvaluationWorkflowStore store)
    {
        var r = await store.ReadAsync(Actor); Check(r.Issue is null && r.Snapshot is not null, "readauthorized"); return r.Snapshot!;
    }
    private static async Task<long[]> Counts()
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("""
          SELECT (SELECT count(*) FROM synthetic_evaluation_workflow.events),
          (SELECT count(*) FROM synthetic_evaluation_workflow.versions),
          (SELECT count(*) FROM synthetic_evaluation_workflow.registries),
          (SELECT aggregate_revision FROM synthetic_evaluation_workflow.workspace WHERE singleton)
          """, c);
        await using var r = await cmd.ExecuteReaderAsync(); await r.ReadAsync();
        return [r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3)];
    }
    private static async Task Sql(string query, params (string Name, object Value)[] values)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var cmd = new NpgsqlCommand(query, c);
        foreach (var p in values) cmd.Parameters.AddWithValue(p.Name, p.Value);
        await cmd.ExecuteNonQueryAsync();
    }
    private static async Task<string> Scalar(string query)
    {
        await using var c = new NpgsqlConnection(Connection); await c.OpenAsync(); await using var cmd = new NpgsqlCommand(query, c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
    private static void General(EvaluationWorkflowVersion version, int confirmed, int rejected, int indeterminate, int unreviewed, int corrected)
    {
        using var doc = JsonDocument.Parse(version.WarningCanonicalJson);
        var g = doc.RootElement.GetProperty("summaries").GetProperty("generalAi");
        Check(g.GetProperty("selected").GetInt32() == 100 && g.GetProperty("confirmed").GetInt32() == confirmed &&
            g.GetProperty("rejected").GetInt32() == rejected && g.GetProperty("indeterminate").GetInt32() == indeterminate &&
            g.GetProperty("unreviewed").GetInt32() == unreviewed && g.GetProperty("corrected").GetInt32() == corrected &&
            g.GetProperty("denominator").GetInt32() == confirmed + rejected, "literalcounts");
        Check(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(version.SnapshotCanonicalJson))) == version.ContentDigest, "independentrawhash");
        using var manifest = JsonDocument.Parse(version.VersionManifestJson);
        Check(manifest.RootElement.GetProperty("sampleDigest").GetString() == "5a372640cd648d266cf0112f69e2510aa3751cc81d6e61c3321870461d0ea110", "sampleforeverfixed");
    }
    private static EvaluationReviewerRegistryInput Next(EvaluationReviewerRegistryInput registry, string version,
        Func<EvaluationFixtureAssignment, EvaluationFixtureAssignment>? assignment = null,
        Func<EvaluationFixtureMember, EvaluationFixtureMember>? member = null, bool revokeIdentity = false)
    {
        return registry with
        {
            VersionId = version,
            Identities = registry.Identities.Select(i => revokeIdentity ? i with { Revision = i.Revision + 1, State = EvaluationFixtureState.Revoked } : i).ToArray(),
            Assignments = registry.Assignments.Select(a => assignment?.Invoke(a) ?? a).ToArray(),
            Members = registry.Members.Select(m => member?.Invoke(m) ?? m).ToArray()
        };
    }
    private static async Task Run()
    {
        var seed = FictionalEvaluationFixture.BuildSeed();
        var store = new SyntheticEvaluationWorkflowStore(Connection);
        await store.InitializeAsync(); await store.InitializeAsync();
        var seeded = await store.SeedAsync(seed);
        Check(seeded.Issue is null && !seeded.AlreadySeeded, "freshseedrequiresdedicatedunseededdb");
        Check((await store.SeedAsync(seed)).AlreadySeeded, "idempotentseed");
        Check((await store.SeedAsync(seed with { Originals = seed.Originals.Select((o, i) => i == 0 ? o with { Title = "Changed fictional source" } : o).ToArray() })).Issue == EvaluationWorkflowIssue.SeedConflict, "changedseedrefused");
        var workspace = await Read(store); var originalJson = JsonSerializer.Serialize(workspace.Members.Select(m => m.Original).ToArray());
        var ids = workspace.Members.Select(m => m.Original.MemberId).ToArray();
        Check(ids.Length == 100 && workspace.AggregateRevision == 0 && workspace.Versions.Count == 1, "initialmembership");
        var v0 = (await store.ReadVersionAsync(0, Actor)).Version!; General(v0, 0, 0, 0, 100, 0);
        var initialCounts = await Counts(); await store.ReadAsync(Actor); await store.ReadVersionAsync(0, Actor); await store.ReadHistoryAsync(ids[0], 0, Actor);
        Check(initialCounts.AsEnumerable().SequenceEqual(await Counts()), "readonlynowrites");
        var rejected = await store.ApplyAsync(Command(workspace, ids[0]) with { Reason = "\ud800" }, Actor);
        Check(rejected.Issue == EvaluationWorkflowIssue.InvalidInput && rejected.Receipt is null, "UTF16invalid");
        Check(initialCounts.AsEnumerable().SequenceEqual(await Counts()), "invalidnowrites");
        var unknown = await store.ReadMemberAsync("synthetic-unknown", Actor);
        Check(unknown.Issue == EvaluationWorkflowIssue.NotFound && unknown.Member is null, "knownactorunknownmember");
        Check((await store.ReadMemberAsync("synthetic-unknown", new("synthetic-other"))).Issue == EvaluationWorkflowIssue.Denied, "identitybeforeNotFound");
        var c1 = Command(workspace, ids[0]);
        var r1 = await store.ApplyAsync(c1, Actor); Check(r1.Issue is null && !r1.AlreadyApplied, "confirmedcommit");
        var replay = await store.ApplyAsync(c1, Actor); Check(replay.AlreadyApplied && replay.Receipt == r1.Receipt, "exactdurableretry");
        Check((await store.ApplyAsync(c1 with { Reason = "different" }, Actor)).Issue == EvaluationWorkflowIssue.EventConflict, "semanticreuseconflict");
        workspace = await Read(store);
        General((await store.ReadVersionAsync(1, Actor)).Version!, 1, 0, 0, 99, 0);
        Check((await store.ApplyAsync(Command(workspace, ids[1]) with { ExpectedAggregateRevision = 0 }, Actor)).Issue == EvaluationWorkflowIssue.RevisionConflict, "stalerevision");
        Check((await store.ApplyAsync(Command(workspace, ids[1]) with { ExpectedRegistryVersionId = "synthetic-stale" }, Actor)).Issue == EvaluationWorkflowIssue.RegistryConflict, "staleregistry");
        Check((await store.ApplyAsync(Command(workspace, ids[1]) with { ExpectedSourceDigest = new('a', 64) }, Actor)).Issue == EvaluationWorkflowIssue.SourceConflict, "sourcefence");
        Check((await store.ApplyAsync(Command(workspace, ids[1], EvaluationReviewOutcome.Rejected), Actor)).Issue is null, "rejectedcommit");
        General((await store.ReadVersionAsync(2, Actor)).Version!, 1, 1, 0, 98, 0);
        var registry = Next(seed.Registry, "synthetic-registry-v2", member: m => m.Id == ids[2] ? m with { Revision = 2, AuthorizedContextSufficient = false } : m);
        Check((await store.UpdateRegistryAsync(seed.Registry.VersionId, registry)).Issue is null, "contextregistrycommit");
        workspace = await Read(store);
        Check((await store.ApplyAsync(Command(workspace, ids[2], EvaluationReviewOutcome.Indeterminate), Actor)).Issue is null, "eligibleinsufficientIndeterminate");
        General((await store.ReadVersionAsync(4, Actor)).Version!, 1, 1, 1, 97, 0);
        workspace = await Read(store);
        var dimensions = new EvaluationWorkflowCorrection("Medium", "Presentation category", "New contextual cause", "New presentation recommendation");
        Check((await store.ApplyAsync(Command(workspace, ids[3], EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Rejected, dimensions), Actor)).Issue is null, "correctedoriginrejected");
        General((await store.ReadVersionAsync(5, Actor)).Version!, 1, 2, 1, 96, 1);
        workspace = await Read(store);
        Check(JsonSerializer.Serialize(workspace.Members.Select(m => m.Original).ToArray()) == originalJson, "generatedoriginalsimmutable");
        var presentation = Command(workspace, ids[0]) with { Kind = EvaluationWorkflowCommandKind.PresentationCorrection, Outcome = null, Correction = dimensions, EvidenceReferenceIds = [] };
        Check((await store.ApplyAsync(presentation, Actor)).Issue is null, "presentationclassified");
        General((await store.ReadVersionAsync(6, Actor)).Version!, 1, 2, 1, 96, 2);
        workspace = await Read(store);
        var excluded = Command(workspace, ids[4]) with { Kind = EvaluationWorkflowCommandKind.PresentationCorrection, Outcome = null, Correction = dimensions, EvidenceReferenceIds = [] };
        Check((await store.ApplyAsync(excluded, Actor)).Issue is null, "presentationunreviewed");
        General((await store.ReadVersionAsync(7, Actor)).Version!, 1, 2, 1, 96, 2);
        workspace = await Read(store);
        var before = await Counts();
        var hookContext = Next(registry, "synthetic-hook-context", member: m => m.Id == ids[5] ? m with { Revision = m.Revision + 1, AuthorizedContextSufficient = false } : m);
        Check((await store.ApplyWithRegistryReplacementForTestAsync(Command(workspace, ids[5]), Actor, hookContext)).Issue == EvaluationWorkflowIssue.Denied, "precommitcontextflipdenied");
        Check(before.AsEnumerable().SequenceEqual(await Counts()), "hookrollbackallincludingregistry");
        Check((await store.ApplyWithRegistryReplacementForTestAsync(Command(workspace, ids[5], EvaluationReviewOutcome.Corrected, EvaluationOriginClassification.Confirmed, dimensions), Actor, hookContext)).Issue == EvaluationWorkflowIssue.Denied, "precommitcorrectedcontextflipdenied");
        var hookSufficient = Next(registry, "synthetic-hook-sufficient", member: m => m.Id == ids[2] ? m with { Revision = m.Revision + 1, AuthorizedContextSufficient = true } : m);
        Check((await store.ApplyWithRegistryReplacementForTestAsync(Command(workspace, ids[2], EvaluationReviewOutcome.Indeterminate), Actor, hookSufficient)).Issue == EvaluationWorkflowIssue.Denied, "precommitIndeterminatecontextflipdenied");
        Check(before.AsEnumerable().SequenceEqual(await Counts()), "allcontextchecksrollback");
        var hookRevoke = Next(registry, "synthetic-hook-revoke", revokeIdentity: true);
        Check((await store.ApplyWithRegistryReplacementForTestAsync(Command(workspace, ids[5]), Actor, hookRevoke)).Issue == EvaluationWorkflowIssue.Denied, "precommitidentityrevokedenied");
        Check(before.AsEnumerable().SequenceEqual(await Counts()), "revokedhookrollbackall");
        var failing = new SyntheticEvaluationWorkflowStore(Connection, new ThrowingObserver());
        var command = Command(workspace, ids[5]); var threw = false;
        try { await failing.ApplyAsync(command, Actor); } catch (InvalidOperationException) { threw = true; }
        Check(threw && before.AsEnumerable().SequenceEqual(await Counts()), "observerfailurerollback");
        Check((await store.ApplyAsync(command, Actor)).Issue is null, "afterfailureexactnewcommandsucceeds");
        workspace = await Read(store);
        var cA = Command(workspace, ids[6]); var cB = Command(workspace, ids[7]);
        var race = await Task.WhenAll(store.ApplyAsync(cA, Actor), store.ApplyAsync(cB, Actor));
        Check(race.Count(r => r.Issue is null) == 1 && race.Count(r => r.Issue == EvaluationWorkflowIssue.RevisionConflict) == 1, "concurrentwriterslinearize");
        Check((await store.ApplyAsync(c1, Actor)).AlreadyApplied, "oldexactretryafterregistryadvance");
        var restarted = new SyntheticEvaluationWorkflowStore(Connection);
        Check((await restarted.SeedAsync(seed)).AlreadySeeded, "restartseeddoesnotresetregistry");
        workspace = await Read(restarted);
        Check(workspace.RegistryVersionId == registry.VersionId, "currentregistryretained");
        Check((await store.ReadVersionAsync(0, Actor)).Version == v0, "historicalversionbytesstable");
        before = await Counts();
        var row = await Scalar("SELECT current_json FROM synthetic_evaluation_workflow.members WHERE member_id='" + ids[0] + "'");
        await Sql("UPDATE synthetic_evaluation_workflow.members SET current_json='{}' WHERE member_id=@id", ("id", ids[0]));
        Check((await store.ReadAsync(Actor)).Issue == EvaluationWorkflowIssue.IntegrityMismatch, "directcurrenttampertyped");
        await Sql("UPDATE synthetic_evaluation_workflow.members SET current_json=@json WHERE member_id=@id", ("json", row), ("id", ids[0]));
        Check(before.AsEnumerable().SequenceEqual(await Counts()) && (await store.ReadAsync(Actor)).Issue is null, "no-readrepair+restoreownedtamper");
        await Sql("CREATE INDEX wf04_drift ON synthetic_evaluation_workflow.members(revision)");
        Check((await store.ReadAsync(Actor)).Issue == EvaluationWorkflowIssue.MigrationDrift, "schemadrift");
        await Sql("DROP INDEX synthetic_evaluation_workflow.wf04_drift");
        var blocked = false;
        try { await Sql("UPDATE synthetic_evaluation_workflow.versions SET accuracy_digest='tampered' WHERE version=0"); }
        catch (PostgresException) { blocked = true; }
        Check(blocked, "immutableSQLguard");
        var originalRegistry = await Scalar("SELECT canonical_json FROM synthetic_evaluation_workflow.registries WHERE revision=1");
        await Sql("ALTER TABLE synthetic_evaluation_workflow.registries DISABLE TRIGGER registries_immutable; UPDATE synthetic_evaluation_workflow.registries SET canonical_json='{}' WHERE revision=1; ALTER TABLE synthetic_evaluation_workflow.registries ENABLE TRIGGER registries_immutable");
        Check((await store.ReadAsync(Actor)).Issue == EvaluationWorkflowIssue.IntegrityMismatch, "invalidpersistedregistrytyped");
        await Sql("ALTER TABLE synthetic_evaluation_workflow.registries DISABLE TRIGGER registries_immutable; UPDATE synthetic_evaluation_workflow.registries SET canonical_json=@json WHERE revision=1; ALTER TABLE synthetic_evaluation_workflow.registries ENABLE TRIGGER registries_immutable", ("json", originalRegistry));
        Check((await store.ReadAsync(Actor)).Issue is null, "restoreownedregistrytamper");
        // Build a50+1page on one member without replacing its independent original classification.
        for (var i = 0; i < 51; i++)
        {
            workspace = await Read(store);
            var p = Command(workspace, ids[0]) with { Kind = EvaluationWorkflowCommandKind.PresentationCorrection, Outcome = null, Correction = dimensions, EvidenceReferenceIds = [], Reason = "Attributable fictional presentation revision " + i };
            Check((await store.ApplyAsync(p, Actor)).Issue is null, "appendboundedhistory");
        }
        var page = await store.ReadHistoryAsync(ids[0], 0, Actor);
        Check(page.History is { Events.Count: 50, NextAfterSequence: not null }, "historypage50");
        var nextPage = await store.ReadHistoryAsync(ids[0], page.History!.NextAfterSequence!.Value, Actor);
        Check(nextPage.History!.Events.All(e => e.Sequence > page.History.NextAfterSequence) &&
            !nextPage.History.Events.Select(e => e.EventId).Intersect(page.History.Events.Select(e => e.EventId)).Any(), "paginationnoduplication");
        registry = Next(registry, "synthetic-registry-v3", assignment: a => a with { Revision = a.Revision + 1, ScoredReviewGranted = false, PresentationCorrectionGranted = false });
        Check((await store.UpdateRegistryAsync("synthetic-registry-v2", registry)).Issue is null, "historyonlyregistry");
        Check((await store.ReadAsync(Actor)).Issue == EvaluationWorkflowIssue.Denied &&
            (await store.ReadMemberAsync(ids[0], Actor)).Issue == EvaluationWorkflowIssue.Denied, "historycannotpresentoriginal");
        Check((await store.ReadHistoryAsync(ids[0], 0, Actor)).History is not null &&
            (await store.ReadVersionAsync(0, Actor)).Version is not null, "separatehistoryderivedread");
        registry = Next(registry, "synthetic-registry-v4", assignment: a => a.Id == "synthetic-env-b-assignment" ? a with { Revision = a.Revision + 1, RelatedHistoryGranted = false } : a);
        Check((await store.UpdateRegistryAsync("synthetic-registry-v3", registry)).Issue is null, "narrowhistory");
        Check((await store.ReadVersionAsync(0, Actor)).Issue == EvaluationWorkflowIssue.Denied, "aggregatehistoryall100required");
        var envA = seed.Registry.Members.First(m => m.Scope.EnvironmentId == "synthetic-env-a").Id;
        var envB = seed.Registry.Members.First(m => m.Scope.EnvironmentId == "synthetic-env-b").Id;
        Check((await store.ReadHistoryAsync(envA, 0, Actor)).History is not null &&
            (await store.ReadHistoryAsync(envB, 0, Actor)).Issue == EvaluationWorkflowIssue.Denied, "historyexactenvironment");
        registry = Next(registry, "synthetic-registry-v5", revokeIdentity: true);
        Check((await store.UpdateRegistryAsync("synthetic-registry-v4", registry)).Issue is null, "durablerevoke");
        before = await Counts();
        Check((await store.ReadHistoryAsync(envA, 0, Actor)).Issue == EvaluationWorkflowIssue.Denied &&
            (await store.ApplyAsync(c1, Actor)).Issue == EvaluationWorkflowIssue.Denied, "revokedhistoryandreplaydenied");
        Check(before.AsEnumerable().SequenceEqual(await Counts()), "revokednowrites");
        Check((await new SyntheticEvaluationWorkflowStore(Connection).SeedAsync(seed)).AlreadySeeded, "restartafterrevoked");
        Check((await new SyntheticEvaluationWorkflowStore(Connection).ReadAsync(Actor)).Issue == EvaluationWorkflowIssue.Denied, "revocationnotreset");
    }
    private sealed class ThrowingObserver : ISyntheticEvaluationWorkflowCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid? eventId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic rollback injection.");
    }
}
