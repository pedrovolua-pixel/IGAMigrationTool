using System.Collections.Immutable;
using System.Text.Json;
using Npgsql;
using SyntheticSourceFence;

namespace SyntheticFixReview;

/// <summary>Append-only local fictional attestations; no customer identity or remediation authority.</summary>
public sealed class SyntheticFixReviewStore
{
    private readonly string connectionString;
    private readonly ArtifactReviewSourceReader readSource;
    private readonly ISyntheticFixReviewCommitObserver? observer;
    private readonly ArtifactReviewScope scope;
    public SyntheticFixReviewStore(string connectionString, ArtifactReviewScope trustedScope,
        ArtifactReviewSourceReader readSource, ISyntheticFixReviewCommitObserver? observer = null)
    {
        if (trustedScope != ArtifactReviewScope.Fixed) throw new ArgumentException("Only the fixed fictional scope is supported.", nameof(trustedScope));
        ArgumentNullException.ThrowIfNull(readSource);
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database is null ||
            !connection.Database.StartsWith("iga_synthetic_", StringComparison.Ordinal))
            throw new ArgumentException("Artifact review requires a loopback iga_synthetic_ database.", nameof(connectionString));
        connection.CommandTimeout = 15;
        this.connectionString = connection.ConnectionString;
        scope = trustedScope;
        this.readSource = readSource;
        this.observer = observer;
    }
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await Open(cancellationToken);
        await SyntheticFixReviewMigration.InitializeAsync(connection, scope, cancellationToken);
    }
    public async Task<ArtifactReviewReadResult> ReadAsync(Guid runId, ArtifactReviewAuthority authority, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty) return new(ArtifactReviewIssue.InvalidInput, null);
        if (ArtifactReviewPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticFixReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var capture = await Capture(runId, authority, cancellationToken);
            if (!capture.Succeeded) return new(capture.Issue, null);
            var source = capture.Source!;
            var entries = await ReadEntries(connection, transaction, source, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, new(source.Binding, authority.ActorId, entries.Select(item => item.Entry).ToImmutableArray()));
        }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(ArtifactReviewIssue.IntegrityMismatch, null); }
    }
    /// <summary>Owning attestation read within a caller's source transaction; never opens, starts, commits or invokes a callback.</summary>
    public async Task<ArtifactReviewReadResult> ReadInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid runId, ArtifactReviewAuthority authority, ArtifactReviewSource source, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || source is null) return new(ArtifactReviewIssue.InvalidInput, null);
        if (ArtifactReviewPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString);
        var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != System.Data.ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username)
            return new(ArtifactReviewIssue.InvalidInput, null);
        await SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticFixReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var rebuilt = ArtifactReviewSourceBuilder.Build(source.Packages);
            if (!rebuilt.Succeeded || ArtifactReviewCanonical.Json(source.Binding) != ArtifactReviewCanonical.Json(rebuilt.Source!.Binding) ||
                ArtifactReviewCanonical.Json(source.Artifacts) != ArtifactReviewCanonical.Json(rebuilt.Source.Artifacts))
                return new(ArtifactReviewIssue.IntegrityMismatch, null);
            if (source.Binding.Scope != scope) return new(ArtifactReviewIssue.WrongScope, null);
            if (source.Binding.RunId != runId) return new(ArtifactReviewIssue.SourceConflict, null);
            foreach (var finding in source.Packages.Guidance.Findings)
                if (ArtifactReviewPolicy.Authorize(authority, scope, finding.CategoryId) is { } categoryDenied) return new(categoryDenied, null);
            var entries = await ReadEntries(connection, transaction, rebuilt.Source, cancellationToken);
            return new(null, new(source.Binding, authority.ActorId, entries.Select(item => item.Entry).ToImmutableArray()));
        }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(ArtifactReviewIssue.IntegrityMismatch, null); }
    }

    public async Task<ArtifactReviewApplyResult> ApplyAsync(Guid runId, string artifactId, ArtifactReviewAuthority authority,
        ArtifactReviewCommand command, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !ArtifactReviewPolicy.ValidDigest(artifactId) || ArtifactReviewPolicy.ValidateCommand(command) is not null)
            return new(ArtifactReviewIssue.InvalidInput, null);
        if (ArtifactReviewPolicy.Authorize(authority, scope) is { } denied) return new(denied, null);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticFixReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var capture = await Capture(runId, authority, cancellationToken);
            if (!capture.Succeeded) return new(capture.Issue, null);
            var source = capture.Source!;
            if (!source.Artifacts.Any(item => item.ArtifactId == artifactId)) return new(ArtifactReviewIssue.NotFound, null);
            // Validate complete visible source and every artifact history before replay lookup.
            var entries = await ReadEntries(connection, transaction, source, cancellationToken);
            var entry = entries.Single(item => item.Entry.Artifact.ArtifactId == artifactId);
            var payloadDigest = ArtifactReviewCanonical.CommandDigest(scope, runId, artifactId, authority.ActorId, command);
            var replay = entry.Events.SingleOrDefault(item => item.Event.EventId == command.EventId);
            if (replay is not null)
                return replay.CommandDigest == payloadDigest ? new(null, replay.Receipt, AlreadyApplied: true) : new(ArtifactReviewIssue.EventConflict, null);
            if (command.ExpectedRevision != entry.Entry.Revision) return new(ArtifactReviewIssue.RevisionConflict, null);
            if (command.ExpectedSourceDigest != source.Binding.SourceDigest) return new(ArtifactReviewIssue.SourceConflict, null);
            if (ArtifactReviewPolicy.Transition(entry.Entry.State, command.Kind) is { } transition) return new(transition, null);
            if (entry.Entry.Revision >= ArtifactReviewPolicy.MaximumRevision) return new(ArtifactReviewIssue.RevisionOverflow, null);
            var revision = checked(entry.Entry.Revision + 1);
            await Register(connection, transaction, source, entry.Entry.Artifact, entry.Events.IsEmpty, cancellationToken);
            await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction);
            var now = (DateTime)(await clock.ExecuteScalarAsync(cancellationToken))!;
            var historyEvent = new ArtifactReviewEvent(command.EventId, revision, command.Kind, authority.ActorId, ["Consultant"], new(now), command.Reason,
                source.Binding, command.Kind == ArtifactReviewKind.ReviewForPlanning ? ArtifactReviewState.ReviewedForPlanning : ArtifactReviewState.Unverified);
            var current = Current(historyEvent);
            var receipt = Receipt(runId, artifactId, historyEvent);
            await Execute(connection, transaction, """
                INSERT INTO synthetic_fix_review.events VALUES (@run,@artifact,@event,@expected,@revision,@source,@command,@json,@digest,@after,@time)
                """, cancellationToken, ("run", runId), ("artifact", artifactId), ("event", command.EventId), ("expected", command.ExpectedRevision),
                ("revision", revision), ("source", source.Binding.SourceDigest), ("command", payloadDigest), ("json", ArtifactReviewCanonical.Json(historyEvent)),
                ("digest", ArtifactReviewCanonical.Digest(historyEvent)), ("after", ArtifactReviewCanonical.Json(current)), ("time", now));
            await Execute(connection, transaction, "INSERT INTO synthetic_fix_review.receipts VALUES (@run,@artifact,@event,@json,@digest)", cancellationToken,
                ("run", runId), ("artifact", artifactId), ("event", command.EventId), ("json", ArtifactReviewCanonical.Json(receipt)), ("digest", ArtifactReviewCanonical.Digest(receipt)));
            await Execute(connection, transaction, "UPDATE synthetic_fix_review.artifact_current SET revision=@revision,current_json=@json WHERE run_id=@run AND artifact_id=@artifact", cancellationToken,
                ("revision", revision), ("json", ArtifactReviewCanonical.Json(current)), ("run", runId), ("artifact", artifactId));
            if (observer is not null) await observer.BeforeCommitAsync("apply", runId, artifactId, command.EventId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, receipt);
        }
        catch (Exception exception) when (IntegrityFailure(exception)) { return new(ArtifactReviewIssue.IntegrityMismatch, null); }
    }
    private async Task<ArtifactReviewSourceResult> Capture(Guid runId, ArtifactReviewAuthority authority, CancellationToken cancellationToken)
    {
        var captured = await readSource(runId, cancellationToken);
        if (captured is null || !captured.Succeeded) return new(captured?.Issue ?? ArtifactReviewIssue.SourceUnavailable, null);
        var source = captured.Source!;
        var rebuilt = ArtifactReviewSourceBuilder.Build(source.Packages);
        if (!rebuilt.Succeeded || ArtifactReviewCanonical.Json(source.Binding) != ArtifactReviewCanonical.Json(rebuilt.Source!.Binding) ||
            ArtifactReviewCanonical.Json(source.Artifacts) != ArtifactReviewCanonical.Json(rebuilt.Source.Artifacts)) return new(ArtifactReviewIssue.IntegrityMismatch, null);
        if (source.Binding.Scope != scope) return new(ArtifactReviewIssue.WrongScope, null);
        if (source.Binding.RunId != runId) return new(ArtifactReviewIssue.SourceConflict, null);
        foreach (var finding in source.Packages.Guidance.Findings)
            if (ArtifactReviewPolicy.Authorize(authority, scope, finding.CategoryId) is { } denied) return new(denied, null);
        return rebuilt;
    }
    private static bool IntegrityFailure(Exception exception) => exception is ArtifactReviewIntegrityException or JsonException or
        InvalidOperationException or ArgumentException or NullReferenceException or OverflowException or KeyNotFoundException;
    private async Task<NpgsqlConnection> Open(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed record StoredCurrent(long Revision, Guid? EventId, ArtifactReviewKind? Kind, string? SourceDigest);
    private sealed record StoredEvent(ArtifactReviewEvent Event, ArtifactReviewReceipt Receipt, string CommandDigest);
    private sealed record LoadedEntry(ArtifactReviewEntry Entry, ImmutableArray<StoredEvent> Events);
    private static StoredCurrent Current(ArtifactReviewEvent item) => new(item.Revision, item.EventId, item.Kind, item.Source.SourceDigest);
    private static ArtifactReviewReceipt Receipt(Guid runId, string artifactId, ArtifactReviewEvent item) =>
        new("synthetic-fix-review-receipt-v1", item.EventId, runId, artifactId, item.Kind, item.Revision, item.ActorId, item.RecordedAtUtc, item.Source.SourceDigest);
    private static Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters) => SyntheticFixReviewMigration.Execute(connection, transaction, sql, cancellationToken, parameters);
    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Guid runId)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("run", runId);
        return command;
    }
    private static bool Dominates(ArtifactReviewSourceBinding newer, ArtifactReviewSourceBinding older) => newer.RunRevision >= older.RunRevision &&
        newer.FindingRevisions.Length == older.FindingRevisions.Length && newer.FindingRevisions.Zip(older.FindingRevisions)
            .All(pair => pair.First.FindingId == pair.Second.FindingId && pair.First.Revision >= pair.Second.Revision);
    private static bool SameVector(ArtifactReviewSourceBinding first, ArtifactReviewSourceBinding second) =>
        first.RunRevision == second.RunRevision && first.FindingRevisions.SequenceEqual(second.FindingRevisions);
    private static void CheckSource(ArtifactReviewSource current, ArtifactReviewSource recorded)
    {
        var first = current.Binding; var second = recorded.Binding;
        if (first.Scope != second.Scope || first.RunId != second.RunId || first.RunInputDigest != second.RunInputDigest || first.BaselineId != second.BaselineId ||
            first.ProfileId != second.ProfileId || first.ApplicationVersion != second.ApplicationVersion || first.ContractDigest != second.ContractDigest ||
            first.TemplateVersion != second.TemplateVersion || first.TemplateDigest != second.TemplateDigest || !current.Artifacts.SequenceEqual(recorded.Artifacts) ||
            !Dominates(first, second) || SameVector(first, second) && first.SourceDigest != second.SourceDigest) throw new ArtifactReviewIntegrityException();
    }
    private static async Task<Dictionary<string, ArtifactReviewSource>> ReadSources(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ArtifactReviewSource current, CancellationToken cancellationToken)
    {
        var sources = new Dictionary<string, ArtifactReviewSource>(StringComparer.Ordinal);
        await using var command = Command(connection, transaction, """
            SELECT source_digest,customer_id,project_id,environment_id,canonical_package,binding_json,artifacts_json,binding_digest
            FROM synthetic_fix_review.source_versions WHERE run_id=@run
            """, current.Binding.RunId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var restored = ArtifactReviewSourceBuilder.Restore(reader.GetString(4));
            if (reader.GetString(0) != restored.Binding.SourceDigest || reader.GetString(1) != current.Binding.Scope.CustomerId ||
                reader.GetString(2) != current.Binding.Scope.ProjectId || reader.GetString(3) != current.Binding.Scope.EnvironmentId ||
                reader.GetString(5) != ArtifactReviewCanonical.Json(restored.Binding) || reader.GetString(6) != ArtifactReviewCanonical.Json(restored.Artifacts) ||
                reader.GetString(7) != ArtifactReviewCanonical.Digest(restored.Binding)) throw new ArtifactReviewIntegrityException();
            CheckSource(current, restored);
            foreach (var prior in sources.Values)
                if (!Dominates(prior.Binding, restored.Binding) && !Dominates(restored.Binding, prior.Binding) ||
                    SameVector(prior.Binding, restored.Binding) && prior.Binding.SourceDigest != restored.Binding.SourceDigest) throw new ArtifactReviewIntegrityException();
            sources.Add(restored.Binding.SourceDigest, restored);
        }
        return sources;
    }
    private static async Task<ImmutableArray<LoadedEntry>> ReadEntries(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ArtifactReviewSource source, CancellationToken cancellationToken)
    {
        var versions = await ReadSources(connection, transaction, source, cancellationToken);
        var seeds = new Dictionary<string, ArtifactReviewArtifact>(StringComparer.Ordinal);
        await using (var command = Command(connection, transaction, "SELECT artifact_id,source_digest,artifact_json,artifact_digest FROM synthetic_fix_review.artifact_seeds WHERE run_id=@run", source.Binding.RunId))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
            {
                var artifact = ArtifactReviewCanonical.Parse<ArtifactReviewArtifact>(reader.GetString(2));
                if (artifact.ArtifactId != reader.GetString(0) || !versions.TryGetValue(reader.GetString(1), out var original) ||
                    !original.Artifacts.Contains(artifact) || reader.GetString(2) != ArtifactReviewCanonical.Json(artifact) ||
                    reader.GetString(3) != ArtifactReviewCanonical.Digest(artifact)) throw new ArtifactReviewIntegrityException();
                seeds.Add(artifact.ArtifactId, artifact);
            }
        if (seeds.Values.Any(seed => !source.Artifacts.Contains(seed))) throw new ArtifactReviewIntegrityException();
        var entries = ImmutableArray.CreateBuilder<LoadedEntry>();
        var usedVersions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in source.Artifacts)
        {
            var events = await ReadHistory(connection, transaction, source.Binding.RunId, artifact, seeds.ContainsKey(artifact.ArtifactId), versions, cancellationToken);
            foreach (var item in events) usedVersions.Add(item.Event.Source.SourceDigest);
            var latest = events.LastOrDefault()?.Event;
            var state = ArtifactReviewPolicy.CurrentState(latest, source.Binding.SourceDigest);
            entries.Add(new(new(artifact, latest?.Revision ?? 0, state, state != ArtifactReviewState.ReviewedForPlanning,
                state == ArtifactReviewState.ReviewedForPlanning, events.Select(item => item.Event).ToImmutableArray()), events));
        }
        if (!usedVersions.SetEquals(versions.Keys)) throw new ArtifactReviewIntegrityException();
        // Detect orphan/extra current, event or receipt rows even if an administrative bypass removed a FK.
        foreach (var (table, expected) in new[] { ("artifact_current", (long)seeds.Count), ("events", entries.Sum(item => (long)item.Events.Length)), ("receipts", entries.Sum(item => (long)item.Events.Length)) })
        {
            await using var count = Command(connection, transaction, "SELECT count(*) FROM synthetic_fix_review." + table + " WHERE run_id=@run", source.Binding.RunId);
            if ((long)(await count.ExecuteScalarAsync(cancellationToken))! != expected) throw new ArtifactReviewIntegrityException();
        }
        return entries.ToImmutable();
    }
    private static async Task<ImmutableArray<StoredEvent>> ReadHistory(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid runId,
        ArtifactReviewArtifact artifact, bool seeded, Dictionary<string, ArtifactReviewSource> versions, CancellationToken cancellationToken)
    {
        var events = ImmutableArray.CreateBuilder<StoredEvent>();
        StoredCurrent stored = new(0, null, null, null);
        await using (var command = Command(connection, transaction, "SELECT revision,current_json FROM synthetic_fix_review.artifact_current WHERE run_id=@run AND artifact_id=@artifact", runId))
        {
            command.Parameters.AddWithValue("artifact", artifact.ArtifactId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var found = await reader.ReadAsync(cancellationToken);
            if (found != seeded) throw new ArtifactReviewIntegrityException();
            if (found)
            {
                stored = ArtifactReviewCanonical.Parse<StoredCurrent>(reader.GetString(1));
                if (stored.Revision != reader.GetInt64(0) || reader.GetString(1) != ArtifactReviewCanonical.Json(stored)) throw new ArtifactReviewIntegrityException();
            }
        }
        await using (var command = Command(connection, transaction, """
            SELECT e.event_id,e.expected_revision,e.result_revision,e.source_digest,e.command_digest,e.event_json,e.event_digest,e.after_json,e.recorded_at,
                r.receipt_json,r.receipt_digest FROM synthetic_fix_review.events e LEFT JOIN synthetic_fix_review.receipts r
                ON r.run_id=e.run_id AND r.artifact_id=e.artifact_id AND r.event_id=e.event_id
                WHERE e.run_id=@run AND e.artifact_id=@artifact ORDER BY e.result_revision
            """, runId))
        {
            command.Parameters.AddWithValue("artifact", artifact.ArtifactId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var ids = new HashSet<Guid>();
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(9) || reader.IsDBNull(10)) throw new ArtifactReviewIntegrityException();
                var item = ArtifactReviewCanonical.Parse<ArtifactReviewEvent>(reader.GetString(5));
                var commandValue = new ArtifactReviewCommand(item.EventId, item.Kind, item.Revision - 1, item.Source.SourceDigest, item.Reason);
                var latest = events.LastOrDefault()?.Event;
                if (item.Revision != events.Count + 1L || item.Revision > ArtifactReviewPolicy.MaximumRevision || item.EventId != reader.GetGuid(0) ||
                    !ids.Add(item.EventId) || reader.GetInt64(1) != item.Revision - 1 || reader.GetInt64(2) != item.Revision ||
                    item.Source.SourceDigest != reader.GetString(3) || !versions.TryGetValue(item.Source.SourceDigest, out var historical) ||
                    ArtifactReviewCanonical.Json(item.Source) != ArtifactReviewCanonical.Json(historical.Binding) || !historical.Artifacts.Contains(artifact) ||
                    latest is not null && !Dominates(item.Source, latest.Source) || ArtifactReviewPolicy.ValidateCommand(commandValue) is not null ||
                    string.IsNullOrWhiteSpace(item.ActorId) || item.ActorRoles.IsDefault || !item.ActorRoles.SequenceEqual(["Consultant"]) ||
                    item.RecordedAtUtc.Offset != TimeSpan.Zero || item.RecordedAtUtc.UtcDateTime != reader.GetDateTime(8) ||
                    item.RecordedState != (item.Kind == ArtifactReviewKind.ReviewForPlanning ? ArtifactReviewState.ReviewedForPlanning : ArtifactReviewState.Unverified) ||
                    ArtifactReviewPolicy.Transition(ArtifactReviewPolicy.CurrentState(latest, item.Source.SourceDigest), item.Kind) is not null ||
                    reader.GetString(4) != ArtifactReviewCanonical.CommandDigest(historical.Binding.Scope, runId, artifact.ArtifactId, item.ActorId, commandValue) ||
                    reader.GetString(5) != ArtifactReviewCanonical.Json(item) || reader.GetString(6) != ArtifactReviewCanonical.Digest(item) ||
                    reader.GetString(7) != ArtifactReviewCanonical.Json(Current(item))) throw new ArtifactReviewIntegrityException();
                var receipt = ArtifactReviewCanonical.Parse<ArtifactReviewReceipt>(reader.GetString(9));
                if (receipt != Receipt(runId, artifact.ArtifactId, item) || reader.GetString(9) != ArtifactReviewCanonical.Json(receipt) ||
                    reader.GetString(10) != ArtifactReviewCanonical.Digest(receipt)) throw new ArtifactReviewIntegrityException();
                events.Add(new(item, receipt, reader.GetString(4)));
            }
        }
        if (seeded && events.Count == 0 || stored != (events.Count == 0 ? new StoredCurrent(0, null, null, null) : Current(events[^1].Event)))
            throw new ArtifactReviewIntegrityException();
        return events.ToImmutable();
    }
    private static async Task Register(NpgsqlConnection connection, NpgsqlTransaction transaction, ArtifactReviewSource source,
        ArtifactReviewArtifact artifact, bool first, CancellationToken cancellationToken)
    {
        var binding = source.Binding;
        await Execute(connection, transaction, """
            INSERT INTO synthetic_fix_review.source_versions VALUES (@run,@source,@customer,@project,@environment,@package,@binding,@artifacts,@digest,clock_timestamp()) ON CONFLICT DO NOTHING
            """, cancellationToken, ("run", binding.RunId), ("source", binding.SourceDigest), ("customer", binding.Scope.CustomerId), ("project", binding.Scope.ProjectId),
            ("environment", binding.Scope.EnvironmentId), ("package", source.CanonicalPackage), ("binding", ArtifactReviewCanonical.Json(binding)),
            ("artifacts", ArtifactReviewCanonical.Json(source.Artifacts)), ("digest", ArtifactReviewCanonical.Digest(binding)));
        if (!first) return;
        await Execute(connection, transaction, "INSERT INTO synthetic_fix_review.artifact_seeds VALUES (@run,@artifact,@source,@json,@digest)", cancellationToken,
            ("run", binding.RunId), ("artifact", artifact.ArtifactId), ("source", binding.SourceDigest), ("json", ArtifactReviewCanonical.Json(artifact)), ("digest", ArtifactReviewCanonical.Digest(artifact)));
        await Execute(connection, transaction, "INSERT INTO synthetic_fix_review.artifact_current VALUES (@run,@artifact,0,@json)", cancellationToken,
            ("run", binding.RunId), ("artifact", artifact.ArtifactId), ("json", ArtifactReviewCanonical.Json(new StoredCurrent(0, null, null, null))));
    }
}
