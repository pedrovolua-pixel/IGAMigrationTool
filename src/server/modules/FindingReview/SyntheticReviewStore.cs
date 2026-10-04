using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Npgsql;

namespace FindingReview;

public sealed class SyntheticReviewIntegrityException(string message) : Exception(message);

/// <summary>Bounded local synthetic history, not production identity, review authority or a customer route.</summary>
public sealed partial class SyntheticReviewStore
{
    private readonly string connectionString;
    private readonly ISyntheticReviewCommitObserver? observer;
    public SyntheticReviewStore(string connectionString, SyntheticReviewScope trustedScope, ISyntheticReviewCommitObserver? observer = null)
    {
        if (trustedScope != SyntheticReviewScope.Fixed) throw new ArgumentException("Only the fixed synthetic review scope is supported.", nameof(trustedScope));
        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database is null ||
            !connection.Database.StartsWith("iga_synthetic_", StringComparison.Ordinal))
            throw new ArgumentException("Review persistence requires a loopback iga_synthetic_ database.", nameof(connectionString));
        connection.CommandTimeout = 15;
        this.connectionString = connection.ConnectionString;
        this.observer = observer;
    }
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await Open(cancellationToken);
        await SyntheticReviewMigration.InitializeAsync(connection, SyntheticReviewScope.Fixed, cancellationToken);
    }

    /// <summary>Trusted host-only original seeding after canonical run/analysis validation. Never expose seed content in an HTTP mutation.</summary>
    public async Task<SyntheticReviewSeedResult> SeedAsync(SyntheticReviewRunSeed? input, SyntheticReviewAuthority authority,
        CancellationToken cancellationToken = default)
    {
        if (input is null) return new(SyntheticReviewIssue.InvalidInput);
        if (input.Scope != SyntheticReviewScope.Fixed) return new(SyntheticReviewIssue.WrongScope);
        var seed = input is null ? null : Normalize(input);
        if (seed is null) return new(SyntheticReviewIssue.InvalidInput);
        var scope = seed.Scope;
        var runId = seed.RunId;
        if (scope != SyntheticReviewScope.Fixed) return new(SyntheticReviewIssue.WrongScope);
        if (authority is null || authority.Roles.IsDefault || !authority.Roles.Contains(SyntheticReviewRole.Consultant)) return new(SyntheticReviewIssue.Denied);
        foreach (var finding in seed.Findings)
        {
            if (SyntheticReviewPolicy.Authorize(authority, scope, finding.CategoryId, SyntheticReviewAction.Read, seed.ResourceState) is { } readIssue) return new(readIssue);
            if (SyntheticReviewPolicy.Authorize(authority, scope, finding.CategoryId, SyntheticReviewAction.Review, seed.ResourceState) is { } reviewIssue) return new(reviewIssue);
        }
        if (Identity(authority, scope, SyntheticReviewAction.Read) is { } readIdentityIssue) return new(readIdentityIssue);
        if (Identity(authority, scope, SyntheticReviewAction.Review) is { } reviewIdentityIssue) return new(reviewIdentityIssue);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(connection, transaction,
            scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue);
        await SyntheticReviewMigration.Execute(connection, transaction, "SELECT pg_advisory_xact_lock(734021006)", cancellationToken);
        SyntheticReviewRunSeed? existing;
        try { existing = await ReadSeed(connection, transaction, scope, runId, cancellationToken); }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch); }
        if (existing is not null) return SyntheticReviewDigest.Compute(existing) == SyntheticReviewDigest.Compute(seed)
            ? new(null, true) : new(SyntheticReviewIssue.SeedConflict);
        await SyntheticReviewMigration.Execute(connection, transaction,
            "INSERT INTO synthetic_review.run_seeds VALUES (@run,@customer,@project,@environment,@json,@digest,clock_timestamp())", cancellationToken,
            ("run", runId), ("customer", scope.CustomerId), ("project", scope.ProjectId), ("environment", scope.EnvironmentId),
            ("json", JsonSerializer.Serialize(seed)), ("digest", SyntheticReviewDigest.Compute(seed)));
        foreach (var finding in seed.Findings)
        {
            await SyntheticReviewMigration.Execute(connection, transaction, "INSERT INTO synthetic_review.finding_seeds VALUES (@run,@finding,@json,@digest)", cancellationToken,
                ("run", runId), ("finding", finding.FindingId), ("json", JsonSerializer.Serialize(finding)), ("digest", SyntheticReviewDigest.Compute(finding)));
            await SyntheticReviewMigration.Execute(connection, transaction, "INSERT INTO synthetic_review.finding_current VALUES (@run,@finding,0,@json)", cancellationToken,
                ("run", runId), ("finding", finding.FindingId), ("json", JsonSerializer.Serialize(Initial(finding))));
        }
        await BeforeCommit("seed", runId, null, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(null);
    }

    public async Task<SyntheticReviewReadResult> ReadAsync(SyntheticReviewScope scope, Guid runId,
        SyntheticReviewAuthority authority, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty) return new(SyntheticReviewIssue.InvalidInput, null);
        if (Identity(authority, scope, SyntheticReviewAction.Read) is { } identityIssue) return new(identityIssue, null);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var seed = await ReadSeed(connection, transaction, scope, runId, cancellationToken);
            if (seed is null) return new(SyntheticReviewIssue.NotFound, null);
            var findings = ImmutableArray.CreateBuilder<SyntheticReviewedFinding>();
            foreach (var original in seed.Findings)
            {
                if (SyntheticReviewPolicy.Authorize(authority, scope, original.CategoryId, SyntheticReviewAction.Read, seed.ResourceState) is { } denied) return new(denied, null);
                findings.Add(await ReadFinding(connection, transaction, seed, original.FindingId, false, cancellationToken));
            }
            var snapshot = new SyntheticReviewSnapshot(seed, findings.ToImmutable(), "");
            await transaction.CommitAsync(cancellationToken);
            return new(null, snapshot with { SnapshotDigest = SyntheticReviewDigest.Compute(snapshot) });
        }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
    }

    /// <summary>Same-transaction trusted read; no seed, callback, new connection or commit.</summary>
    public async Task<SyntheticReviewReadResult> ReadInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SyntheticReviewScope scope, Guid runId, SyntheticReviewAuthority authority, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty) return new(SyntheticReviewIssue.InvalidInput, null);
        if (Identity(authority, scope, SyntheticReviewAction.Read) is { } denied) return new(denied, null);
        var expected = new NpgsqlConnectionStringBuilder(connectionString);
        var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (transaction.Connection != connection || connection.State != ConnectionState.Open || actual.Host != expected.Host ||
            actual.Port != expected.Port || actual.Database != expected.Database || actual.Username != expected.Username)
            return new(SyntheticReviewIssue.InvalidInput, null);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(connection, transaction,
            scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var seed = await ReadSeed(connection, transaction, scope, runId, cancellationToken);
            if (seed is null) return new(SyntheticReviewIssue.NotFound, null);
            var findings = ImmutableArray.CreateBuilder<SyntheticReviewedFinding>();
            foreach (var original in seed.Findings)
            {
                if (SyntheticReviewPolicy.Authorize(authority, scope, original.CategoryId, SyntheticReviewAction.Read, seed.ResourceState) is { } categoryDenied)
                    return new(categoryDenied, null);
                findings.Add(await ReadFinding(connection, transaction, seed, original.FindingId, false, cancellationToken));
            }
            var snapshot = new SyntheticReviewSnapshot(seed, findings.ToImmutable(), "");
            return new(null, snapshot with { SnapshotDigest = SyntheticReviewDigest.Compute(snapshot) });
        }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
    }

    public async Task<SyntheticReviewApplyResult> ApplyAsync(SyntheticReviewScope scope, Guid runId, string findingId,
        SyntheticReviewAuthority authority, SyntheticReviewCommand command, CancellationToken cancellationToken = default)
    {
        if (runId == Guid.Empty || !SyntheticReviewPolicy.ValidDigest(findingId) || SyntheticReviewPolicy.ValidateCommand(command) is not null)
            return new(SyntheticReviewIssue.InvalidInput, null);
        var action = SyntheticReviewPolicy.ActionFor(command.Kind);
        if (Identity(authority, scope, action) is { } identityIssue) return new(identityIssue, null);
        await using var connection = await Open(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(connection, transaction,
            scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var seed = await ReadSeed(connection, transaction, scope, runId, cancellationToken);
            if (seed is null || !seed.Findings.Any(finding => finding.FindingId == findingId)) return new(SyntheticReviewIssue.NotFound, null);
            var original = seed.Findings.Single(finding => finding.FindingId == findingId);
            if (SyntheticReviewPolicy.Authorize(authority, scope, original.CategoryId, action, seed.ResourceState) is { } denied) return new(denied, null);
            var finding = await ReadFinding(connection, transaction, seed, findingId, true, cancellationToken);
            var payloadDigest = PayloadDigest(scope, runId, findingId, authority.ActorId, command);
            var replay = finding.History.SingleOrDefault(item => item.EventId == command.EventId);
            if (replay is not null)
                return PayloadDigest(scope, runId, findingId, replay.ActorId, replay.Command) == payloadDigest
                    ? new(null, replay.Outcome, true) : new(SyntheticReviewIssue.EventConflict, null);
            if (finding.Current.Revision != command.ExpectedRevision) return new(SyntheticReviewIssue.RevisionConflict, finding.Current);
            if (SyntheticReviewPolicy.ValidateTransition(finding.Current, command) is { } transitionIssue) return new(transitionIssue, finding.Current);
            var next = SyntheticReviewPolicy.Next(finding.Current, command);
            await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction);
            var now = (DateTime)(await clock.ExecuteScalarAsync(cancellationToken))!;
            var historyEvent = new SyntheticReviewEvent(command.EventId, authority.ActorId,
                authority.Roles.Distinct().Order().ToImmutableArray(), command, new DateTimeOffset(now), next);
            var afterJson = JsonSerializer.Serialize(next);
            await SyntheticReviewMigration.Execute(connection, transaction, "INSERT INTO synthetic_review.events VALUES (@run,@finding,@event,@expected,@revision,@payload,@json,@digest,@after,@time)", cancellationToken,
                ("run", runId), ("finding", findingId), ("event", command.EventId), ("expected", command.ExpectedRevision), ("revision", next.Revision),
                ("payload", payloadDigest), ("json", JsonSerializer.Serialize(historyEvent)), ("digest", SyntheticReviewDigest.Compute(historyEvent)), ("after", afterJson), ("time", now));
            await SyntheticReviewMigration.Execute(connection, transaction, "UPDATE synthetic_review.finding_current SET revision=@revision,current_json=@json WHERE run_id=@run AND finding_id=@finding", cancellationToken,
                ("revision", next.Revision), ("json", afterJson), ("run", runId), ("finding", findingId));
            await BeforeCommit("apply", runId, command.EventId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, next);
        }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
    }

    /// <summary>Owning write on a supplied transaction; returned result is pending caller commit. Never opens or commits a nested transaction.</summary>
    public async Task<SyntheticReviewSeedResult> SeedInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, SyntheticReviewRunSeed? input, SyntheticReviewAuthority authority,
        CancellationToken cancellationToken = default)
    {
        if (!ValidSharedConnection(connection, transaction)) return new(SyntheticReviewIssue.InvalidInput);
        if (input is null) return new(SyntheticReviewIssue.InvalidInput);
        if (input.Scope != SyntheticReviewScope.Fixed) return new(SyntheticReviewIssue.WrongScope);
        var seed = input is null ? null : Normalize(input);
        if (seed is null) return new(SyntheticReviewIssue.InvalidInput);
        var scope = seed.Scope;
        var runId = seed.RunId;
        if (scope != SyntheticReviewScope.Fixed) return new(SyntheticReviewIssue.WrongScope);
        if (authority is null || authority.Roles.IsDefault || !authority.Roles.Contains(SyntheticReviewRole.Consultant)) return new(SyntheticReviewIssue.Denied);
        foreach (var finding in seed.Findings)
        {
            if (SyntheticReviewPolicy.Authorize(authority, scope, finding.CategoryId, SyntheticReviewAction.Read, seed.ResourceState) is { } readIssue) return new(readIssue);
            if (SyntheticReviewPolicy.Authorize(authority, scope, finding.CategoryId, SyntheticReviewAction.Review, seed.ResourceState) is { } reviewIssue) return new(reviewIssue);
        }
        if (Identity(authority, scope, SyntheticReviewAction.Read) is { } readIdentityIssue) return new(readIdentityIssue);
        if (Identity(authority, scope, SyntheticReviewAction.Review) is { } reviewIdentityIssue) return new(reviewIdentityIssue);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(connection, transaction,
            scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue);
        await SyntheticReviewMigration.Execute(connection, transaction, "SELECT pg_advisory_xact_lock(734021006)", cancellationToken);
        SyntheticReviewRunSeed? existing;
        try { existing = await ReadSeed(connection, transaction, scope, runId, cancellationToken); }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch); }
        if (existing is not null) return SyntheticReviewDigest.Compute(existing) == SyntheticReviewDigest.Compute(seed)
            ? new(null, true) : new(SyntheticReviewIssue.SeedConflict);
        await SyntheticReviewMigration.Execute(connection, transaction,
            "INSERT INTO synthetic_review.run_seeds VALUES (@run,@customer,@project,@environment,@json,@digest,clock_timestamp())", cancellationToken,
            ("run", runId), ("customer", scope.CustomerId), ("project", scope.ProjectId), ("environment", scope.EnvironmentId),
            ("json", JsonSerializer.Serialize(seed)), ("digest", SyntheticReviewDigest.Compute(seed)));
        foreach (var finding in seed.Findings)
        {
            await SyntheticReviewMigration.Execute(connection, transaction, "INSERT INTO synthetic_review.finding_seeds VALUES (@run,@finding,@json,@digest)", cancellationToken,
                ("run", runId), ("finding", finding.FindingId), ("json", JsonSerializer.Serialize(finding)), ("digest", SyntheticReviewDigest.Compute(finding)));
            await SyntheticReviewMigration.Execute(connection, transaction, "INSERT INTO synthetic_review.finding_current VALUES (@run,@finding,0,@json)", cancellationToken,
                ("run", runId), ("finding", finding.FindingId), ("json", JsonSerializer.Serialize(Initial(finding))));
        }
        await BeforeCommit("seed", runId, null, cancellationToken);
        return new(null);
    }

    /// <summary>Owning write on a supplied transaction; returned result is pending caller commit. Never opens or commits a nested transaction.</summary>
    public async Task<SyntheticReviewApplyResult> ApplyInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, SyntheticReviewScope scope, Guid runId, string findingId,
        SyntheticReviewAuthority authority, SyntheticReviewCommand command, CancellationToken cancellationToken = default)
    {
        if (!ValidSharedConnection(connection, transaction)) return new(SyntheticReviewIssue.InvalidInput, null);
        if (runId == Guid.Empty || !SyntheticReviewPolicy.ValidDigest(findingId) || SyntheticReviewPolicy.ValidateCommand(command) is not null)
            return new(SyntheticReviewIssue.InvalidInput, null);
        var action = SyntheticReviewPolicy.ActionFor(command.Kind);
        if (Identity(authority, scope, action) is { } identityIssue) return new(identityIssue, null);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(connection, transaction,
            scope.CustomerId, scope.ProjectId, scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var seed = await ReadSeed(connection, transaction, scope, runId, cancellationToken);
            if (seed is null || !seed.Findings.Any(finding => finding.FindingId == findingId)) return new(SyntheticReviewIssue.NotFound, null);
            var original = seed.Findings.Single(finding => finding.FindingId == findingId);
            if (SyntheticReviewPolicy.Authorize(authority, scope, original.CategoryId, action, seed.ResourceState) is { } denied) return new(denied, null);
            var finding = await ReadFinding(connection, transaction, seed, findingId, true, cancellationToken);
            var payloadDigest = PayloadDigest(scope, runId, findingId, authority.ActorId, command);
            var replay = finding.History.SingleOrDefault(item => item.EventId == command.EventId);
            if (replay is not null)
                return PayloadDigest(scope, runId, findingId, replay.ActorId, replay.Command) == payloadDigest
                    ? new(null, replay.Outcome, true) : new(SyntheticReviewIssue.EventConflict, null);
            if (finding.Current.Revision != command.ExpectedRevision) return new(SyntheticReviewIssue.RevisionConflict, finding.Current);
            if (SyntheticReviewPolicy.ValidateTransition(finding.Current, command) is { } transitionIssue) return new(transitionIssue, finding.Current);
            var next = SyntheticReviewPolicy.Next(finding.Current, command);
            await using var clock = new NpgsqlCommand("SELECT clock_timestamp()", connection, transaction);
            var now = (DateTime)(await clock.ExecuteScalarAsync(cancellationToken))!;
            var historyEvent = new SyntheticReviewEvent(command.EventId, authority.ActorId,
                authority.Roles.Distinct().Order().ToImmutableArray(), command, new DateTimeOffset(now), next);
            var afterJson = JsonSerializer.Serialize(next);
            await SyntheticReviewMigration.Execute(connection, transaction, "INSERT INTO synthetic_review.events VALUES (@run,@finding,@event,@expected,@revision,@payload,@json,@digest,@after,@time)", cancellationToken,
                ("run", runId), ("finding", findingId), ("event", command.EventId), ("expected", command.ExpectedRevision), ("revision", next.Revision),
                ("payload", payloadDigest), ("json", JsonSerializer.Serialize(historyEvent)), ("digest", SyntheticReviewDigest.Compute(historyEvent)), ("after", afterJson), ("time", now));
            await SyntheticReviewMigration.Execute(connection, transaction, "UPDATE synthetic_review.finding_current SET revision=@revision,current_json=@json WHERE run_id=@run AND finding_id=@finding", cancellationToken,
                ("revision", next.Revision), ("json", afterJson), ("run", runId), ("finding", findingId));
            await BeforeCommit("apply", runId, command.EventId, cancellationToken);
            return new(null, next);
        }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
    }

    private bool ValidSharedConnection(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        if (connection is null || transaction is null || transaction.Connection != connection || connection.State != ConnectionState.Open) return false;
        var expected = new NpgsqlConnectionStringBuilder(connectionString);
        var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        return actual.Host == expected.Host && actual.Port == expected.Port && actual.Database == expected.Database && actual.Username == expected.Username;
    }

    private async Task<NpgsqlConnection> Open(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    private Task BeforeCommit(string operation, Guid runId, Guid? eventId, CancellationToken cancellationToken) =>
        observer?.BeforeCommitAsync(operation, runId, eventId, cancellationToken) ?? Task.CompletedTask;
    private static SyntheticReviewIssue? Identity(SyntheticReviewAuthority? authority, SyntheticReviewScope scope, SyntheticReviewAction action)
    {
        if (scope != SyntheticReviewScope.Fixed) return SyntheticReviewIssue.WrongScope;
        if (authority is null || authority.Categories.IsDefaultOrEmpty) return SyntheticReviewIssue.Denied;
        return SyntheticReviewPolicy.Authorize(authority, scope, authority.Categories[0], action, SyntheticReviewResourceState.Mutable);
    }
    private static SyntheticFindingCurrent Initial(SyntheticFindingSeed seed) => new(seed.FindingId, seed.CategoryId, 0, seed.InitialState, seed.OriginalTitle, "");
    private static string PayloadDigest(SyntheticReviewScope scope, Guid runId, string findingId, string actorId, SyntheticReviewCommand command) =>
        SyntheticReviewDigest.Compute(new { SchemaVersion = "synthetic-review-event-v1", scope, runId, findingId, actorId, command });
    private static SyntheticReviewRunSeed? Normalize(SyntheticReviewRunSeed seed)
    {
        if (seed.RunId == Guid.Empty || !SyntheticReviewPolicy.ValidDigest(seed.RunInputDigest) || !SyntheticReviewPolicy.ValidDigest(seed.AnalysisDigest) ||
            seed.Findings.IsDefault || seed.ResourceState != SyntheticReviewResourceState.Mutable) return null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var occurrences = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in seed.Findings)
        {
            if (finding is null || !SyntheticReviewPolicy.ValidDigest(finding.FindingId) || !ids.Add(finding.FindingId) ||
                !SyntheticReviewPolicy.ValidText(finding.CategoryId, 250) || !SyntheticReviewPolicy.ValidText(finding.OriginalTitle, 250) ||
                finding.InitialState is not (SyntheticFindingState.Proposed or SyntheticFindingState.AutoConfirmed) ||
                finding.OriginalDigests.IsDefaultOrEmpty || finding.OriginalDigests.Any(digest => !SyntheticReviewPolicy.ValidDigest(digest)) ||
                finding.Occurrences.IsDefaultOrEmpty || finding.Occurrences.Any(item => item is null || !SyntheticReviewPolicy.ValidDigest(item.OccurrenceId) ||
                    !occurrences.Add(item.OccurrenceId) || !SyntheticReviewPolicy.ValidText(item.ObjectId, 250) ||
                    !SyntheticReviewPolicy.ValidText(item.RuleId, 250) || !SyntheticReviewPolicy.ValidText(item.RuleVersion, 250) || !SyntheticReviewPolicy.ValidDigest(item.OriginalDigest)) ||
                !finding.OriginalDigests.ToHashSet(StringComparer.Ordinal).SetEquals(finding.Occurrences.Select(item => item.OriginalDigest))) return null;
        }
        return seed with
        {
            Findings = seed.Findings.OrderBy(finding => finding.FindingId, StringComparer.Ordinal).Select(finding => finding with
            {
                OriginalDigests = finding.OriginalDigests.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
                Occurrences = finding.Occurrences.OrderBy(item => item.OccurrenceId, StringComparer.Ordinal).ToImmutableArray()
            }).ToImmutableArray()
        };
    }
    private static async Task<SyntheticReviewRunSeed?> ReadSeed(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SyntheticReviewScope scope, Guid runId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT seed_json,seed_digest FROM synthetic_review.run_seeds WHERE run_id=@run AND customer_id=@customer AND project_id=@project AND environment_id=@environment", connection, transaction);
        command.Parameters.AddWithValue("run", runId);
        command.Parameters.AddWithValue("customer", scope.CustomerId);
        command.Parameters.AddWithValue("project", scope.ProjectId);
        command.Parameters.AddWithValue("environment", scope.EnvironmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var seed = JsonSerializer.Deserialize<SyntheticReviewRunSeed>(reader.GetString(0));
        if (seed is null || seed.Scope != scope || seed.RunId != runId || Normalize(seed) is null || SyntheticReviewDigest.Compute(seed) != reader.GetString(1))
            throw new SyntheticReviewIntegrityException("Stored review run seed differs.");
        return seed;
    }
    private static async Task<SyntheticReviewedFinding> ReadFinding(NpgsqlConnection connection, NpgsqlTransaction transaction,
        SyntheticReviewRunSeed runSeed, string findingId, bool forUpdate, CancellationToken cancellationToken)
    {
        var expectedSeed = runSeed.Findings.Single(item => item.FindingId == findingId);
        var history = ImmutableArray.CreateBuilder<SyntheticReviewEvent>();
        SyntheticFindingCurrent stored;
        await using (var command = new NpgsqlCommand("SELECT current_json,revision FROM synthetic_review.finding_current WHERE run_id=@run AND finding_id=@finding" + (forUpdate ? " FOR UPDATE" : ""), connection, transaction))
        {
            command.Parameters.AddWithValue("run", runSeed.RunId);
            command.Parameters.AddWithValue("finding", findingId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new SyntheticReviewIntegrityException("Stored current missing.");
            stored = JsonSerializer.Deserialize<SyntheticFindingCurrent>(reader.GetString(0)) ?? throw new SyntheticReviewIntegrityException("Stored current invalid.");
            if (stored.Revision != reader.GetInt64(1)) throw new SyntheticReviewIntegrityException("Stored revision differs.");
        }
        await using (var command = new NpgsqlCommand("SELECT seed_json,seed_digest FROM synthetic_review.finding_seeds WHERE run_id=@run AND finding_id=@finding", connection, transaction))
        {
            command.Parameters.AddWithValue("run", runSeed.RunId);
            command.Parameters.AddWithValue("finding", findingId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(1) != SyntheticReviewDigest.Compute(expectedSeed) ||
                SyntheticReviewDigest.Compute(JsonSerializer.Deserialize<SyntheticFindingSeed>(reader.GetString(0))) != SyntheticReviewDigest.Compute(expectedSeed))
                throw new SyntheticReviewIntegrityException("Stored finding original differs.");
        }
        var current = Initial(expectedSeed);
        await using (var command = new NpgsqlCommand("SELECT event_json,event_digest,payload_digest,expected_revision,result_revision,after_json,recorded_at,event_id FROM synthetic_review.events WHERE run_id=@run AND finding_id=@finding ORDER BY result_revision", connection, transaction))
        {
            command.Parameters.AddWithValue("run", runSeed.RunId);
            command.Parameters.AddWithValue("finding", findingId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var item = JsonSerializer.Deserialize<SyntheticReviewEvent>(reader.GetString(0)) ?? throw new SyntheticReviewIntegrityException("Stored event invalid.");
                if (SyntheticReviewPolicy.ValidateCommand(item.Command) is not null || item.Command.ExpectedRevision != current.Revision ||
                    SyntheticReviewPolicy.ValidateTransition(current, item.Command) is not null || item.EventId != item.Command.EventId || item.EventId != reader.GetGuid(7) ||
                    SyntheticReviewDigest.Compute(item) != reader.GetString(1) ||
                    PayloadDigest(runSeed.Scope, runSeed.RunId, findingId, item.ActorId, item.Command) != reader.GetString(2) ||
                    item.Command.ExpectedRevision != reader.GetInt64(3) || item.Outcome.Revision != reader.GetInt64(4) ||
                    JsonSerializer.Serialize(item.Outcome) != reader.GetString(5) || item.RecordedAtUtc != new DateTimeOffset(reader.GetDateTime(6)) ||
                    SyntheticReviewPolicy.Next(current, item.Command) != item.Outcome)
                    throw new SyntheticReviewIntegrityException("Stored review history differs.");
                current = item.Outcome;
                history.Add(item);
            }
        }
        if (stored != current) throw new SyntheticReviewIntegrityException("Stored current differs from immutable history.");
        return new(expectedSeed, current, history.ToImmutable());
    }
}
