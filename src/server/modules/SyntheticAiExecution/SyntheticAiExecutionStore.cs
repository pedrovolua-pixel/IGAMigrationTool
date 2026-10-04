using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using SyntheticAiValidation;
using SyntheticSourceFence;
using AssessmentCoverage;

namespace SyntheticAiExecution;

public sealed partial class SyntheticAiExecutionStore
{
    public static readonly Guid RegistryFenceId = Guid.Parse("1b000000-0000-4000-8000-000000000001");
    private readonly string connectionString;
    private readonly IAiExecutionCommitObserver? observer;
    public SyntheticAiExecutionStore(string connectionString, IAiExecutionCommitObserver? observer = null)
    {
        var parsed = new NpgsqlConnectionStringBuilder(connectionString); Guard(parsed); this.connectionString = parsed.ConnectionString; this.observer = observer;
    }
    private static void Guard(NpgsqlConnectionStringBuilder c)
    {
        if (c.Host is not ("127.0.0.1" or "localhost" or "::1") || c.Port != 55433 || c.Username != "iga_synthetic" ||
            c.Database is null || !c.Database.StartsWith("iga_synthetic_phase1b_", StringComparison.Ordinal)) throw new ArgumentException("AI requires a dedicated loopback synthetic database.");
    }
    public async Task InitializeAsync(CancellationToken ct = default)
    { await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct); await AiExecutionMigration.Initialize(connection, ct); }
    private sealed record Seed(AiRunLock RunLock, ImmutableArray<AiWork> Works);
    private sealed class State
    {
        public Seed Seed { get; set; } = null!;
        public long Revision { get; set; }
        public long BudgetRevision { get; set; }
        public int RunAllowance { get; set; } = 600;
        public Dictionary<string, int> CategoryAllowances { get; set; } = new(StringComparer.Ordinal);
        public List<AiWorkSnapshot> Works { get; set; } = [];
        public List<AiOverrideEvent> Overrides { get; set; } = [];
    }
    private sealed record Event(Guid Id, string Actor, string CommandDigest, string Receipt);
    private sealed class IntegrityException : Exception { }
    private static AiOperationResult<T> Deny<T>(AiIssue issue) => new(issue, default);
    private async Task<AiIssue?> Prepare(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, CancellationToken ct)
    {
        if (c.State != System.Data.ConnectionState.Open || t.Connection != c || runId == Guid.Empty) return AiIssue.InvalidInput;
        var supplied = new NpgsqlConnectionStringBuilder(c.ConnectionString); var configured = new NpgsqlConnectionStringBuilder(connectionString);
        if (supplied.Host != configured.Host || supplied.Port != configured.Port || supplied.Database != configured.Database || supplied.Username != configured.Username) return AiIssue.WrongScope;
        await using (var order = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND pid=pg_backend_pid() AND objsubid=1 AND ((classid::bigint << 32) | objid::bigint)=@run),EXISTS(SELECT 1 FROM pg_locks WHERE locktype='advisory' AND pid=pg_backend_pid() AND objsubid=1 AND ((classid::bigint << 32) | objid::bigint)=@registry)", c, t))
        {
            order.Parameters.AddWithValue("run", SourceFenceKey(runId)); order.Parameters.AddWithValue("registry", SourceFenceKey(RegistryFenceId));
            await using var reader = await order.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct) && reader.GetBoolean(0) && !reader.GetBoolean(1)) return AiIssue.InvalidInput;
        }
        await SyntheticRunSourceFence.AcquireAsync(c, t, AiScope.Fixed.CustomerId, AiScope.Fixed.ProjectId, AiScope.Fixed.EnvironmentId, RegistryFenceId, ct);
        await SyntheticRunSourceFence.AcquireAsync(c, t, AiScope.Fixed.CustomerId, AiScope.Fixed.ProjectId, AiScope.Fixed.EnvironmentId, runId, ct);
        return await AiExecutionMigration.Verify(c, t, ct);
    }
    private static long SourceFenceKey(Guid runId) => BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    { schemaVersion = "synthetic-source-fence-v1", customerId = AiScope.Fixed.CustomerId, projectId = AiScope.Fixed.ProjectId, environmentId = AiScope.Fixed.EnvironmentId, runId = runId.ToString("D") })));
    private static async Task BudgetFence(NpgsqlConnection c, NpgsqlTransaction t, string epoch, CancellationToken ct)
    {
        var value = AiExecutionCanonical.Digest(new { schemaVersion = "synthetic-ai-budget-fence-v1", scope = AiScope.Fixed, epoch });
        var key = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(value));
        await AiExecutionMigration.Execute(c, t, "SELECT pg_advisory_xact_lock(@key)", ct, ("key", key));
    }
    private static Dictionary<string, (int Charged, int Held)> Totals(State state)
    {
        var counters = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var work in state.Works)
            foreach (var attempt in work.Attempts)
                foreach (var key in Keys(state.Seed.RunLock, work.Work.Category))
                {
                    var previous = counters.GetValueOrDefault(key);
                    counters[key] = (checked(previous.Item1 + (attempt.Receipt?.InputUse ?? 0) + (attempt.Receipt?.OutputUse ?? 0)), checked(previous.Item2 + (attempt.Held ? 300 : 0)));
                }
        foreach (var category in state.CategoryAllowances.Keys) foreach (var key in Keys(state.Seed.RunLock, category)) counters.TryAdd(key, (0, 0));
        return counters;
    }
    private static string[] Keys(AiRunLock locked, string category) =>
        ["run:" + locked.RunId.ToString("D"), "category:" + locked.RunId.ToString("D") + ":" + category,
        "period:" + locked.Epoch, "user:" + locked.Epoch + ":" + locked.InitiatingConsultantId];
    private static int Allowance(State state, string key) => key.StartsWith("run:", StringComparison.Ordinal) ? state.RunAllowance :
        key.StartsWith("category:", StringComparison.Ordinal) ? state.CategoryAllowances[key[(key.LastIndexOf(':') + 1)..]] : 1200;
    private static string EventProof(Guid runId, long revision, Guid eventId, string actor, string kind, string command, string before, string after, string receipt, DateTime recorded) =>
        AiExecutionCanonical.Digest(new { schemaVersion = "synthetic-ai-event-proof-v1", runId, revision, eventId, actor, kind, commandDigest = command, beforeDigest = before, afterDigest = after, receiptCanonical = receipt, recordedAtUtc = new DateTimeOffset(recorded, TimeSpan.Zero) });
    internal static async Task VerifyUnboundJournalEmpty(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT (SELECT count(*) FROM synthetic_ai_execution.run_lock)+(SELECT count(*) FROM synthetic_ai_execution.events)+(SELECT count(*) FROM synthetic_ai_execution.current_state)+(SELECT count(*) FROM synthetic_ai_execution.counter)", c, t);
        if ((long)(await command.ExecuteScalarAsync(ct) ?? throw new IntegrityException()) != 0) throw new IntegrityException();
    }

    private static async Task<State?> Load(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, CancellationToken ct)
    {
        string? seedJson = null; string? stateJson = null; string? stateDigest = null; long revision = 0;
        await using (var command = new NpgsqlCommand("SELECT l.canonical,l.digest,s.canonical,s.digest,s.revision FROM synthetic_ai_execution.run_lock l LEFT JOIN synthetic_ai_execution.current_state s ON s.run_id=l.run_id WHERE l.run_id=@run", c, t))
        {
            command.Parameters.AddWithValue("run", runId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            seedJson = reader.GetString(0); if (AiExecutionCanonical.Hash(seedJson) != reader.GetString(1) || reader.IsDBNull(2)) throw new IntegrityException();
            stateJson = reader.GetString(2); stateDigest = reader.GetString(3); revision = reader.GetInt64(4);
        }
        var seed = AiExecutionCanonical.Parse<Seed>(seedJson); var state = AiExecutionCanonical.Parse<State>(stateJson);
        if (AiExecutionCanonical.Serialize(seed) != seedJson || AiExecutionPolicy.ValidateRun(seed.RunLock, seed.Works) is not null ||
            seed.RunLock.RunId != runId || AiExecutionCanonical.Serialize(state.Seed) != seedJson || AiExecutionCanonical.Hash(stateJson) != stateDigest ||
            AiExecutionCanonical.Serialize(state) != stateJson || state.Revision != revision || revision < 1 || revision > AiExecutionPolicy.MaximumRevision) throw new IntegrityException();
        var previous = ""; long expected = 1;
        await using var events = new NpgsqlCommand("SELECT e.revision,e.before_digest,e.after_digest,e.after_canonical,e.receipt_canonical,e.event_id,e.actor_id,e.kind,e.command_digest,e.recorded_at,p.digest FROM synthetic_ai_execution.events e LEFT JOIN synthetic_ai_execution.event_proof p ON p.event_id=e.event_id WHERE e.run_id=@run ORDER BY e.revision", c, t);
        events.Parameters.AddWithValue("run", runId); await using (var reader = await events.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                if (reader.GetInt64(0) != expected++ || reader.GetString(1) != previous || AiExecutionCanonical.Hash(reader.GetString(3)) != reader.GetString(2)) throw new IntegrityException();
                if (reader.IsDBNull(10) || reader.GetString(10) != EventProof(runId, reader.GetInt64(0), reader.GetGuid(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetString(1), reader.GetString(2), reader.GetString(4), reader.GetDateTime(9))) throw new IntegrityException();
                var historical = AiExecutionCanonical.Parse<State>(reader.GetString(3));
                if (historical.Revision != expected - 1 || AiExecutionCanonical.Serialize(historical.Seed) != seedJson || AiExecutionCanonical.Serialize(historical) != reader.GetString(3)) throw new IntegrityException();
                using var receipt = JsonDocument.Parse(reader.GetString(4));
                previous = reader.GetString(2);
            }
        if (expected - 1 != revision || previous != stateDigest || state.Works.Count != seed.Works.Length ||
            state.Works.Select(x => x.Work.WorkId).Distinct().Count() != state.Works.Count || state.CategoryAllowances.Count != seed.Works.Select(x => x.Category).Distinct().Count()) throw new IntegrityException();
        foreach (var work in state.Works)
        {
            if (AiExecutionCanonical.Serialize(work.Work) != AiExecutionCanonical.Serialize(seed.Works.Single(x => x.WorkId == work.Work.WorkId)) ||
                work.Revision < 1 || work.Revision > AiExecutionPolicy.MaximumRevision || work.Generation < 0 || !Enum.IsDefined(work.State) || work.Attempts.IsDefault || work.Outcomes.IsDefault || work.Attempts.Length > 3) throw new IntegrityException();
            for (var i = 0; i < work.Attempts.Length; i++)
            {
                var attempt = work.Attempts[i];
                if (attempt.Key.Ordinal != i + 1 || attempt.Key.AttemptId == Guid.Empty || attempt.Key.LogicalKey != AiExecutionPolicy.LogicalKey(seed.RunLock, work.Work) ||
                    attempt.Key.PacketDigest != SyntheticAiPacketBuilder.Build(work.Work.PacketInputJson).Packet!.ContentDigest ||
                    attempt.PacketInputJson != work.Work.PacketInputJson || attempt.WorkerGeneration < 1 || attempt.WorkRevision < 1 ||
                    attempt.Receipt is not null && (attempt.Held || ValidateReceipt(attempt.Key, attempt.Receipt, true) is not null)) throw new IntegrityException();
            }
            if (work.State is AiWorkState.Succeeded or AiWorkState.Failed && work.Outcomes.Length != work.Work.Units.Length) throw new IntegrityException();
            if (work.Outcomes.Select(x => x.Key).Distinct().Count() != work.Outcomes.Length || work.Outcomes.Any(x => !work.Work.Units.Any(u => u.Key == x.Key))) throw new IntegrityException();
            foreach (var result in work.Outcomes)
                if (!Enum.IsDefined(result.State) || result.State == CoverageState.Finding && (result.Finding is null || result.Finding.State != "Proposed" ||
                    result.Finding.OriginalDigest != AiExecutionCanonical.Digest(result.Finding with { OriginalDigest = "" })) ||
                    result.State != CoverageState.Finding && result.Finding is not null) throw new IntegrityException();
            foreach (var attempt in work.Attempts)
            {
                await using var saved = new NpgsqlCommand("SELECT canonical,digest,source_json FROM synthetic_ai_execution.accepted_snapshot WHERE attempt_id=@attempt", c, t); saved.Parameters.AddWithValue("attempt", attempt.Key.AttemptId);
                string? acceptedJson = null; string? acceptedDigest = null; string? sourceJson = null;
                await using (var reader = await saved.ExecuteReaderAsync(ct)) if (await reader.ReadAsync(ct)) { acceptedJson = reader.GetString(0); acceptedDigest = reader.GetString(1); sourceJson = reader.GetString(2); }
                if (acceptedJson is null) { if (work.State == AiWorkState.Succeeded && attempt == work.Attempts[^1]) throw new IntegrityException(); continue; }
                var packet = SyntheticAiPacketBuilder.Build(work.Work.PacketInputJson); var validated = SyntheticAiProposalValidator.Validate(packet.Packet, sourceJson);
                var mapped = AiExecutionPolicy.Map(seed.RunLock, work.Work, attempt.Key, sourceJson);
                if (work.State != AiWorkState.Succeeded || attempt != work.Attempts[^1] || attempt.Receipt?.Outcome != AiProviderOutcome.Response ||
                    !validated.Succeeded || acceptedJson != validated.Snapshot!.CanonicalJson || acceptedDigest != validated.Snapshot.ContentDigest || AiExecutionCanonical.Hash(sourceJson!) != attempt.Receipt.OutputDigest || !mapped.Succeeded ||
                    AiExecutionCanonical.Serialize(mapped.Value) != AiExecutionCanonical.Serialize(work.Outcomes)) throw new IntegrityException();
            }
        }
        return state;
    }
    private static async Task<Dictionary<string, (int Charged, int Held)>> VerifyCounters(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        var ids = new List<Guid>(); await using (var command = new NpgsqlCommand("SELECT run_id FROM synthetic_ai_execution.run_lock ORDER BY run_id", c, t))
        { await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0)); }
        var expected = new Dictionary<string, (int Charged, int Held)>(StringComparer.Ordinal);
        var expectedSnapshots = new HashSet<Guid>();
        foreach (var id in ids)
        {
            var state = await Load(c, t, id, ct) ?? throw new IntegrityException();
            foreach (var work in state.Works.Where(x => x.State == AiWorkState.Succeeded)) if (!expectedSnapshots.Add(work.Attempts[^1].Key.AttemptId)) throw new IntegrityException();
            foreach (var pair in Totals(state)) { var old = expected.GetValueOrDefault(pair.Key); expected[pair.Key] = (checked(old.Charged + pair.Value.Charged), checked(old.Held + pair.Value.Held)); }
        }
        var actual = new Dictionary<string, (int Charged, int Held)>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("SELECT counter_key,charged,held,digest FROM synthetic_ai_execution.counter ORDER BY counter_key FOR UPDATE", c, t))
        { await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) { var key = reader.GetString(0); var charged = reader.GetInt32(1); var held = reader.GetInt32(2); if (AiExecutionCanonical.Digest(new { key, charged, held }) != reader.GetString(3)) throw new IntegrityException(); actual.Add(key, (charged, held)); } }
        if (actual.Count != expected.Count || expected.Any(x => !actual.TryGetValue(x.Key, out var count) || count != x.Value)) throw new IntegrityException();
        await using (var snapshots = new NpgsqlCommand("SELECT attempt_id FROM synthetic_ai_execution.accepted_snapshot ORDER BY attempt_id", c, t))
        {
            var actualSnapshots = new HashSet<Guid>(); await using var reader = await snapshots.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) actualSnapshots.Add(reader.GetGuid(0));
            if (!actualSnapshots.SetEquals(expectedSnapshots)) throw new IntegrityException();
        }
        return actual;
    }
    private static async Task<Event?> FindEvent(NpgsqlConnection c, NpgsqlTransaction t, Guid eventId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT event_id,actor_id,command_digest,receipt_canonical FROM synthetic_ai_execution.events WHERE event_id=@id", c, t); command.Parameters.AddWithValue("id", eventId);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)) : null;
    }
    private static Guid EventId(AiAttemptKey key, string kind)
    { var bytes = Convert.FromHexString(AiExecutionCanonical.Digest(new { key, kind })); return new Guid(bytes.AsSpan(0, 16)); }
    private async Task Save<T>(NpgsqlConnection c, NpgsqlTransaction t, State state, string before, Guid eventId, string actor, string kind, string commandDigest, T receipt, CancellationToken ct)
    {
        state.Revision = checked(state.Revision + 1); if (state.Revision > AiExecutionPolicy.MaximumRevision) throw new IntegrityException();
        if (observer is not null) await observer.BeforeWriteAsync(kind, state.Seed.RunLock.RunId, ct);
        var canonical = AiExecutionCanonical.Serialize(state); var digest = AiExecutionCanonical.Hash(canonical);
        await AiExecutionMigration.Execute(c, t, "INSERT INTO synthetic_ai_execution.events VALUES(@run,@revision,@event,@actor,@kind,@command,@before,@after,@canonical,@receipt,clock_timestamp())", ct,
            ("run", state.Seed.RunLock.RunId), ("revision", state.Revision), ("event", eventId), ("actor", actor), ("kind", kind), ("command", commandDigest), ("before", before), ("after", digest), ("canonical", canonical), ("receipt", AiExecutionCanonical.Serialize(receipt)));
        await using (var clock = new NpgsqlCommand("SELECT recorded_at FROM synthetic_ai_execution.events WHERE event_id=@event", c, t))
        {
            clock.Parameters.AddWithValue("event", eventId); var recorded = (DateTime)(await clock.ExecuteScalarAsync(ct) ?? throw new IntegrityException());
            await AiExecutionMigration.Execute(c, t, "INSERT INTO synthetic_ai_execution.event_proof VALUES(@id,@digest)", ct, ("id", eventId),
                ("digest", EventProof(state.Seed.RunLock.RunId, state.Revision, eventId, actor, kind, commandDigest, before, digest, AiExecutionCanonical.Serialize(receipt), recorded)));
        }
        await AiExecutionMigration.Execute(c, t, "INSERT INTO synthetic_ai_execution.current_state VALUES(@run,@revision,@canonical,@digest) ON CONFLICT(run_id) DO UPDATE SET revision=EXCLUDED.revision,canonical=EXCLUDED.canonical,digest=EXCLUDED.digest", ct,
            ("run", state.Seed.RunLock.RunId), ("revision", state.Revision), ("canonical", canonical), ("digest", digest));
        var expected = new Dictionary<string, (int Charged, int Held)>(StringComparer.Ordinal);
        var ids = new List<Guid>(); await using (var command = new NpgsqlCommand("SELECT run_id FROM synthetic_ai_execution.run_lock ORDER BY run_id", c, t))
        { await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) ids.Add(reader.GetGuid(0)); }
        foreach (var id in ids) foreach (var entry in Totals((await Load(c, t, id, ct))!))
        { var old = expected.GetValueOrDefault(entry.Key); expected[entry.Key] = (checked(old.Charged + entry.Value.Charged), checked(old.Held + entry.Value.Held)); }
        foreach (var entry in expected.OrderBy(x => x.Key, StringComparer.Ordinal))
            await AiExecutionMigration.Execute(c, t, "INSERT INTO synthetic_ai_execution.counter VALUES(@key,@charged,@held,@digest) ON CONFLICT(counter_key) DO UPDATE SET charged=EXCLUDED.charged,held=EXCLUDED.held,digest=EXCLUDED.digest", ct,
                ("key", entry.Key), ("charged", entry.Value.Charged), ("held", entry.Value.Held), ("digest", AiExecutionCanonical.Digest(new { key = entry.Key, charged = entry.Value.Charged, held = entry.Value.Held })));
    }
    private static bool IntegrityFailure(Exception x) => x is IntegrityException or JsonException or InvalidOperationException or ArgumentException or OverflowException;
    public async Task<AiOperationResult<AiExecutionSnapshot>> EnsureRunAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, AiRunLock locked, ImmutableArray<AiWork> works, CancellationToken ct = default)
    {
        if (AiExecutionPolicy.Authorize(authority, AiAction.Dispatch) is { } denied) return Deny<AiExecutionSnapshot>(denied);
        if (AiExecutionPolicy.ValidateRun(locked, works) is { } issue) return Deny<AiExecutionSnapshot>(issue);
        if (works.Any(x => !authority.Categories.Contains(x.Category))) return Deny<AiExecutionSnapshot>(AiIssue.Denied);
        if (await Prepare(c, t, locked.RunId, ct) is { } error) return Deny<AiExecutionSnapshot>(error);
        await BudgetFence(c, t, locked.Epoch, ct);
        try
        {
            var state = await Load(c, t, locked.RunId, ct); var counters = await VerifyCounters(c, t, ct);
            var seed = new Seed(locked, works);
            if (state is not null) return AiExecutionCanonical.Serialize(state.Seed) == AiExecutionCanonical.Serialize(seed) ? new(null, Snapshot(state, counters), true) : Deny<AiExecutionSnapshot>(AiIssue.SeedConflict);
            var seedJson = AiExecutionCanonical.Serialize(seed);
            await AiExecutionMigration.Execute(c, t, "INSERT INTO synthetic_ai_execution.run_lock VALUES(@run,@canonical,@digest)", ct, ("run", locked.RunId), ("canonical", seedJson), ("digest", AiExecutionCanonical.Hash(seedJson)));
            state = new State { Seed = seed, Works = works.Select(x => new AiWorkSnapshot(x, AiWorkState.Pending, 1, 0, [], [])).ToList(), CategoryAllowances = works.Select(x => x.Category).Distinct().ToDictionary(x => x, _ => 600, StringComparer.Ordinal) };
            await Save(c, t, state, "", Guid.NewGuid(), authority.ActorId, "EnsureRun", AiExecutionCanonical.Digest(seed), true, ct);
            return new(null, Snapshot(state, await VerifyCounters(c, t, ct)));
        }
        catch (Exception x) when (IntegrityFailure(x)) { return Deny<AiExecutionSnapshot>(AiIssue.IntegrityMismatch); }
    }
    private static AiExecutionSnapshot Snapshot(State state, Dictionary<string, (int Charged, int Held)> counters)
    {
        var relevant = state.CategoryAllowances.Keys.SelectMany(x => Keys(state.Seed.RunLock, x)).Distinct().Order(StringComparer.Ordinal).Select(key =>
            new AiCounter(key, counters.GetValueOrDefault(key).Charged, counters.GetValueOrDefault(key).Held, Allowance(state, key), 1200)).ToImmutableArray();
        var snapshot = new AiExecutionSnapshot(state.Seed.RunLock, state.Works.ToImmutableArray(), new(state.BudgetRevision, state.RunAllowance,
            state.CategoryAllowances.OrderBy(x => x.Key, StringComparer.Ordinal).ToImmutableArray(), relevant, state.Overrides.ToImmutableArray()), "");
        return snapshot with { ContentDigest = AiExecutionCanonical.Digest(snapshot) };
    }
    public async Task<AiOperationResult<AiExecutionSnapshot>> ReadInTransactionAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, CancellationToken ct = default)
    {
        if (AiExecutionPolicy.Authorize(authority, AiAction.Read) is { } denied) return Deny<AiExecutionSnapshot>(denied);
        if (await Prepare(c, t, runId, ct) is { } error) return Deny<AiExecutionSnapshot>(error);
        try
        {
            var state = await Load(c, t, runId, ct); if (state is null) return Deny<AiExecutionSnapshot>(AiIssue.NotFound);
            if (state.Works.Any(x => !authority.Categories.Contains(x.Work.Category))) return Deny<AiExecutionSnapshot>(AiIssue.Denied);
            await BudgetFence(c, t, state.Seed.RunLock.Epoch, ct); return new(null, Snapshot(state, await VerifyCounters(c, t, ct)));
        }
        catch (Exception x) when (IntegrityFailure(x)) { return Deny<AiExecutionSnapshot>(AiIssue.IntegrityMismatch); }
    }
    private async Task<AiOperationResult<T>> Mutate<T>(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId,
        AiAction action, bool billingOnly, Guid eventId, string kind, object command, Func<State, AiWorkSnapshot, Dictionary<string, (int Charged, int Held)>, Task<AiOperationResult<T>>> apply, CancellationToken ct)
    {
        if (eventId == Guid.Empty || !AiExecutionPolicy.ValidText(workId, 256)) return Deny<T>(AiIssue.InvalidInput);
        if (AiExecutionPolicy.Authorize(authority, action, billingOnly: billingOnly) is { } denied) return Deny<T>(denied);
        if (await Prepare(c, t, runId, ct) is { } error) return Deny<T>(error);
        try
        {
            var state = await Load(c, t, runId, ct); if (state is null) return Deny<T>(AiIssue.NotFound);
            var work = state.Works.SingleOrDefault(x => x.Work.WorkId == workId); if (work is null) return Deny<T>(AiIssue.NotFound);
            if (!authority.Categories.Contains(work.Work.Category)) return Deny<T>(AiIssue.Denied);
            await BudgetFence(c, t, state.Seed.RunLock.Epoch, ct); var counters = await VerifyCounters(c, t, ct);
            var commandDigest = AiExecutionCanonical.Digest(new { authority.ActorId, runId, workId, kind, command });
            var old = await FindEvent(c, t, eventId, ct);
            if (old is not null)
            {
                if (old.Actor != authority.ActorId || old.CommandDigest != commandDigest) return Deny<T>(AiIssue.EventConflict);
                var replay = AiExecutionCanonical.Parse<T>(old.Receipt);
                if (billingOnly && replay is AiCompletion completion) replay = (T)(object)new AiCompletion([], true, completion.ReceiptId);
                return new(null, replay, true);
            }
            var before = AiExecutionCanonical.Digest(state); var result = await apply(state, work, counters);
            if (!result.Succeeded) return result;
            await Save(c, t, state, before, eventId, authority.ActorId, kind, commandDigest, result.Value!, ct); return result;
        }
        catch (Exception x) when (IntegrityFailure(x)) { return Deny<T>(AiIssue.IntegrityMismatch); }
    }
    private static void Replace(State state, AiWorkSnapshot work) => state.Works[state.Works.FindIndex(x => x.Work.WorkId == work.Work.WorkId)] = work;
    private static AiIssue? Revision(AiWorkSnapshot work, long expected) => expected < 1 || expected > AiExecutionPolicy.MaximumRevision ? AiIssue.InvalidInput : expected != work.Revision ? AiIssue.RevisionConflict : null;
    public Task<AiOperationResult<AiAttemptSnapshot>> ReserveAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, Guid attemptId, long expectedRevision, CancellationToken ct = default) =>
        Mutate(c, t, authority, runId, workId, AiAction.Dispatch, false, attemptId, "Reserve", new { attemptId, expectedRevision }, (state, work, counters) =>
        {
            if (Revision(work, expectedRevision) is { } error) return Task.FromResult(Deny<AiAttemptSnapshot>(error));
            if (work.State is not (AiWorkState.Pending or AiWorkState.Retryable)) return Task.FromResult(Deny<AiAttemptSnapshot>(AiIssue.InvalidState));
            if (work.Attempts.Length >= 3) return Task.FromResult(Deny<AiAttemptSnapshot>(AiIssue.RetryExhausted));
            foreach (var key in Keys(state.Seed.RunLock, work.Work.Category))
            { var value = counters.GetValueOrDefault(key); if (checked(value.Charged + value.Held + 300) > Allowance(state, key)) return Task.FromResult(Deny<AiAttemptSnapshot>(AiIssue.BudgetExhausted)); }
            var attempt = new AiAttemptSnapshot(new(AiExecutionPolicy.LogicalKey(state.Seed.RunLock, work.Work), attemptId, work.Attempts.Length + 1,
                SyntheticAiPacketBuilder.Build(work.Work.PacketInputJson).Packet!.ContentDigest), AiWorkState.Reserved, true, null, work.Work.PacketInputJson, work.Generation + 1, work.Revision + 1);
            Replace(state, work with { State = AiWorkState.Reserved, Revision = work.Revision + 1, Generation = work.Generation + 1, Attempts = work.Attempts.Add(attempt) });
            return Task.FromResult(new AiOperationResult<AiAttemptSnapshot>(null, attempt));
        }, ct);
    private Task<AiOperationResult<AiAttemptSnapshot>> Mark(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, AiAttemptKey key, long expectedRevision, bool unknown, CancellationToken ct) =>
        Mutate(c, t, authority, runId, workId, AiAction.Dispatch, false, EventId(key, unknown ? "Unknown" : "Dispatched"), unknown ? "Unknown" : "Dispatched", new { key, expectedRevision }, (state, work, _) =>
        {
            if (Revision(work, expectedRevision) is { } error) return Task.FromResult(Deny<AiAttemptSnapshot>(error));
            var attempt = work.Attempts.LastOrDefault();
            if (attempt is null || attempt.Key != key || attempt.WorkerGeneration != work.Generation || !attempt.Held || work.State != (unknown ? AiWorkState.Dispatched : AiWorkState.Reserved))
                return Task.FromResult(Deny<AiAttemptSnapshot>(AiIssue.InvalidState));
            attempt = attempt with { State = unknown ? AiWorkState.Unknown : AiWorkState.Dispatched, WorkRevision = work.Revision + 1 };
            Replace(state, work with { State = attempt.State, Revision = work.Revision + 1, Attempts = work.Attempts.SetItem(work.Attempts.Length - 1, attempt) });
            return Task.FromResult(new AiOperationResult<AiAttemptSnapshot>(null, attempt));
        }, ct);
    public Task<AiOperationResult<AiAttemptSnapshot>> MarkDispatchedAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, AiAttemptKey key, long expectedRevision, CancellationToken ct = default) => Mark(c, t, authority, runId, workId, key, expectedRevision, false, ct);
    public Task<AiOperationResult<AiAttemptSnapshot>> MarkUnknownAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, AiAttemptKey key, long expectedRevision, CancellationToken ct = default) => Mark(c, t, authority, runId, workId, key, expectedRevision, true, ct);
    private static AiIssue? ValidateReceipt(AiAttemptKey key, AiProviderReceipt receipt, bool billingOnly)
    {
        if (receipt is null || receipt.Attempt != key || !Enum.IsDefined(receipt.Outcome) || receipt.Outcome == AiProviderOutcome.Unknown ||
            receipt.InputUse is null or < 0 or > 100 || receipt.OutputUse is null or < 0 or > 200 ||
            receipt.ReceiptId != FakeAiProvider.ReceiptIdentity(key, receipt.Outcome, receipt.InputUse, receipt.OutputUse)) return AiIssue.InvalidReceipt;
        if (receipt.Outcome == AiProviderOutcome.RetryableFailure && (receipt.OutputJson is not null || receipt.OutputDigest is not null)) return AiIssue.InvalidReceipt;
        if (!billingOnly && receipt.Outcome == AiProviderOutcome.Response && (receipt.OutputJson is null || receipt.OutputDigest != AiExecutionCanonical.Hash(receipt.OutputJson))) return AiIssue.InvalidReceipt;
        if (receipt.OutputJson is not null && receipt.OutputDigest != AiExecutionCanonical.Hash(receipt.OutputJson)) return AiIssue.InvalidReceipt;
        return null;
    }
    public Task<AiOperationResult<AiCompletion>> CompleteAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, AiAttemptKey key, AiProviderReceipt receipt, CancellationToken ct = default) =>
        Mutate(c, t, authority, runId, workId, AiAction.Reconcile, authority.ResourceState != AiResourceState.Mutable || !authority.AiPolicyAllowed,
            EventId(key, "Complete"), "Complete", new { key, receipt }, async (state, work, _) =>
        {
            var index = Array.FindIndex(work.Attempts.ToArray(), x => x.Key == key); if (index < 0) return Deny<AiCompletion>(AiIssue.InvalidReceipt);
            var attempt = work.Attempts[index]; var billingOnly = work.State is AiWorkState.Succeeded or AiWorkState.Failed or AiWorkState.Cancelled || authority.ResourceState != AiResourceState.Mutable || !authority.AiPolicyAllowed;
            if (ValidateReceipt(key, receipt, billingOnly) is { } error) return Deny<AiCompletion>(error);
            if (!attempt.Held || attempt.Receipt is not null || attempt.State == AiWorkState.Reserved) return Deny<AiCompletion>(AiIssue.InvalidState);
            var storedReceipt = receipt with { OutputJson = null };
            var outcomes = work.Outcomes; var next = work.State;
            if (!billingOnly)
            {
                if (receipt.Outcome == AiProviderOutcome.RetryableFailure)
                { next = key.Ordinal == 3 ? AiWorkState.Failed : AiWorkState.Retryable; if (next == AiWorkState.Failed) outcomes = AiExecutionPolicy.Gaps(work.Work, CoverageState.Error, "AI_RETRY_EXHAUSTED"); }
                else
                {
                    var mapped = AiExecutionPolicy.Map(state.Seed.RunLock, work.Work, key, receipt.OutputJson);
                    outcomes = mapped.Succeeded ? mapped.Value : AiExecutionPolicy.Gaps(work.Work, CoverageState.Error, "AI_OUTPUT_REJECTED");
                    next = mapped.Succeeded ? AiWorkState.Succeeded : AiWorkState.Failed;
                    if (mapped.Succeeded)
                    {
                        var accepted = SyntheticAiProposalValidator.Validate(SyntheticAiPacketBuilder.Build(work.Work.PacketInputJson).Packet, receipt.OutputJson).Snapshot!;
                        await AiExecutionMigration.Execute(c, t, "INSERT INTO synthetic_ai_execution.accepted_snapshot VALUES(@id,@canonical,@digest,@source)", ct,
                            ("id", key.AttemptId), ("canonical", accepted.CanonicalJson), ("digest", accepted.ContentDigest), ("source", receipt.OutputJson!));
                    }
                }
            }
            attempt = attempt with { Held = false, Receipt = storedReceipt, State = next, WorkRevision = work.Revision + 1 };
            Replace(state, work with { State = next, Revision = work.Revision + 1, Attempts = work.Attempts.SetItem(index, attempt), Outcomes = outcomes });
            return new AiOperationResult<AiCompletion>(null, new(billingOnly ? [] : outcomes, billingOnly, receipt.ReceiptId));
        }, ct);
    public Task<AiOperationResult<ImmutableArray<AiUnitOutcome>>> RecordTerminalGapAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId,
        long expectedRevision, CoverageState state, string reason, CancellationToken ct = default) =>
        Mutate(c, t, authority, runId, workId, AiAction.Reconcile, !authority.AiPolicyAllowed && reason is "AI_DISABLED" or "AI_EXCLUDED_SOURCE", new Guid(Convert.FromHexString(AiExecutionCanonical.Digest(new { runId, workId, expectedRevision, state, reason })).AsSpan(0, 16)), "Gap", new { expectedRevision, state, reason }, (stored, work, counters) =>
        {
            if (Revision(work, expectedRevision) is { } error) return Task.FromResult(Deny<ImmutableArray<AiUnitOutcome>>(error));
            var expectedState = reason switch
            {
                "AI_BUDGET_EXHAUSTED" => CoverageState.NotAssessed,
                "AI_UNKNOWN_OUTCOME" => CoverageState.Error,
                "AI_DISABLED" or "AI_EXCLUDED_SOURCE" => CoverageState.Excluded,
                "AI_INSUFFICIENT_SOURCE" or "AI_CONFLICTING_SOURCE" => CoverageState.InsufficientEvidence,
                "AI_UNSUPPORTED_SOURCE" => CoverageState.Unsupported,
                "AI_INACCESSIBLE_SOURCE" => CoverageState.Inaccessible,
                "AI_REDACTED_SOURCE" => CoverageState.Redacted,
                _ => (CoverageState?)null
            };
            if (expectedState != state || authority.ResourceState != AiResourceState.Mutable ||
                (reason == "AI_UNKNOWN_OUTCOME" ? work.State is not (AiWorkState.Dispatched or AiWorkState.Unknown) : work.State is not (AiWorkState.Pending or AiWorkState.Retryable)) ||
                reason == "AI_BUDGET_EXHAUSTED" && !Keys(stored.Seed.RunLock, work.Work.Category).Any(key =>
                { var value = counters.GetValueOrDefault(key); return checked(value.Charged + value.Held + 300) > Allowance(stored, key); }))
                return Task.FromResult(Deny<ImmutableArray<AiUnitOutcome>>(AiIssue.InvalidState));
            var outcomes = AiExecutionPolicy.Gaps(work.Work, state, reason); Replace(stored, work with { State = AiWorkState.Failed, Revision = work.Revision + 1, Outcomes = outcomes });
            return Task.FromResult(new AiOperationResult<ImmutableArray<AiUnitOutcome>>(null, outcomes));
        }, ct);
    public Task<AiOperationResult<AiOverrideEvent>> OverrideAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, AiOverrideCommand command, CancellationToken ct = default)
    {
        if (command is null || command.EventId == Guid.Empty || command.ExpectedRevision < 0 || command.ExpectedRevision > AiExecutionPolicy.MaximumRevision ||
            command.RunTarget is not (900 or 1200) || command.CategoryTarget is not (900 or 1200) || !AiExecutionPolicy.ValidText(command.Reason) || command.Category is not ("SECURITY" or "OPERATIONS"))
            return Task.FromResult(Deny<AiOverrideEvent>(AiIssue.InvalidInput));
        return OverrideCore();
        async Task<AiOperationResult<AiOverrideEvent>> OverrideCore()
        {
            if (AiExecutionPolicy.Authorize(authority, AiAction.Override, command.Category) is { } denied) return Deny<AiOverrideEvent>(denied);
            if (await Prepare(c, t, runId, ct) is { } error) return Deny<AiOverrideEvent>(error);
            try
            {
                var state = await Load(c, t, runId, ct); if (state is null) return Deny<AiOverrideEvent>(AiIssue.NotFound);
                await BudgetFence(c, t, state.Seed.RunLock.Epoch, ct); await VerifyCounters(c, t, ct);
                var digest = AiExecutionCanonical.Digest(new { authority.ActorId, runId, command }); var old = await FindEvent(c, t, command.EventId, ct);
                if (old is not null) return old.Actor == authority.ActorId && old.CommandDigest == digest ? new(null, AiExecutionCanonical.Parse<AiOverrideEvent>(old.Receipt), true) : Deny<AiOverrideEvent>(AiIssue.EventConflict);
                if (!state.CategoryAllowances.TryGetValue(command.Category, out var current)) return Deny<AiOverrideEvent>(AiIssue.InvalidInput);
                if (command.ExpectedRevision != state.BudgetRevision) return Deny<AiOverrideEvent>(AiIssue.RevisionConflict);
                if (command.RunTarget < state.RunAllowance || command.CategoryTarget < current || command.RunTarget == state.RunAllowance && command.CategoryTarget == current ||
                    !state.Works.Any(x => x.Work.Category == command.Category && x.State is AiWorkState.Pending or AiWorkState.Retryable)) return Deny<AiOverrideEvent>(AiIssue.InvalidState);
                var before = AiExecutionCanonical.Digest(state); state.BudgetRevision = checked(state.BudgetRevision + 1);
                await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", c, t); var time = (DateTime)(await clock.ExecuteScalarAsync(ct) ?? throw new IntegrityException());
                var item = new AiOverrideEvent(command.EventId, state.BudgetRevision, authority.ActorId, new DateTimeOffset(time, TimeSpan.Zero), command.RunTarget, command.CategoryTarget, command.Category, command.Reason);
                state.RunAllowance = command.RunTarget; state.CategoryAllowances[command.Category] = command.CategoryTarget; state.Overrides.Add(item);
                await Save(c, t, state, before, command.EventId, authority.ActorId, "Override", digest, item, ct); return new(null, item);
            }
            catch (Exception x) when (IntegrityFailure(x)) { return Deny<AiOverrideEvent>(AiIssue.IntegrityMismatch); }
        }
    }
    public Task<AiOperationResult<bool>> CancelPendingAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, long expectedRevision, CancellationToken ct = default) =>
        Mutate(c, t, authority, runId, workId, AiAction.Reconcile, authority.ResourceState != AiResourceState.Mutable || !authority.AiPolicyAllowed,
            new Guid(Convert.FromHexString(AiExecutionCanonical.Digest(new { runId, workId, expectedRevision, kind = "Cancel" })).AsSpan(0, 16)), "Cancel", new { expectedRevision }, (state, work, _) =>
        {
            if (Revision(work, expectedRevision) is { } error) return Task.FromResult(Deny<bool>(error));
            if (work.State is AiWorkState.Succeeded or AiWorkState.Failed or AiWorkState.Cancelled) return Task.FromResult(Deny<bool>(AiIssue.InvalidState));
            var attempts = work.Attempts;
            if (!attempts.IsEmpty && attempts[^1].State == AiWorkState.Reserved)
            {
                var attempt = attempts[^1];
                var receipt = new AiProviderReceipt(FakeAiProvider.ReceiptIdentity(attempt.Key, AiProviderOutcome.RetryableFailure, 0, 0), attempt.Key, AiProviderOutcome.RetryableFailure, 0, 0, null, null);
                attempts = attempts.SetItem(attempts.Length - 1, attempt with { Held = false, Receipt = receipt, State = AiWorkState.Cancelled, WorkRevision = work.Revision + 1 });
            }
            Replace(state, work with { State = AiWorkState.Cancelled, Revision = work.Revision + 1, Attempts = attempts, Outcomes = AiExecutionPolicy.Gaps(work.Work, CoverageState.Excluded, "AI_CANCELLED") });
            return Task.FromResult(new AiOperationResult<bool>(null, true));
        }, ct);
    public async Task<AiOperationResult<AiRecovery>> GetRecoveryAsync(NpgsqlConnection c, NpgsqlTransaction t, AiAuthority authority, Guid runId, string workId, CancellationToken ct = default)
    {
        var billingOnly = authority.ResourceState != AiResourceState.Mutable || !authority.AiPolicyAllowed;
        if (AiExecutionPolicy.Authorize(authority, AiAction.Reconcile, billingOnly: billingOnly) is { } denied) return Deny<AiRecovery>(denied);
        if (await Prepare(c, t, runId, ct) is { } error) return Deny<AiRecovery>(error);
        try
        {
            var state = await Load(c, t, runId, ct); if (state is null) return Deny<AiRecovery>(AiIssue.NotFound);
            var work = state.Works.SingleOrDefault(x => x.Work.WorkId == workId); if (work is null) return Deny<AiRecovery>(AiIssue.NotFound);
            if (!authority.Categories.Contains(work.Work.Category)) return Deny<AiRecovery>(AiIssue.Denied);
            await BudgetFence(c, t, state.Seed.RunLock.Epoch, ct); await VerifyCounters(c, t, ct);
            var attempt = work.Attempts.LastOrDefault(x => x.Held); if (attempt is null) return Deny<AiRecovery>(AiIssue.InvalidState);
            billingOnly |= work.State is AiWorkState.Succeeded or AiWorkState.Failed or AiWorkState.Cancelled;
            return new(null, new(attempt.Key, work.Work.Scenario, billingOnly ? null : work.Work.PacketInputJson, billingOnly ? [] : work.Work.Units, billingOnly));
        }
        catch (Exception x) when (IntegrityFailure(x)) { return Deny<AiRecovery>(AiIssue.IntegrityMismatch); }
    }
    public async Task<AiOperationResult<AiExportVerification>> VerifyForExportInTransactionAsync(NpgsqlConnection c, NpgsqlTransaction t, AiExportAuthority authority, Guid runId, CancellationToken ct = default)
    {
        if (authority is null || authority.Scope != AiScope.Fixed || !AiExecutionPolicy.ValidText(authority.ActorId, 256) || !authority.Authenticated || !authority.Active ||
            !authority.AssignmentActive || authority.Revoked || !authority.ExportGranted || !authority.CustomerExportAllowed || authority.ResourceState != AiResourceState.Mutable ||
            authority.Roles.IsDefaultOrEmpty || !(authority.Roles.SequenceEqual([AiRole.Consultant]) || authority.Roles.SequenceEqual([AiRole.Auditor]) && authority.AuditorScopedGrant) ||
            authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(x => x is not ("SECURITY" or "OPERATIONS"))) return Deny<AiExportVerification>(AiIssue.Denied);
        if (await Prepare(c, t, runId, ct) is { } error) return Deny<AiExportVerification>(error);
        try
        {
            var state = await Load(c, t, runId, ct); if (state is null) return Deny<AiExportVerification>(AiIssue.NotFound);
            if (state.Works.Any(x => !authority.Categories.Contains(x.Work.Category) || x.State is not (AiWorkState.Succeeded or AiWorkState.Failed))) return Deny<AiExportVerification>(AiIssue.Denied);
            await BudgetFence(c, t, state.Seed.RunLock.Epoch, ct); var snapshot = Snapshot(state, await VerifyCounters(c, t, ct));
            return new(null, new(state.Seed.RunLock, state.Works.SelectMany(x => x.Outcomes).Select(x => new AiExportUnitBinding(x.Key, x.State, x.ReasonCode,
                x.Finding?.OccurrenceId, x.Finding?.OriginalDigest, x.Finding?.PacketDigest, x.Finding?.ProposalDigest, x.Finding?.Attempt)).ToImmutableArray(), snapshot.ContentDigest));
        }
        catch (Exception x) when (IntegrityFailure(x)) { return Deny<AiExportVerification>(AiIssue.IntegrityMismatch); }
    }
}
