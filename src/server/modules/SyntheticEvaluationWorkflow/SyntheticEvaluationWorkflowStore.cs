using System.Data;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using SyntheticEvaluation;

namespace SyntheticEvaluationWorkflow;

/// <summary>Guarded fictional fixture persistence. Not production identity or evidence authority.</summary>
public sealed class SyntheticEvaluationWorkflowStore
{
    private readonly string connectionString;
    private readonly ISyntheticEvaluationWorkflowCommitObserver? observer;
    public SyntheticEvaluationWorkflowStore(string connectionString, ISyntheticEvaluationWorkflowCommitObserver? observer = null)
    {
        var c = new NpgsqlConnectionStringBuilder(connectionString);
        if (c.Host is not ("localhost" or "127.0.0.1" or "::1") || c.Database is null ||
            !c.Database.StartsWith("iga_synthetic_evaluation_", StringComparison.Ordinal))
            throw new ArgumentException("Only a dedicated loopback fictional evaluation database is supported.", nameof(connectionString));
        c.CommandTimeout = 15;
        this.connectionString = c.ConnectionString;
        this.observer = observer;
    }
    private static string J<T>(T value) => EvaluationWorkflowCanonical.Json(value);
    private static string H(string value) => EvaluationWorkflowCanonical.Hash(value);
    private static IReadOnlyList<T> L<T>(IEnumerable<T> values) => EvaluationWorkflowCanonical.List(values);
    private static void Require(bool condition, EvaluationWorkflowIssue issue = EvaluationWorkflowIssue.IntegrityMismatch)
    {
        if (!condition) throw new WorkflowInvalidException(issue);
    }
    private async Task<NpgsqlConnection> Open(CancellationToken ct)
    {
        var c = new NpgsqlConnection(connectionString);
        try
        {
            await c.OpenAsync(ct);
            if (c.PostgreSqlVersion.Major != 18) throw new ArgumentException("PostgreSQL18 is required.");
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }
    private static async Task Start(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await SyntheticEvaluationWorkflowMigration.LockAsync(c, t, ct);
        await SyntheticEvaluationWorkflowMigration.VerifyAsync(c, t, ct);
    }
    private static Task Execute(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct,
        params (string Name, object Value)[] values) => SyntheticEvaluationWorkflowMigration.Execute(c, t, sql, ct, values);
    private static async Task<DateTimeOffset> Now(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT clock_timestamp()", c, t);
        return new DateTimeOffset((DateTime)(await cmd.ExecuteScalarAsync(ct))!);
    }
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await Open(cancellationToken);
        await SyntheticEvaluationWorkflowMigration.InitializeAsync(c, cancellationToken);
    }
    public async Task<EvaluationWorkflowSeedResult> SeedAsync(EvaluationWorkflowSeed? input, CancellationToken cancellationToken = default)
    {
        try
        {
            var (source, sample, registryInput) = EvaluationWorkflowPolicy.Seed(input);
            var json = J(source); var digest = H(json); var registryJson = J(registryInput); var registryDigest = H(registryJson);
            await using var c = await Open(cancellationToken);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await Start(c, t, cancellationToken);
            await using (var exists = new NpgsqlCommand("SELECT count(*) FROM synthetic_evaluation_workflow.workspace", c, t))
            {
                if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
                {
                    var prior = await Load(c, t, cancellationToken);
                    Require(prior.SourceDigest == digest && prior.Registries[0].Digest == registryDigest, EvaluationWorkflowIssue.SeedConflict);
                    return new(null, true);
                }
            }
            await using (var residue = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM synthetic_evaluation_workflow.registries)
                +(SELECT count(*) FROM synthetic_evaluation_workflow.members)
                +(SELECT count(*) FROM synthetic_evaluation_workflow.events)
                +(SELECT count(*) FROM synthetic_evaluation_workflow.versions)
                """, c, t))
                Require(Convert.ToInt64(await residue.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 0);
            var at = await Now(c, t, cancellationToken);
            var registry = new WorkflowRegistry(1, registryInput, registryJson, registryDigest, at);
            await InsertRegistry(c, t, registry, cancellationToken);
            await Execute(c, t, "INSERT INTO synthetic_evaluation_workflow.workspace VALUES(true,@json,@digest,@registry,0,0,@version)",
                cancellationToken, ("json", json), ("digest", digest), ("registry", registryDigest), ("version", registryInput.VersionId));
            var members = source.Originals.ToDictionary(o => o.MemberId,
                o => new WorkflowCurrent(o.MemberId, 0, EvaluationReviewOutcome.Unreviewed, null, null), StringComparer.Ordinal);
            foreach (var m in members.Values)
                await Execute(c, t, "INSERT INTO synthetic_evaluation_workflow.members VALUES(@member,0,@json)", cancellationToken,
                    ("member", m.MemberId), ("json", J(m)));
            var version = EvaluationWorkflowCanonical.Version(source, sample, digest, 0, registry, [], members, at, null);
            await InsertVersion(c, t, version, cancellationToken);
            await Observe("seed", null, cancellationToken);
            await Load(c, t, cancellationToken);
            await t.CommitAsync(cancellationToken);
            return new(null);
        }
        catch (WorkflowInvalidException e) { return new(e.Issue); }
        catch (JsonException) { return new(EvaluationWorkflowIssue.IntegrityMismatch); }
    }
    private sealed class State
    {
        internal required WorkflowSource Source;
        internal required SamplingProjection Sample;
        internal required string SourceDigest;
        internal required List<WorkflowRegistry> Registries;
        internal required List<WorkflowStoredEvent> Events;
        internal required List<EvaluationWorkflowVersion> Versions;
        internal required Dictionary<string, WorkflowCurrent> Members;
        internal WorkflowRegistry CurrentRegistry => Registries[^1];
        internal long Revision => Versions[^1].Version;
        internal EvaluationReviewerRegistry Captured => EvaluationReviewerPolicy.Capture(CurrentRegistry.Input).Registry
            ?? throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch);
    }
    private static async Task<State> Load(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        try { return await Reconstruct(c, t, ct); }
        catch (WorkflowInvalidException e) when(e.Issue==EvaluationWorkflowIssue.InvalidInput) { throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch); }
        catch (JsonException) { throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch); }
        catch (FormatException) { throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch); }
        catch (ArgumentException) { throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch); }
        catch (InvalidOperationException) { throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch); }
    }
    private static async Task<State> Reconstruct(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        string sourceJson, sourceDigest, initialRegistry, currentRegistry;
        long aggregate, sequence;
        await using (var cmd = new NpgsqlCommand("SELECT source_json,source_digest,initial_registry_digest,aggregate_revision,last_event_sequence,current_registry_version_id FROM synthetic_evaluation_workflow.workspace WHERE singleton=true", c, t))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) throw new WorkflowInvalidException(EvaluationWorkflowIssue.NotInitialized);
            sourceJson = r.GetString(0); sourceDigest = r.GetString(1); initialRegistry = r.GetString(2);
            aggregate = r.GetInt64(3); sequence = r.GetInt64(4); currentRegistry = r.GetString(5);
            Require(!await r.ReadAsync(ct));
        }
        Require(aggregate is >= 0 and <= 1000 && sequence is >= 0 and <= 1000 && H(sourceJson) == sourceDigest);
        var source = EvaluationWorkflowCanonical.Parse<WorkflowSource>(sourceJson);
        Require(source.SchemaVersion == "synthetic-evaluation-workflow-source-v1" && source.ScopeId == "synthetic-scope");
        var registries = new List<WorkflowRegistry>();
        await using (var cmd = new NpgsqlCommand("SELECT version_id,revision,canonical_json,digest,recorded_at FROM synthetic_evaluation_workflow.registries ORDER BY revision", c, t))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                Require(registries.Count < 1001);
                var json = r.GetString(2); var input = EvaluationWorkflowCanonical.Registry(EvaluationWorkflowCanonical.Parse<EvaluationReviewerRegistryInput>(json));
                Require(input.VersionId == r.GetString(0) && r.GetInt64(1) == registries.Count + 1 && J(input) == json && H(json) == r.GetString(3));
                if (registries.Count > 0) Require(EvaluationWorkflowPolicy.RegistryReplacement(registries[0].Input, registries[^1].Input, input));
                registries.Add(new(r.GetInt64(1), input, json, r.GetString(3), r.GetFieldValue<DateTimeOffset>(4)));
            }
        Require(registries.Count > 0 && registries[0].Digest == initialRegistry);
        var admitted = EvaluationWorkflowPolicy.Seed(new(source.Sampling, registries[0].Input, source.Originals));
        Require(J(admitted.Source) == sourceJson);
        source = admitted.Source; var sample = admitted.Sample;
        var members = source.Originals.ToDictionary(o => o.MemberId,
            o => new WorkflowCurrent(o.MemberId, 0, EvaluationReviewOutcome.Unreviewed, null, null), StringComparer.Ordinal);
        var stored = new List<WorkflowStoredEvent>();
        await using (var cmd = new NpgsqlCommand("SELECT event_id,sequence,aggregate_revision,member_id,member_revision,actor_id,command_json,command_digest,event_json,event_digest,receipt_json FROM synthetic_evaluation_workflow.events ORDER BY sequence", c, t))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                Require(stored.Count < 1000);
                var command = EvaluationWorkflowCanonical.Parse<EvaluationWorkflowCommand>(r.GetString(6));
                var ev = EvaluationWorkflowCanonical.Parse<EvaluationWorkflowEvent>(r.GetString(8)) with { ContentDigest = r.GetString(9) };
                ev = ev with { EvidenceReferenceIds = L(ev.EvidenceReferenceIds) };
                var receipt = EvaluationWorkflowCanonical.Parse<EvaluationWorkflowReceipt>(r.GetString(10));
                Require(EvaluationWorkflowPolicy.Command(command) && command.EventId == r.GetGuid(0) &&
                    ev.EventId == command.EventId && ev.Sequence == r.GetInt64(1) && ev.AggregateRevision == r.GetInt64(2) &&
                    ev.MemberId == r.GetString(3) && ev.MemberRevision == r.GetInt64(4) && ev.ActorId == r.GetString(5) &&
                    J(command) == r.GetString(6) && H(J(command)) == r.GetString(7) && ev.CommandDigest == r.GetString(7) &&
                    EvaluationWorkflowCanonical.EventJson(ev) == r.GetString(8) && H(r.GetString(8)) == ev.ContentDigest &&
                    J(receipt) == r.GetString(10));
                stored.Add(new(EvaluationWorkflowPolicy.Detach(command), ev, receipt));
            }
        Require(stored.Count == sequence && stored.Select(e => e.Event.Sequence).SequenceEqual(Enumerable.Range(1, stored.Count).Select(i => (long)i)));
        var versions = new List<EvaluationWorkflowVersion>();
        var accepted = new List<WorkflowStoredEvent>();
        var registryIndex = 0;
        await using (var cmd = new NpgsqlCommand("SELECT version,registry_version_id,last_event_sequence,manifest_json,manifest_digest,accuracy_json,accuracy_digest,warning_json,warning_digest,snapshot_digest,recorded_at FROM synthetic_evaluation_workflow.versions ORDER BY version", c, t))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                Require(versions.Count <= 1000 && r.GetInt64(0) == versions.Count);
                var versionNumber = r.GetInt64(0); var registryId = r.GetString(1); var at = r.GetFieldValue<DateTimeOffset>(10);
                Require(versions.Count == 0 || at >= versions[^1].CorrectionCutoffUtc);
                WorkflowStoredEvent? added = null;
                if (versionNumber == 0) Require(registryId == registries[0].Input.VersionId && at == registries[0].AtUtc);
                else if (registryId != registries[registryIndex].Input.VersionId)
                {
                    Require(registryIndex + 1 < registries.Count && registryId == registries[registryIndex + 1].Input.VersionId);
                    registryIndex++;
                    Require(at == registries[registryIndex].AtUtc && !stored.Any(e => e.Event.AggregateRevision == versionNumber));
                }
                else
                {
                    added = stored.SingleOrDefault(e => e.Event.AggregateRevision == versionNumber);
                    Require(added is not null);
                    var command = added!.Command; var ev = added.Event;
                    Require(members.ContainsKey(command.MemberId) && command.ExpectedAggregateRevision == versionNumber - 1 &&
                        command.ExpectedMemberRevision == members[command.MemberId].Revision &&
                        command.ExpectedRegistryVersionId == registryId && command.ExpectedSourceDigest == sourceDigest &&
                        command.ExpectedSampleDigest == sample.ContentDigest && ev.RecordedAtUtc == at &&
                        command.EvidenceReferenceIds.All(id => source.Originals.Single(o => o.MemberId == command.MemberId).EvidenceReferenceIds.Contains(id)));
                    var currentCaptured = EvaluationReviewerPolicy.Capture(registries[registryIndex].Input).Registry!;
                    var selected = sample.Selected.Single(s => s.Member.Id == command.MemberId).Member;
                    var action = command.Kind == EvaluationWorkflowCommandKind.Review ? EvaluationReviewerAction.ScoredReview : EvaluationReviewerAction.PresentationCorrection;
                    var decision = EvaluationWorkflowPolicy.Decide(currentCaptured, new(ev.ActorId), selected, action, at);
                    Require(EvaluationWorkflowPolicy.Context(command, decision));
                    var updated = EvaluationWorkflowPolicy.Apply(members[command.MemberId], command);
                    var expectedEvent = MakeEvent(command, ev.ActorId, registries[registryIndex], updated, versionNumber, accepted, at);
                    Require(J(expectedEvent) == J(ev));
                    members[command.MemberId] = updated;
                    accepted.Add(added);
                }
                Require(r.GetInt64(2) == accepted.Count);
                var expected = EvaluationWorkflowCanonical.Version(source, sample, sourceDigest, versionNumber,
                    registries[registryIndex], accepted, members, at, versions.LastOrDefault()?.ContentDigest);
                Require(expected.VersionManifestJson == r.GetString(3) && expected.VersionManifestDigest == r.GetString(4) &&
                    expected.AccuracyCanonicalJson == r.GetString(5) && expected.AccuracyDigest == r.GetString(6) &&
                    expected.WarningCanonicalJson == r.GetString(7) && expected.WarningDigest == r.GetString(8) &&
                    expected.ContentDigest == r.GetString(9));
                if (added is not null) Require(J(added.Receipt) == J(MakeReceipt(added.Event, expected)));
                versions.Add(expected);
            }
        Require(versions.Count == aggregate + 1 && accepted.Count == stored.Count && registryIndex == registries.Count - 1 &&
            currentRegistry == registries[^1].Input.VersionId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand("SELECT member_id,revision,current_json FROM synthetic_evaluation_workflow.members ORDER BY member_id", c, t))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var id = r.GetString(0);
                Require(seen.Add(id) && members.TryGetValue(id, out var current) &&
                    current.Revision == r.GetInt64(1) && J(current) == r.GetString(2));
            }
        Require(seen.Count == 100);
        return new() { Source = source, Sample = sample, SourceDigest = sourceDigest, Registries = registries,
            Events = stored, Versions = versions, Members = members };
    }
    private static EvaluationWorkflowEvent MakeEvent(EvaluationWorkflowCommand command, string actor, WorkflowRegistry registry,
        WorkflowCurrent updated, long aggregate, IReadOnlyList<WorkflowStoredEvent> events, DateTimeOffset at)
    {
        var env = registry.Input.Members.Single(m => m.Id == command.MemberId).Scope.EnvironmentId;
        var ev = new EvaluationWorkflowEvent(command.EventId, events.Count + 1, aggregate, updated.Revision, command.MemberId,
            command.Kind, actor, EvaluationWorkflowPolicy.Assignment(env), registry.Input.VersionId, registry.Digest, at,
            updated.Outcome, updated.OriginatingClassification, command.Reason, L(command.EvidenceReferenceIds),
            command.Correction, H(J(command)), events.LastOrDefault()?.Event.ContentDigest ?? new string('0', 64), "");
        return ev with { ContentDigest = H(EvaluationWorkflowCanonical.EventJson(ev)) };
    }
    private static EvaluationWorkflowReceipt MakeReceipt(EvaluationWorkflowEvent ev, EvaluationWorkflowVersion version) =>
        new(ev.EventId, ev.MemberId, ev.ActorId, ev.AggregateRevision, ev.MemberRevision, version.Version, version.ContentDigest, ev.RecordedAtUtc);
    private static async Task InsertRegistry(NpgsqlConnection c, NpgsqlTransaction t, WorkflowRegistry registry, CancellationToken ct) =>
        await Execute(c, t, "INSERT INTO synthetic_evaluation_workflow.registries VALUES(@id,@rev,@json,@digest,@at)", ct,
            ("id", registry.Input.VersionId), ("rev", registry.Revision), ("json", registry.Json), ("digest", registry.Digest), ("at", registry.AtUtc));
    private static async Task InsertVersion(NpgsqlConnection c, NpgsqlTransaction t, EvaluationWorkflowVersion v, CancellationToken ct) =>
        await Execute(c, t, "INSERT INTO synthetic_evaluation_workflow.versions VALUES(@version,@registry,@sequence,@manifest,@manifestdigest,@accuracy,@accuracydigest,@warning,@warningdigest,@digest,@at)", ct,
            ("version", v.Version), ("registry", v.RegistryVersionId), ("sequence", v.LastEventSequence),
            ("manifest", v.VersionManifestJson), ("manifestdigest", v.VersionManifestDigest), ("accuracy", v.AccuracyCanonicalJson),
            ("accuracydigest", v.AccuracyDigest), ("warning", v.WarningCanonicalJson), ("warningdigest", v.WarningDigest),
            ("digest", v.ContentDigest), ("at", v.CorrectionCutoffUtc));
    private Task Observe(string operation, Guid? eventId, CancellationToken ct) =>
        observer?.BeforeCommitAsync(operation, eventId, ct) ?? Task.CompletedTask;
    private static void Actor(EvaluationWorkflowActor? actor, State state)
    {
        if (actor is null || !EvaluationWorkflowPolicy.Reference(actor.IdentityId)) throw new WorkflowInvalidException(EvaluationWorkflowIssue.Denied);
        var i = state.CurrentRegistry.Input.Identities.SingleOrDefault(i => i.Id == actor.IdentityId);
        Require(i is { Authenticated: true, State: EvaluationFixtureState.Active }, EvaluationWorkflowIssue.Denied);
    }
    private static SamplingMember Selected(State state, string memberId) =>
        state.Sample.Selected.SingleOrDefault(s => s.Member.Id == memberId)?.Member ??
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.NotFound);
    private static void History(State state, EvaluationWorkflowActor actor, SamplingMember member, DateTimeOffset at) =>
        Require(EvaluationWorkflowPolicy.Decide(state.Captured, actor, member, EvaluationReviewerAction.RelatedHistory, at).IsAuthorized, EvaluationWorkflowIssue.Denied);
    private static EvaluationWorkflowMember Member(State state, EvaluationWorkflowActor actor, SamplingMember m, DateTimeOffset at)
    {
        History(state, actor, m, at);
        var score = EvaluationWorkflowPolicy.Decide(state.Captured, actor, m, EvaluationReviewerAction.ScoredReview, at);
        var presentation = EvaluationWorkflowPolicy.Decide(state.Captured, actor, m, EvaluationReviewerAction.PresentationCorrection, at);
        Require(score.IsAuthorized || presentation.IsAuthorized, EvaluationWorkflowIssue.Denied);
        var current = state.Members[m.Id];
        return new(state.Source.Originals.Single(o => o.MemberId == m.Id),
            EvaluationWorkflowCanonical.Metadata(state.Sample).Single(md => md.MemberId == m.Id),
            EvaluationWorkflowPolicy.Assignment(m.EnvironmentId), current.Revision, current.Outcome,
            current.OriginatingClassification, score.IsAuthorized, presentation.IsAuthorized,
            score.IsAuthorized && score.AuthorizedContextSufficient == true, current.Correction);
    }
    public async Task<EvaluationWorkflowReadResult> ReadAsync(EvaluationWorkflowActor actor, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var c = await Open(cancellationToken);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await Start(c, t, cancellationToken);
            var state = await Load(c, t, cancellationToken); Actor(actor, state);
            var at = await Now(c, t, cancellationToken);
            var members = L(state.Sample.Selected.OrderBy(s => s.Member.Id, StringComparer.Ordinal).Select(s => Member(state, actor, s.Member, at)));
            var versions = L(state.Versions.Select(v => new EvaluationWorkflowVersionSummary(v.Version, v.CorrectionCutoffUtc,
                v.LastEventSequence, v.RegistryVersionId, v.ContentDigest, v.PredecessorDigest)));
            return new(null, new("synthetic-evaluation-workflow-v1", actor.IdentityId, state.Revision, state.CurrentRegistry.Input.VersionId,
                state.SourceDigest, state.Sample.PopulationDigest, state.Sample.ContentDigest,
                state.Sample.Versions.CorrectionCutoffUtc, members, versions));
        }
        catch (WorkflowInvalidException e) { return new(e.Issue, null); }
    }
    public async Task<EvaluationWorkflowMemberReadResult> ReadMemberAsync(string memberId, EvaluationWorkflowActor actor, CancellationToken cancellationToken = default)
    {
        if (!EvaluationWorkflowPolicy.Reference(memberId)) return new(EvaluationWorkflowIssue.InvalidInput, null, null, null, null, null);
        try
        {
            await using var c = await Open(cancellationToken);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await Start(c, t, cancellationToken);
            var state = await Load(c, t, cancellationToken); Actor(actor, state);
            var member = Member(state, actor, Selected(state, memberId), await Now(c, t, cancellationToken));
            return new(null, member, state.Revision, state.CurrentRegistry.Input.VersionId, state.SourceDigest, state.Sample.ContentDigest);
        }
        catch (WorkflowInvalidException e) { return new(e.Issue, null, null, null, null, null); }
    }
    public async Task<EvaluationWorkflowHistoryReadResult> ReadHistoryAsync(string memberId, long afterSequence,
        EvaluationWorkflowActor actor, CancellationToken cancellationToken = default)
    {
        if (!EvaluationWorkflowPolicy.Reference(memberId) || !EvaluationWorkflowPolicy.Revision(afterSequence)) return new(EvaluationWorkflowIssue.InvalidInput, null);
        try
        {
            await using var c = await Open(cancellationToken);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await Start(c, t, cancellationToken);
            var state = await Load(c, t, cancellationToken); Actor(actor, state);
            History(state, actor, Selected(state, memberId), await Now(c, t, cancellationToken));
            var remaining = state.Events.Where(e => e.Event.MemberId == memberId && e.Event.Sequence > afterSequence).Select(e => e.Event).ToArray();
            var page = L(remaining.Take(50));
            return new(null, new(memberId, state.Revision, state.Members[memberId].Revision, state.CurrentRegistry.Input.VersionId,
                page, remaining.Length > 50 ? page[^1].Sequence : null));
        }
        catch (WorkflowInvalidException e) { return new(e.Issue, null); }
    }
    public async Task<EvaluationWorkflowVersionReadResult> ReadVersionAsync(long version, EvaluationWorkflowActor actor, CancellationToken cancellationToken = default)
    {
        if (!EvaluationWorkflowPolicy.Revision(version)) return new(EvaluationWorkflowIssue.InvalidInput, null);
        try
        {
            await using var c = await Open(cancellationToken);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await Start(c, t, cancellationToken);
            var state = await Load(c, t, cancellationToken); Actor(actor, state);
            var at = await Now(c, t, cancellationToken);
            foreach (var s in state.Sample.Selected) History(state, actor, s.Member, at);
            var result = state.Versions.SingleOrDefault(v => v.Version == version);
            return result is null ? new(EvaluationWorkflowIssue.NotFound, null) : new(null, result);
        }
        catch (WorkflowInvalidException e) { return new(e.Issue, null); }
    }
    public Task<EvaluationWorkflowApplyResult> ApplyAsync(EvaluationWorkflowCommand? command, EvaluationWorkflowActor actor,
        CancellationToken cancellationToken = default) => Apply(command, actor, null, cancellationToken);
    internal Task<EvaluationWorkflowApplyResult> ApplyWithRegistryReplacementForTestAsync(EvaluationWorkflowCommand command,
        EvaluationWorkflowActor actor, EvaluationReviewerRegistryInput replacement, CancellationToken cancellationToken = default) =>
        Apply(command, actor, replacement, cancellationToken);
    private async Task<EvaluationWorkflowApplyResult> Apply(EvaluationWorkflowCommand? input, EvaluationWorkflowActor actor,
        EvaluationReviewerRegistryInput? replacement, CancellationToken ct)
    {
        if (!EvaluationWorkflowPolicy.Command(input)) return new(EvaluationWorkflowIssue.InvalidInput, null);
        var command = EvaluationWorkflowPolicy.Detach(input!);
        try
        {
            await using var c = await Open(ct);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await Start(c, t, ct);
            var state = await Load(c, t, ct); Actor(actor, state);
            var member = Selected(state, command.MemberId);
            Require(command.EvidenceReferenceIds.All(id => state.Source.Originals.Single(o => o.MemberId == member.Id).EvidenceReferenceIds.Contains(id)), EvaluationWorkflowIssue.InvalidInput);
            var at = await Now(c, t, ct);
            var action = command.Kind == EvaluationWorkflowCommandKind.Review ? EvaluationReviewerAction.ScoredReview : EvaluationReviewerAction.PresentationCorrection;
            var decision = EvaluationWorkflowPolicy.Decide(state.Captured, actor, member, action, at);
            Require(decision.IsAuthorized, EvaluationWorkflowIssue.Denied);
            var prior = state.Events.SingleOrDefault(e => e.Event.EventId == command.EventId);
            if (prior is not null)
            {
                Require(prior.Event.ActorId == actor.IdentityId && J(prior.Command) == J(command), EvaluationWorkflowIssue.EventConflict);
                return new(null, prior.Receipt, true);
            }
            Require(command.ExpectedSourceDigest == state.SourceDigest && command.ExpectedSampleDigest == state.Sample.ContentDigest, EvaluationWorkflowIssue.SourceConflict);
            Require(command.ExpectedRegistryVersionId == state.CurrentRegistry.Input.VersionId, EvaluationWorkflowIssue.RegistryConflict);
            Require(command.ExpectedAggregateRevision == state.Revision && command.ExpectedMemberRevision == state.Members[member.Id].Revision, EvaluationWorkflowIssue.RevisionConflict);
            Require(EvaluationWorkflowPolicy.Context(command, decision), EvaluationWorkflowIssue.Denied);
            Require(state.Revision < EvaluationWorkflowPolicy.MaxRevision && state.Members[member.Id].Revision < EvaluationWorkflowPolicy.MaxRevision, EvaluationWorkflowIssue.RevisionOverflow);
            Require(state.Revision < 1000, EvaluationWorkflowIssue.WorkflowLimit);
            Require(at >= state.Versions[^1].CorrectionCutoffUtc, EvaluationWorkflowIssue.ClockConflict);
            var updated = EvaluationWorkflowPolicy.Apply(state.Members[member.Id], command);
            var ev = MakeEvent(command, actor.IdentityId, state.CurrentRegistry, updated, state.Revision + 1, state.Events, at);
            state.Members[member.Id] = updated;
            var events = state.Events.Append(new WorkflowStoredEvent(command, ev, null!)).ToArray();
            var version = EvaluationWorkflowCanonical.Version(state.Source, state.Sample, state.SourceDigest, state.Revision + 1,
                state.CurrentRegistry, events, state.Members, at, state.Versions[^1].ContentDigest);
            var receipt = MakeReceipt(ev, version);
            await Execute(c, t, "INSERT INTO synthetic_evaluation_workflow.events VALUES(@id,@seq,@aggregate,@member,@rev,@actor,@command,@commanddigest,@event,@eventdigest,@receipt)", ct,
                ("id", ev.EventId), ("seq", ev.Sequence), ("aggregate", ev.AggregateRevision), ("member", ev.MemberId),
                ("rev", ev.MemberRevision), ("actor", ev.ActorId), ("command", J(command)), ("commanddigest", ev.CommandDigest),
                ("event", EvaluationWorkflowCanonical.EventJson(ev)), ("eventdigest", ev.ContentDigest), ("receipt", J(receipt)));
            await Execute(c, t, "UPDATE synthetic_evaluation_workflow.members SET revision=@rev,current_json=@json WHERE member_id=@member", ct,
                ("rev", updated.Revision), ("json", J(updated)), ("member", updated.MemberId));
            await Execute(c, t, "UPDATE synthetic_evaluation_workflow.workspace SET aggregate_revision=@aggregate,last_event_sequence=@seq", ct,
                ("aggregate", ev.AggregateRevision), ("seq", ev.Sequence));
            await InsertVersion(c, t, version, ct);
            await Observe("apply", command.EventId, ct);
            if (replacement is not null)
            {
                var normalized = EvaluationWorkflowCanonical.Registry(replacement);
                Require(EvaluationWorkflowPolicy.RegistryReplacement(state.Registries[0].Input, state.CurrentRegistry.Input, normalized), EvaluationWorkflowIssue.InvalidInput);
                var injected = new WorkflowRegistry(state.CurrentRegistry.Revision + 1, normalized, J(normalized), H(J(normalized)), at);
                await InsertRegistry(c, t, injected, ct);
                await Execute(c, t, "UPDATE synthetic_evaluation_workflow.workspace SET current_registry_version_id=@id", ct, ("id", normalized.VersionId));
                // Deliberately incomplete injected fixture cannot commit: prove policy recheck, then roll back all.
            }
            var current = await ReadCurrentRegistry(c, t, ct);
            var final = EvaluationWorkflowPolicy.Decide(current, actor, member, action, await Now(c, t, ct));
            Require(EvaluationWorkflowPolicy.Context(command, final), EvaluationWorkflowIssue.Denied);
            Require(replacement is null, EvaluationWorkflowIssue.InvalidInput);
            await Load(c, t, ct);
            await t.CommitAsync(ct);
            return new(null, receipt);
        }
        catch (WorkflowInvalidException e) { return new(e.Issue, null); }
    }
    private static async Task<EvaluationReviewerRegistry> ReadCurrentRegistry(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT r.canonical_json,r.digest FROM synthetic_evaluation_workflow.workspace w JOIN synthetic_evaluation_workflow.registries r ON r.version_id=w.current_registry_version_id WHERE w.singleton=true", c, t);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        Require(await r.ReadAsync(ct));
        var json = r.GetString(0); Require(H(json) == r.GetString(1));
        return EvaluationReviewerPolicy.Capture(EvaluationWorkflowCanonical.Parse<EvaluationReviewerRegistryInput>(json)).Registry ??
            throw new WorkflowInvalidException(EvaluationWorkflowIssue.IntegrityMismatch);
    }
    public async Task<EvaluationWorkflowRegistryResult> UpdateRegistryAsync(string expectedRegistryVersionId,
        EvaluationReviewerRegistryInput replacement, CancellationToken cancellationToken = default)
    {
        if (!EvaluationWorkflowPolicy.Reference(expectedRegistryVersionId)) return new(EvaluationWorkflowIssue.InvalidInput);
        try
        {
            var input = EvaluationWorkflowCanonical.Registry(replacement);
            await using var c = await Open(cancellationToken);
            await using var t = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await Start(c, t, cancellationToken);
            var state = await Load(c, t, cancellationToken);
            Require(state.CurrentRegistry.Input.VersionId == expectedRegistryVersionId, EvaluationWorkflowIssue.RegistryConflict);
            Require(!state.Registries.Any(r => r.Input.VersionId == input.VersionId) &&
                EvaluationWorkflowPolicy.RegistryReplacement(state.Registries[0].Input, state.CurrentRegistry.Input, input), EvaluationWorkflowIssue.InvalidInput);
            Require(state.Revision < 1000, EvaluationWorkflowIssue.WorkflowLimit);
            var at = await Now(c, t, cancellationToken);
            Require(at >= state.Versions[^1].CorrectionCutoffUtc, EvaluationWorkflowIssue.ClockConflict);
            var registry = new WorkflowRegistry(state.CurrentRegistry.Revision + 1, input, J(input), H(J(input)), at);
            await InsertRegistry(c, t, registry, cancellationToken);
            var version = EvaluationWorkflowCanonical.Version(state.Source, state.Sample, state.SourceDigest, state.Revision + 1,
                registry, state.Events, state.Members, at, state.Versions[^1].ContentDigest);
            await InsertVersion(c, t, version, cancellationToken);
            await Execute(c, t, "UPDATE synthetic_evaluation_workflow.workspace SET aggregate_revision=@rev,current_registry_version_id=@registry", cancellationToken,
                ("rev", version.Version), ("registry", input.VersionId));
            await Observe("registry", null, cancellationToken);
            await Load(c, t, cancellationToken);
            await t.CommitAsync(cancellationToken);
            return new(null, input.VersionId, version.Version, version.Version, version.ContentDigest);
        }
        catch (WorkflowInvalidException e) { return new(e.Issue); }
    }
}
