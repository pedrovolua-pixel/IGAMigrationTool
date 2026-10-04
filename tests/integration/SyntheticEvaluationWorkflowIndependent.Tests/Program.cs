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
    private static EvaluationReviewerRegistryInput? currentRegistry;
    private static readonly EvaluationWorkflowActor Actor = new("synthetic-reviewer");
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Independent check failed: " + name);
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static async Task<EvaluationWorkflowSnapshot> Read(SyntheticEvaluationWorkflowStore store)
    {
        var result = await store.ReadAsync(Actor);
        Check(result.Issue is null && result.Snapshot is not null, "current workspace admitted");
        return result.Snapshot!;
    }
    private static EvaluationWorkflowCommand Command(EvaluationWorkflowSnapshot s, int index,
        EvaluationReviewOutcome outcome = EvaluationReviewOutcome.Confirmed) =>
        new(Guid.NewGuid(), s.Members[index].Original.MemberId, EvaluationWorkflowCommandKind.Review,
            s.AggregateRevision, s.Members[index].Revision, s.RegistryVersionId, s.SourceDigest, s.SampleDigest,
            outcome, null, "Independent fictional rationale", s.Members[index].Original.EvidenceReferenceIds.Take(1).ToArray(), null);
    private static async Task<EvaluationWorkflowReceipt> Apply(SyntheticEvaluationWorkflowStore store, EvaluationWorkflowCommand command)
    {
        var result = await store.ApplyAsync(command, Actor);
        Check(result.Issue is null && result.Receipt is not null && !result.AlreadyApplied, "new command exactly one receipt");
        return result.Receipt!;
    }
    private static async Task<EvaluationWorkflowVersion> Version(SyntheticEvaluationWorkflowStore store, long n)
    {
        var r = await store.ReadVersionAsync(n, Actor);
        Check(r.Issue is null && r.Version is not null, "version admitted");
        var v = r.Version!;
        Check(Hash(v.VersionManifestJson) == v.VersionManifestDigest, "independent manifest SHA");
        Check(Hash(v.AccuracyCanonicalJson) == v.AccuracyDigest, "independent accuracy SHA");
        Check(Hash(v.WarningCanonicalJson) == v.WarningDigest, "independent warning SHA");
        Check(Hash(v.SnapshotCanonicalJson) == v.ContentDigest, "independent snapshot SHA");
        using var e = JsonDocument.Parse(v.SnapshotCanonicalJson);
        Check(e.RootElement.EnumerateObject().Select(p => p.Name).SequenceEqual(
            new[] { "schemaVersion", "versionManifestJson", "accuracyCanonicalJson", "warningCanonicalJson" }), "snapshot exact field order");
        Check(e.RootElement.GetProperty("schemaVersion").GetString() == "synthetic-evaluation-workflow-snapshot-v1", "snapshot schema literal");
        Check(e.RootElement.GetProperty("versionManifestJson").GetString() == v.VersionManifestJson &&
            e.RootElement.GetProperty("accuracyCanonicalJson").GetString() == v.AccuracyCanonicalJson &&
            e.RootElement.GetProperty("warningCanonicalJson").GetString() == v.WarningCanonicalJson, "raw nested linkage");
        using var m = JsonDocument.Parse(v.VersionManifestJson);
        Check(m.RootElement.EnumerateObject().Select(p => p.Name).SequenceEqual(new[] {
            "schemaVersion", "version", "sourceDigest", "populationDigest", "sampleDigest", "originalSampleVersionManifestDigest",
            "originalSampleCorrectionCutoffUtc", "registryVersionId", "registryDigest", "lastEventSequence", "eventsDigest",
            "correctionCutoffUtc", "predecessorDigest" }), "manifest exact field order");
        Check(m.RootElement.GetProperty("version").GetInt64() == n && v.Version == n, "version exact ordinal");
        Check(v.CorrectionCutoffUtc.Offset == TimeSpan.Zero, "cutoff UTC");
        return v;
    }
    private static void Counts(EvaluationWorkflowVersion v, params int[] expected)
    {
        using var j = JsonDocument.Parse(v.WarningCanonicalJson);
        var s = j.RootElement.GetProperty("summaries").GetProperty("generalAi");
        var fields = new[] { "confirmed", "rejected", "indeterminate", "unreviewed", "corrected", "denominator" };
        for (var i = 0; i < fields.Length; i++) Check(s.GetProperty(fields[i]).GetInt32() == expected[i], "literal " + fields[i]);
        Check(s.GetProperty("selected").GetInt32() == 100, "all frozen members counted");
        Check(s.GetProperty("lowSampleWarning").GetBoolean() == (expected[5] < 30), "warning exact independent threshold");
    }
    private static EvaluationReviewerRegistryInput Registry(EvaluationWorkflowSeed seed, string version,
        Func<EvaluationFixtureAssignment, EvaluationFixtureAssignment>? assignment = null,
        Func<EvaluationFixtureMember, EvaluationFixtureMember>? member = null,
        Func<EvaluationFixtureIdentity, EvaluationFixtureIdentity>? identity = null)
    {
        var previous = currentRegistry ?? seed.Registry;
        static bool Equal<T>(T a, T b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
        return seed.Registry with
        {
            VersionId = version,
            Assignments = seed.Registry.Assignments.Select(a =>
            {
                var p = previous.Assignments.Single(x => x.Id == a.Id);
                var desired = (assignment?.Invoke(a) ?? a) with { Revision = p.Revision };
                return Equal(desired, p) ? desired : desired with { Revision = p.Revision + 1 };
            }).ToArray(),
            Members = seed.Registry.Members.Select(m =>
            {
                var p = previous.Members.Single(x => x.Id == m.Id);
                var desired = (member?.Invoke(m) ?? m) with { Revision = p.Revision };
                return Equal(desired, p) ? desired : desired with { Revision = p.Revision + 1 };
            }).ToArray(),
            Identities = seed.Registry.Identities.Select(i =>
            {
                var p = previous.Identities.Single(x => x.Id == i.Id);
                var desired = (identity?.Invoke(i) ?? i) with { Revision = p.Revision };
                return Equal(desired, p) ? desired : desired with { Revision = p.Revision + 1 };
            }).ToArray()
        };
    }
    private static async Task Update(SyntheticEvaluationWorkflowStore store, string expected, EvaluationReviewerRegistryInput registry)
    {
        var r = await store.UpdateRegistryAsync(expected, registry);
        Check(r.Issue is null, "trusted registry version update");
        currentRegistry = registry;
    }
    private static async Task Denial(SyntheticEvaluationWorkflowStore store, EvaluationWorkflowCommand command, EvaluationWorkflowIssue issue)
    {
        var before = await Read(store);
        var r = await store.ApplyAsync(command, Actor);
        Check(r.Issue == issue && r.Receipt is null && !r.AlreadyApplied, "typed null-receipt denial " + issue);
        var after = await Read(store);
        Check(before.AggregateRevision == after.AggregateRevision && before.Versions.Count == after.Versions.Count, "denial no revision/version write");
    }
    public static async Task Main(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IGA_SYNTHETIC_EVALUATION_DATABASE")
            ?? "Host=127.0.0.1;Port=55433;Database=iga_synthetic_evaluation_wf04_verifier;Username=iga_synthetic";
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expected-v1.json")));
        var seed = FictionalEvaluationFixture.BuildSeed();
        currentRegistry = seed.Registry;
        var store = new SyntheticEvaluationWorkflowStore(connection);
        if (args.Contains("--migration-only") || args.Contains("--migration-recheck"))
        {
            await using var migrationConnection = new NpgsqlConnection(connection);
            await migrationConnection.OpenAsync();
            if (args.Contains("--migration-only"))
            {
                await using var prepare = new NpgsqlCommand("""
                CREATE SCHEMA synthetic_evaluation_workflow;
                CREATE TABLE synthetic_evaluation_workflow.schema_migrations (
                    migration_id text PRIMARY KEY, script_digest text NOT NULL,
                    schema_fingerprint text NOT NULL, applied_at timestamptz NOT NULL,
                    unexpected text DEFAULT 'independent-untracked-metadata');
                """, migrationConnection);
                await prepare.ExecuteNonQueryAsync();
            }
            var rejected = false;
            try { await store.InitializeAsync(); }
            catch (WorkflowInvalidException e) { rejected = e.Issue == EvaluationWorkflowIssue.MigrationDrift; }
            Check(rejected, "initialization refuses untracked metadata instead of establishing baseline");
            await using var inspect = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                        WHERE n.nspname='synthetic_evaluation_workflow' AND c.relkind='r'),
                       (SELECT count(*) FROM synthetic_evaluation_workflow.schema_migrations),
                       (SELECT count(*) FROM information_schema.columns
                        WHERE table_schema='synthetic_evaluation_workflow' AND table_name='schema_migrations'
                        AND column_name='unexpected' AND column_default IS NOT NULL)
                """, migrationConnection);
            await using var inspection = await inspect.ExecuteReaderAsync();
            await inspection.ReadAsync();
            Check(inspection.GetInt64(0) == 1, "denied initialization creates no workflow tables");
            Check(inspection.GetInt64(1) == 0, "denied initialization records no accepted migration baseline");
            Check(inspection.GetInt64(2) == 1, "denied initialization preserves original unexpected default");
            Console.WriteLine($"Independent migration checks passed {checks} assertions.");
            return;
        }
        await store.InitializeAsync();
        Check((await store.SeedAsync(seed)).Issue is null, "trusted initial seed");
        if (args.Contains("--tamper-only"))
        {
            await using var tamperConnection = new NpgsqlConnection(connection);
            await tamperConnection.OpenAsync();
            await using var tamper = new NpgsqlCommand("UPDATE synthetic_evaluation_workflow.members SET revision=1 WHERE member_id='synthetic-item-000000'", tamperConnection);
            await tamper.ExecuteNonQueryAsync();
            var corrupt = await store.ReadAsync(Actor);
            Check(corrupt.Issue == EvaluationWorkflowIssue.IntegrityMismatch && corrupt.Snapshot is null, "direct current projection tamper detected");
            Console.WriteLine($"Independent preserved tamper fixture passed {checks} assertions.");
            return;
        }
        var initial = await Read(store);
        Check(initial.AggregateRevision == 0 && initial.Members.Count == 100, "fresh dedicated DB");
        Check(initial.PopulationDigest == expected.RootElement.GetProperty("populationDigest").GetString() &&
            initial.SampleDigest == expected.RootElement.GetProperty("sampleDigest").GetString(), "pre-code literal cohort anchors");
        Check(initial.Members.Select(m => m.Original.MemberId).SequenceEqual(expected.RootElement.GetProperty("expectedSelectedIds")
            .EnumerateArray().Select(x => x.GetString()!)), "pre-code literal selected IDs");
        var originalTitles = initial.Members.Select(m => m.Original.Title).ToArray();
        var v0 = await Version(store, 0); Counts(v0, 0, 0, 0, 100, 0, 0);
        Check(v0.PredecessorDigest is null && v0.LastEventSequence == 0, "initial version has no predecessor/event");
        Check((await store.SeedAsync(seed)).AlreadySeeded, "identical seed no write");

        var first = Command(initial, 0); var receipt = await Apply(store, first);
        var v1 = await Version(store, 1); Counts(v1, 1, 0, 0, 99, 0, 1);
        Check(v1.PredecessorDigest == v0.ContentDigest, "v1 predecessor frozen v0");
        await Apply(store, Command(await Read(store), 1, EvaluationReviewOutcome.Rejected));
        Counts(await Version(store, 2), 1, 1, 0, 98, 0, 2);
        var corrected = Command(await Read(store), 2, EvaluationReviewOutcome.Corrected) with
        {
            OriginatingClassification = EvaluationOriginClassification.Rejected,
            Correction = new(null, null, "Fictional corrected root cause", "Fictional recommendation <script> text")
        };
        await Apply(store, corrected); Counts(await Version(store, 3), 1, 2, 0, 97, 1, 3);
        var presentation = Command(await Read(store), 0) with
        {
            Kind = EvaluationWorkflowCommandKind.PresentationCorrection,
            Outcome = null,
            Correction = new("Fictional presentation severity", null, null, null)
        };
        await Apply(store, presentation); Counts(await Version(store, 4), 1, 2, 0, 97, 2, 3);
        var state = await Read(store);
        Check(state.Members[0].Outcome == EvaluationReviewOutcome.Corrected &&
            state.Members[0].OriginatingClassification == EvaluationOriginClassification.Confirmed, "presentation preserves prior origin");
        Check(state.Members.Select(m => m.Original.Title).SequenceEqual(originalTitles), "original conclusions byte stable");
        Check((await Version(store, 0)).SnapshotCanonicalJson == v0.SnapshotCanonicalJson, "v0 raw bytes unchanged after corrections");
        var replay = await store.ApplyAsync(first, Actor);
        Check(replay.Issue is null && replay.AlreadyApplied && replay.Receipt == receipt, "exact replay original metadata receipt");
        Check((await Read(store)).AggregateRevision == 4, "exact replay zero writes");
        await Denial(store, first with { Reason = "changed semantic rationale" }, EvaluationWorkflowIssue.EventConflict);
        await Denial(store, Command(state, 4) with { ExpectedAggregateRevision = 0 }, EvaluationWorkflowIssue.RevisionConflict);
        await Denial(store, Command(state, 4) with { ExpectedSourceDigest = new string('a', 64) }, EvaluationWorkflowIssue.SourceConflict);
        await Denial(store, Command(state, 4) with { ExpectedRegistryVersionId = "synthetic-stale" }, EvaluationWorkflowIssue.RegistryConflict);
        foreach (var bad in new[] { Command(state, 4) with { EventId = Guid.Empty }, Command(state, 4) with { Reason = " " },
            Command(state, 4) with { Outcome = EvaluationReviewOutcome.Unreviewed },
            Command(state, 4) with { EvidenceReferenceIds = new[] { "synthetic-not-permitted" } } })
            await Denial(store, bad, EvaluationWorkflowIssue.InvalidInput);

        var concurrencyState = await Read(store);
        var concurrent = await Task.WhenAll(store.ApplyAsync(Command(concurrencyState, 4), Actor), store.ApplyAsync(Command(concurrencyState, 5), Actor));
        Check(concurrent.Count(r => r.Issue is null) == 1 && concurrent.Count(r => r.Issue == EvaluationWorkflowIssue.RevisionConflict) == 1,
            "two writers one append one revision conflict");
        var same = Command(await Read(store), 6);
        var exactConcurrent = await Task.WhenAll(store.ApplyAsync(same, Actor), store.ApplyAsync(same, Actor));
        Check(exactConcurrent.All(r => r.Issue is null) && exactConcurrent.Count(r => r.AlreadyApplied) == 1 &&
            exactConcurrent[0].Receipt == exactConcurrent[1].Receipt, "simultaneous exact replay one event/version");

        var beforeRollback = await Read(store);
        var throwing = new SyntheticEvaluationWorkflowStore(connection, new ThrowObserver());
        var didThrow = false;
        try { await throwing.ApplyAsync(Command(beforeRollback, 7), Actor); } catch (InjectedFailure) { didThrow = true; }
        Check(didThrow, "observer controlled failure surfaced");
        Check((await Read(store)).AggregateRevision == beforeRollback.AggregateRevision, "observer all pending writes rolled back");
        var contextTarget = beforeRollback.Members[8].Original.MemberId;
        var contextChanged = Registry(seed, "synthetic-final-context", member: m => m.Id == contextTarget ? m with
        {
            Revision = m.Revision + 1,
            AuthorizedContextSufficient = false
        } : m);
        var contextResult = await store.ApplyWithRegistryReplacementForTestAsync(Command(beforeRollback, 8), Actor, contextChanged, CancellationToken.None);
        Check(contextResult.Issue == EvaluationWorkflowIssue.Denied && contextResult.Receipt is null, "final context true to false cannot commit Confirmed");
        Check((await Read(store)).RegistryVersionId == beforeRollback.RegistryVersionId &&
            (await Read(store)).AggregateRevision == beforeRollback.AggregateRevision, "context simulation rolls back registry and review");

        var insufficient = Registry(seed, "synthetic-context-insufficient", member: m => m.Id == contextTarget ? m with { AuthorizedContextSufficient = false } : m);
        await Update(store, (await Read(store)).RegistryVersionId, insufficient);
        var insufficientState = await Read(store);
        Check(!insufficientState.Members[8].AuthorizedContextSufficient && insufficientState.Members[8].CanReview, "authorized insufficient context distinct from denial");
        var indeterminate = Command(insufficientState, 8, EvaluationReviewOutcome.Indeterminate) with { EvidenceReferenceIds = Array.Empty<string>() };
        var nowSufficient = Registry(seed, "synthetic-final-context-sufficient");
        var reverseContext = await store.ApplyWithRegistryReplacementForTestAsync(indeterminate, Actor, nowSufficient, CancellationToken.None);
        Check(reverseContext.Issue == EvaluationWorkflowIssue.Denied && reverseContext.Receipt is null, "final context false to true cannot commit Indeterminate");
        Check((await Read(store)).AggregateRevision == insufficientState.AggregateRevision &&
            (await Read(store)).RegistryVersionId == insufficient.VersionId, "reverse context simulation all writes rollback");
        await Apply(store, indeterminate);
        Check((await Read(store)).Members[8].Outcome == EvaluationReviewOutcome.Indeterminate, "authorized context limitation recorded excluded");

        var blocker = new BlockingObserver();
        var blockedStore = new SyntheticEvaluationWorkflowStore(connection, blocker);
        var beforeLinear = await Read(store);
        var admittedWrite = blockedStore.ApplyAsync(Command(beforeLinear, 9), Actor);
        await blocker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var concurrentRevoke = Registry(seed, "synthetic-concurrent-revoke", identity: i => i with { State = EvaluationFixtureState.Revoked });
        var revokeTask = store.UpdateRegistryAsync(beforeLinear.RegistryVersionId, concurrentRevoke);
        await Task.Delay(100);
        Check(!revokeTask.IsCompleted, "registry revocation waits for review transaction lock");
        blocker.Release.TrySetResult();
        Check((await admittedWrite).Issue is null, "held authorized review commits before waiting revocation");
        Check((await revokeTask).Issue is null && (await store.ReadAsync(Actor)).Snapshot is null, "durable subsequent revoke denies current read");
        currentRegistry = concurrentRevoke;
        var afterConcurrentRestore = Registry(seed, "synthetic-after-concurrent-restore");
        await Update(store, concurrentRevoke.VersionId, afterConcurrentRestore);
        Check((await Version(store, beforeLinear.AggregateRevision + 1)).LastEventSequence ==
            beforeLinear.Versions[^1].LastEventSequence + 1, "committed pre-revoke review retained in immutable history");

        var noSource = Registry(seed, "synthetic-history-only", assignment: a => a with
        {
            Revision = a.Revision + 1,
            ScoredReviewGranted = false,
            PresentationCorrectionGranted = false
        });
        await Update(store, (await Read(store)).RegistryVersionId, noSource);
        Check((await store.ReadAsync(Actor)).Issue == EvaluationWorkflowIssue.Denied &&
            (await store.ReadMemberAsync(first.MemberId, Actor)).Member is null, "history grant alone no current source");
        var history = await store.ReadHistoryAsync(first.MemberId, 0, Actor);
        Check(history.Issue is null && history.History is not null && history.History.Events.Count == 2, "scoped history-only retains attributed events");
        Check((await store.ReadVersionAsync(0, Actor)).Version!.SnapshotCanonicalJson == v0.SnapshotCanonicalJson, "history-only immutable version read");
        var oneHistory = noSource with
        {
            VersionId = "synthetic-one-environment-history",
            Assignments = noSource.Assignments.Select(a =>
            a.Scope.EnvironmentId == "synthetic-env-b" ? a with { Revision = a.Revision + 1, RelatedHistoryGranted = false } : a).ToArray()
        };
        await Update(store, noSource.VersionId, oneHistory);
        Check((await store.ReadVersionAsync(0, Actor)).Issue == EvaluationWorkflowIssue.Denied, "no whole aggregate with denied environment");
        Check((await store.ReadHistoryAsync(first.MemberId, 0, Actor)).Issue is null, "independent permitted environment narrow history");
        var restored = Registry(seed, "synthetic-restored"); await Update(store, oneHistory.VersionId, restored);
        var restoredState = await Read(store);
        Check(restoredState.PopulationDigest == initial.PopulationDigest && restoredState.SampleDigest == initial.SampleDigest,
            "registry changes never resample");
        var revoked = Registry(seed, "synthetic-revoked", identity: i => i with { Revision = i.Revision + 1, State = EvaluationFixtureState.Revoked });
        await Update(store, restored.VersionId, revoked);
        var revokedRead = await store.ReadAsync(Actor);
        Check(revokedRead.Issue == EvaluationWorkflowIssue.Denied && revokedRead.Snapshot is null, "current revoke denies all current content");
        Check((await store.ApplyAsync(first, Actor)).Issue == EvaluationWorkflowIssue.Denied, "revoke denies old accepted retry receipt");
        var restarted = new SyntheticEvaluationWorkflowStore(connection); await restarted.InitializeAsync();
        Check((await restarted.SeedAsync(seed)).AlreadySeeded && (await restarted.ReadAsync(Actor)).Snapshot is null,
            "restart initial seed cannot resurrect registry authority");
        var expanded = Registry(seed, "synthetic-forbidden-expansion", assignment: a => a with { ReviewCategoryIds = new[] { "synthetic-category", "synthetic-new-category" } });
        var expansion = await store.UpdateRegistryAsync(revoked.VersionId, expanded);
        Check(expansion.Issue == EvaluationWorkflowIssue.InvalidInput && expansion.RegistryVersionId is null, "seeded grant ceiling enforced");

        await using var c = new NpgsqlConnection(connection); await c.OpenAsync();
        var guardRejected = false;
        try { await using var q = new NpgsqlCommand("UPDATE synthetic_evaluation_workflow.versions SET manifest_json=manifest_json WHERE version=0", c); await q.ExecuteNonQueryAsync(); }
        catch (PostgresException) { guardRejected = true; }
        Check(guardRejected, "SQL immutable version UPDATE refused");
        await using (var q = new NpgsqlCommand("CREATE TABLE synthetic_evaluation_workflow.independent_drift_marker(value integer)", c)) await q.ExecuteNonQueryAsync();
        var drift = await store.ReadAsync(Actor);
        Check(drift.Issue == EvaluationWorkflowIssue.MigrationDrift && drift.Snapshot is null, "own schema drift rejected before content");
        Console.WriteLine($"Independent workflow verifier passed {checks} assertions. Dedicated fictional database preserved; live/queue/retention/manual gates unverified.");
    }
    private sealed class InjectedFailure : Exception { }
    private sealed class ThrowObserver : ISyntheticEvaluationWorkflowCommitObserver
    {
        public Task BeforeCommitAsync(string operation, Guid? eventId, CancellationToken cancellationToken) =>
            Task.FromException(new InjectedFailure());
    }
    private sealed class BlockingObserver : ISyntheticEvaluationWorkflowCommitObserver
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task BeforeCommitAsync(string operation, Guid? eventId, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
