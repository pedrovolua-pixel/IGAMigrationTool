using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using AssessmentCoverage;
using AssessmentScoring;
using Npgsql;
using SyntheticSourceFence;

namespace SyntheticOutcomePriority;

/// <summary>Runtime operations join caller transactions. A successful receipt becomes durable only when that caller commits.</summary>
public sealed class SyntheticOutcomePriorityStore(OutcomeScope scope, OutcomePrioritySourceReader? sourceReader = null,
    IOutcomePriorityWriteObserver? observer = null)
{
    public Task InitializeAsync(NpgsqlConnection connection, CancellationToken cancellationToken = default)
    {
        Guard(connection);
        if (scope != OutcomeScope.Fixed) throw new ArgumentException("Wrong synthetic outcome scope.");
        return OutcomePriorityMigration.InitializeAsync(connection, scope, cancellationToken);
    }
    public static Task AcquireRegistryFenceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, OutcomeScope scope, CancellationToken cancellationToken = default) =>
        SyntheticRunSourceFence.AcquireAsync(connection, transaction, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, OutcomePriorityContract.RegistryFenceId, cancellationToken);
    private Task RegistryFence(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct) => AcquireRegistryFenceAsync(c, t, scope, ct);
    private Task RunFence(NpgsqlConnection c, NpgsqlTransaction t, Guid run, CancellationToken ct) => SyntheticRunSourceFence.AcquireAsync(c, t, scope.CustomerId, scope.ProjectId, scope.EnvironmentId, run, ct);
    private static void Guard(NpgsqlConnection c)
    {
        var b = new NpgsqlConnectionStringBuilder(c.ConnectionString);
        if (b.Host is not ("localhost" or "127.0.0.1" or "::1") || b.Database is null || !b.Database.StartsWith("iga_synthetic_phase1b_", StringComparison.Ordinal)) throw new ArgumentException("Dedicated loopback synthetic outcome database required.");
    }
    private static void Transaction(NpgsqlConnection c, NpgsqlTransaction t)
    { Guard(c); if (t.Connection != c || c.State != System.Data.ConnectionState.Open) throw new ArgumentException("Outcome transaction binding invalid."); }
    private async Task<OutcomePriorityIssue?> Prepare(NpgsqlConnection c, NpgsqlTransaction t, OutcomeAuthority a, OutcomeAction action, CancellationToken ct)
    {
        Transaction(c, t);
        if (OutcomePriorityPolicy.Authorize(a, scope, action) is { } denied) return denied;
        return await OutcomePriorityMigration.VerifyAsync(c, t, scope, ct);
    }
    public async Task<OutcomeRegistryResult> ReadRegistryAsync(NpgsqlConnection c, NpgsqlTransaction t, OutcomeAuthority a, CancellationToken ct = default)
    {
        if (await Prepare(c, t, a, OutcomeAction.ReadOutcome, ct) is { } denied) return new(denied, null);
        await RegistryFence(c, t, ct);
        try { return new(null, await LoadRegistry(c, t, a, ct)); }
        catch (OutcomeAccessException) { return new(OutcomePriorityIssue.Denied, null); }
        catch (Exception e) when (Integrity(e)) { return new(OutcomePriorityIssue.IntegrityMismatch, null); }
    }
    public async Task<OutcomeApplyResult> ApplyOutcomeAsync(NpgsqlConnection c, NpgsqlTransaction t, OutcomeAuthority a, OutcomeCommand command, CancellationToken ct = default)
    {
        if (!OutcomePriorityPolicy.Command(command)) return new(OutcomePriorityIssue.InvalidInput, null);
        var baseAction = command.Kind == OutcomeKind.Approve ? OutcomeAction.ApproveOutcome : command.Kind == OutcomeKind.Retire ? OutcomeAction.ReadOutcome : OutcomeAction.ManageOutcome;
        if (await Prepare(c, t, a, baseAction, ct) is { } denied) return new(denied, null);
        await RegistryFence(c, t, ct);
        try
        {
            var registry = await LoadRegistry(c, t, a, ct);
            var entry = registry.Entries.SingleOrDefault(e => e.Content.OutcomeId == command.OutcomeId && e.Content.Version == command.Version);
            var category = entry?.Content.CategoryId ?? command.Content?.CategoryId;
            if (category is null) return new(OutcomePriorityIssue.NotFound, null);
            var action = command.Kind == OutcomeKind.Retire && entry?.State == OutcomeState.CustomerApproved ? OutcomeAction.ApproveOutcome : baseAction == OutcomeAction.ReadOutcome ? OutcomeAction.ManageOutcome : baseAction;
            if (OutcomePriorityPolicy.Authorize(a, scope, action, category) is { } invalid) return new(invalid, null);
            var digest = OutcomePriorityCanonical.Digest(new { schemaVersion = "synthetic-outcome-command-v1", scope, a.ActorId, command });
            var replay = await Receipt<OutcomeReceipt>(c, t, command.EventId, a.ActorId, digest, "outcome", ct);
            if (replay.Issue is not null || replay.Receipt is not null) return new(replay.Issue, replay.Receipt, replay.Receipt is not null);
            if (registry.Revision != command.ExpectedRegistryRevision) return new(OutcomePriorityIssue.RevisionConflict, null);
            if (registry.Revision == OutcomePriorityContract.MaximumRevision) return new(OutcomePriorityIssue.RevisionOverflow, null);
            if (command.Kind == OutcomeKind.CreateDraft)
            {
                if (entry is not null) return new(OutcomePriorityIssue.InvalidState, null);
                var siblings = registry.Entries.Where(e => e.Content.OutcomeId == command.OutcomeId).ToArray();
                if (siblings.Length == 0 && registry.Entries.Select(e => e.Content.OutcomeId).Distinct(StringComparer.Ordinal).Count() >= 64) return new(OutcomePriorityIssue.InvalidInput, null);
                if (command.Version != (siblings.Length == 0 ? 1 : siblings.Max(e => e.Content.Version) + 1) || command.Content!.PredecessorVersion is { } predecessor && !siblings.Any(e => e.Content.Version == predecessor)) return new(OutcomePriorityIssue.InvalidInput, null);
            }
            else
            {
                if (entry is null) return new(OutcomePriorityIssue.NotFound, null);
                if (entry.Revision != command.ExpectedRevision) return new(OutcomePriorityIssue.RevisionConflict, null);
                if (entry.Content.ContentDigest != command.ExpectedContentDigest) return new(OutcomePriorityIssue.SourceConflict, null);
                if (entry.Revision == OutcomePriorityContract.MaximumRevision) return new(OutcomePriorityIssue.RevisionOverflow, null);
            }
            var next = command.Kind switch
            {
                OutcomeKind.CreateDraft => OutcomeState.Draft,
                OutcomeKind.Review when entry!.State == OutcomeState.Draft && command.ExpectedReviewEventId is null => OutcomeState.ConsultantReviewed,
                OutcomeKind.Approve when entry!.State == OutcomeState.ConsultantReviewed && command.ExpectedReviewEventId == entry.History[^1].EventId => OutcomeState.CustomerApproved,
                OutcomeKind.Retire when entry!.State is OutcomeState.Draft or OutcomeState.ConsultantReviewed or OutcomeState.CustomerApproved && command.ExpectedReviewEventId is null => OutcomeState.Retired,
                _ => (OutcomeState?)null
            };
            if (next is null) return new(OutcomePriorityIssue.InvalidState, null);
            var high = registry.HighWater.SingleOrDefault(h => h.OutcomeId == command.OutcomeId)?.HighestApprovedVersion ?? 0;
            if (next == OutcomeState.CustomerApproved && command.Version <= high) return new(OutcomePriorityIssue.InvalidState, null);
            var prior = registry.Entries.SingleOrDefault(e => e.Content.OutcomeId == command.OutcomeId && e.State == OutcomeState.CustomerApproved);
            if (prior?.Revision == OutcomePriorityContract.MaximumRevision) return new(OutcomePriorityIssue.RevisionOverflow, null);
            var now = await Now(c, t, ct);
            var ev = new OutcomeEvent(command.EventId, command.OutcomeId, command.Version, (entry?.Revision ?? 0) + 1, command.Kind, next.Value, a.ActorId, a.Roles[0], now, command.Reason, command.ExpectedContentDigest, command.Kind == OutcomeKind.Approve ? command.ExpectedReviewEventId : null, null);
            if (observer is not null) await observer.BeforeWriteAsync("outcome", command.EventId, ct);
            if (command.Kind == OutcomeKind.CreateDraft) await Sql(c, t, "INSERT INTO synthetic_outcome_priority.outcome_versions VALUES(@id,@version,@json,@digest)", ct, ("id", command.OutcomeId), ("version", command.Version), ("json", OutcomePriorityCanonical.Json(command.Content)), ("digest", command.ExpectedContentDigest));
            await AddOutcomeEvent(c, t, ev, ct);
            if (next == OutcomeState.CustomerApproved)
            {
                if (prior is not null)
                    await AddOutcomeEvent(c, t, new(command.EventId, prior.Content.OutcomeId, prior.Content.Version, prior.Revision + 1, OutcomeKind.Approve, OutcomeState.Superseded, a.ActorId, a.Roles[0], now, command.Reason, prior.Content.ContentDigest, null, command.Version), ct);
                await Sql(c, t, "INSERT INTO synthetic_outcome_priority.outcome_highwater VALUES(@id,@version) ON CONFLICT(outcome_id) DO UPDATE SET version=excluded.version", ct, ("id", command.OutcomeId), ("version", command.Version));
            }
            await Sql(c, t, "UPDATE synthetic_outcome_priority.registry_current SET revision=revision+1 WHERE singleton=true", ct);
            var receipt = new OutcomeReceipt("synthetic-outcome-receipt-v1", command.EventId, a.ActorId, command.OutcomeId, command.Version, ev.Revision, now, ev.ContentDigest);
            await SaveReceipt(c, t, command.EventId, a.ActorId, digest, "outcome", receipt, ct);
            return new(null, receipt);
        }
        catch (OutcomeAccessException) { return new(OutcomePriorityIssue.Denied, null); }
        catch (Exception e) when (Integrity(e)) { return new(OutcomePriorityIssue.IntegrityMismatch, null); }
    }
    public async Task<OutcomeLockResult> LockOutcomesAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, OutcomeAuthority a, ImmutableArray<OutcomeSelection> selections, CancellationToken ct = default)
    {
        if (runId == Guid.Empty || runId == OutcomePriorityContract.RegistryFenceId || selections.IsDefault || selections.Length > 64 || selections.Any(s => s is null || !OutcomePriorityPolicy.Id(s.OutcomeId) || s.Version <= 0 || s.Revision <= 0 || s.Version > OutcomePriorityContract.MaximumRevision || s.Revision > OutcomePriorityContract.MaximumRevision || !OutcomePriorityPolicy.ValidDigest(s.ContentDigest) || s.ApprovalEventId == Guid.Empty) || selections.Select(s => s.OutcomeId).Distinct(StringComparer.Ordinal).Count() != selections.Length) return new(OutcomePriorityIssue.InvalidInput, null);
        if (await Prepare(c, t, a, OutcomeAction.ManageOutcome, ct) is { } denied) return new(denied, null);
        await RegistryFence(c, t, ct); await RunFence(c, t, runId, ct);
        try
        {
            var existing = await LoadLock(c, t, runId, a, ct);
            if (existing is not null)
            {
                if (!SameSelections(existing, selections)) return new(OutcomePriorityIssue.SourceConflict, null);
                return new(null, existing);
            }
            var registry = await LoadRegistry(c, t, a, ct);
            var locked = ImmutableArray.CreateBuilder<LockedOutcome>();
            foreach (var selection in selections.OrderBy(s => s.OutcomeId, StringComparer.Ordinal))
            {
                var e = registry.Entries.SingleOrDefault(e => e.Content.OutcomeId == selection.OutcomeId && e.Content.Version == selection.Version);
                if (e is null) return new(OutcomePriorityIssue.NotFound, null);
                if (e.Content.ContentDigest != selection.ContentDigest || e.Revision != selection.Revision || e.History[^1].EventId != selection.ApprovalEventId) return new(OutcomePriorityIssue.SourceConflict, null);
                if (e.State != OutcomeState.CustomerApproved) return new(OutcomePriorityIssue.InvalidState, null);
                locked.Add(new(e.Content, e.History[^1], e.History));
            }
            var initial = new LockedOutcomeSet("synthetic-outcome-lock-v1", scope, runId, OutcomePriorityContract.ContractDigest, locked.ToImmutable(), "");
            var result = initial with { ContentDigest = OutcomePriorityCanonical.Digest(initial) };
            if (observer is not null) await observer.BeforeWriteAsync("lock", Guid.Empty, ct);
            await Sql(c, t, "INSERT INTO synthetic_outcome_priority.run_locks VALUES(@run,@json,@digest)", ct, ("run", runId), ("json", OutcomePriorityCanonical.Json(result)), ("digest", result.ContentDigest));
            return new(null, result);
        }
        catch (OutcomeAccessException) { return new(OutcomePriorityIssue.Denied, null); }
        catch (Exception e) when (Integrity(e)) { return new(OutcomePriorityIssue.IntegrityMismatch, null); }
    }
    public async Task<OutcomeLockResult> ReadLockedAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, OutcomeAuthority a, CancellationToken ct = default)
    {
        if (runId == Guid.Empty) return new(OutcomePriorityIssue.InvalidInput, null);
        if (await Prepare(c, t, a, OutcomeAction.ReadOutcome, ct) is { } denied) return new(denied, null);
        await RegistryFence(c, t, ct); await RunFence(c, t, runId, ct);
        try { var result = await LoadLock(c, t, runId, a, ct); return new(result is null ? OutcomePriorityIssue.NotFound : null, result); }
        catch (OutcomeAccessException) { return new(OutcomePriorityIssue.Denied, null); }
        catch (Exception e) when (Integrity(e)) { return new(OutcomePriorityIssue.IntegrityMismatch, null); }
    }
    public async Task<PlanningReadResult> ReadPlanningAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, OutcomeAuthority a, CancellationToken ct = default)
    {
        if (runId == Guid.Empty) return new(OutcomePriorityIssue.InvalidInput, null);
        if (await Prepare(c, t, a, OutcomeAction.ReadPlanning, ct) is { } denied) return new(denied, null);
        await RunFence(c, t, runId, ct);
        try
        {
            var source = await Capture(c, t, runId, a, ct);
            if (source.Issue is not null || source.Source is null) return new(source.Issue ?? OutcomePriorityIssue.SourceUnavailable, null);
            return new(null, await LoadPlanning(c, t, source.Source, a, ct));
        }
        catch (OutcomeAccessException) { return new(OutcomePriorityIssue.Denied, null); }
        catch (Exception e) when (Integrity(e)) { return new(OutcomePriorityIssue.IntegrityMismatch, null); }
    }
    public async Task<PlanningApplyResult> ApplyPlanningAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid runId, OutcomeAuthority a, PlanningCommand command, CancellationToken ct = default)
    {
        if (runId == Guid.Empty || !OutcomePriorityPolicy.Command(command)) return new(OutcomePriorityIssue.InvalidInput, null);
        if (await Prepare(c, t, a, OutcomeAction.ManagePlanning, ct) is { } denied) return new(denied, null);
        await RunFence(c, t, runId, ct);
        try
        {
            var captured = await Capture(c, t, runId, a, ct);
            if (captured.Issue is not null || captured.Source is null) return new(captured.Issue ?? OutcomePriorityIssue.SourceUnavailable, null);
            var source = captured.Source;
            var snapshot = await LoadPlanning(c, t, source, a, ct);
            var entry = snapshot.Entries.SingleOrDefault(e => e.Original.OptionId == command.OptionId);
            if (entry is null) return new(OutcomePriorityIssue.NotFound, null);
            if (OutcomePriorityPolicy.Authorize(a, scope, OutcomeAction.ManagePlanning, entry.Original.CategoryId) is { } deniedCategory) return new(deniedCategory, null);
            var digest = OutcomePriorityCanonical.Digest(new { schemaVersion = "synthetic-planning-command-v1", scope, runId, a.ActorId, command });
            var replay = await Receipt<PlanningReceipt>(c, t, command.EventId, a.ActorId, digest, "planning", ct);
            if (replay.Issue is not null || replay.Receipt is not null) return new(replay.Issue, replay.Receipt, replay.Receipt is not null);
            if (command.ExpectedSourceDigest != source.SourceDigest) return new(OutcomePriorityIssue.SourceConflict, null);
            if (command.ExpectedRevision != entry.Revision) return new(OutcomePriorityIssue.RevisionConflict, null);
            if (entry.Revision == OutcomePriorityContract.MaximumRevision) return new(OutcomePriorityIssue.RevisionOverflow, null);
            if (!PlanningTransition(entry, command.Kind)) return new(OutcomePriorityIssue.InvalidState, null);
            var now = await Now(c, t, ct);
            var ev = new PlanningEvent(command.EventId, entry.Revision + 1, command.Kind, a.ActorId, now, command.Reason, source.SourceDigest, command.PriorityOverride, command.ReplacementSize, command.Assumptions);
            if (observer is not null) await observer.BeforeWriteAsync("planning", command.EventId, ct);
            await Sql(c, t, "INSERT INTO synthetic_outcome_priority.planning_sources VALUES(@run,@digest,@json) ON CONFLICT DO NOTHING", ct, ("run", runId), ("digest", source.SourceDigest), ("json", OutcomePriorityCanonical.Json(source)));
            await Sql(c, t, "INSERT INTO synthetic_outcome_priority.planning_events VALUES(@run,@option,@revision,@json,@digest)", ct, ("run", runId), ("option", command.OptionId), ("revision", ev.Revision), ("json", OutcomePriorityCanonical.Json(ev)), ("digest", OutcomePriorityCanonical.Digest(ev)));
            await Sql(c, t, "INSERT INTO synthetic_outcome_priority.planning_current VALUES(@run,@option,@revision) ON CONFLICT(run_id,option_id) DO UPDATE SET revision=excluded.revision", ct, ("run", runId), ("option", command.OptionId), ("revision", ev.Revision));
            var receipt = new PlanningReceipt("synthetic-planning-receipt-v1", command.EventId, runId, command.OptionId, ev.Revision, a.ActorId, now, source.SourceDigest);
            await SaveReceipt(c, t, command.EventId, a.ActorId, digest, "planning", receipt, ct);
            return new(null, receipt);
        }
        catch (OutcomeAccessException) { return new(OutcomePriorityIssue.Denied, null); }
        catch (Exception e) when (Integrity(e)) { return new(OutcomePriorityIssue.IntegrityMismatch, null); }
    }
    private static bool PlanningTransition(PlanningEntry e, PlanningKind kind) => kind switch
    {
        PlanningKind.ApproveOriginalEffort => e.OriginalEffort is not null && !e.HasEffortOverride && e.EffortApproval == EffortApprovalState.Proposed,
        PlanningKind.ReplaceEffort => true,
        PlanningKind.WithdrawEffortOverride => e.HasEffortOverride,
        PlanningKind.OverridePriority => true,
        PlanningKind.WithdrawPriorityOverride => e.HasPriorityOverride,
        _ => false
    };
    private async Task<PlanningSourceResult> Capture(NpgsqlConnection c, NpgsqlTransaction t, Guid run, OutcomeAuthority a, CancellationToken ct)
    {
        if (sourceReader is null) return new(OutcomePriorityIssue.SourceUnavailable, null);
        var result = await sourceReader(c, t, run, a, ct);
        if (result.Issue is not null || result.Source is null) return new(result.Issue ?? OutcomePriorityIssue.SourceUnavailable, null);
        if (!OutcomePriorityPolicy.Source(result.Source) || result.Source.RunId != run || result.Source.Scope != scope) return new(OutcomePriorityIssue.IntegrityMismatch, null);
        foreach (var option in result.Source.Options) if (OutcomePriorityPolicy.Authorize(a, scope, OutcomeAction.ReadPlanning, option.CategoryId) is not null) return new(OutcomePriorityIssue.Denied, null);
        return result;
    }
    private async Task<OutcomeRegistrySnapshot> LoadRegistry(NpgsqlConnection c, NpgsqlTransaction t, OutcomeAuthority a, CancellationToken ct)
    {
        var contents = new List<OutcomeContentVersion>();
        await using (var q = Query(c, t, "SELECT content_json,content_digest FROM synthetic_outcome_priority.outcome_versions ORDER BY outcome_id COLLATE \"C\",version"))
        await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
        {
            var content = OutcomePriorityCanonical.Parse<OutcomeContentVersion>(r.GetString(0));
            if (!OutcomePriorityPolicy.Content(content) || content.ContentDigest != r.GetString(1)) throw new OutcomeIntegrityException();
            if (OutcomePriorityPolicy.Authorize(a, scope, OutcomeAction.ReadOutcome, content.CategoryId) is not null) throw new OutcomeAccessException();
            contents.Add(content);
        }
        if (contents.Select(v => v.OutcomeId).Distinct(StringComparer.Ordinal).Count() > 64) throw new OutcomeIntegrityException();
        var entries = ImmutableArray.CreateBuilder<OutcomeEntry>(); long commands = 0;
        var eventIds = new HashSet<Guid>();
        foreach (var content in contents)
        {
            var events = await OutcomeHistory(c, t, content, ct);
            if (events.IsDefaultOrEmpty) throw new OutcomeIntegrityException();
            foreach (var ev in events) eventIds.Add(ev.EventId);
            await using var q = Query(c, t, "SELECT revision,state FROM synthetic_outcome_priority.outcome_current WHERE outcome_id=@id AND version=@version", ("id", content.OutcomeId), ("version", content.Version));
            await using var r = await q.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct) || r.GetInt64(0) != events[^1].Revision || r.GetString(1) != events[^1].State.ToString()) throw new OutcomeIntegrityException();
            entries.Add(new(content, events[^1].State, events[^1].Revision, events));
        }
        commands = eventIds.Count;
        var waters = ImmutableArray.CreateBuilder<OutcomeHighWater>();
        foreach (var group in entries.GroupBy(e => e.Content.OutcomeId))
        {
            if (group.Count(e => e.State == OutcomeState.CustomerApproved) > 1) throw new OutcomeIntegrityException();
            var high = group.Where(e => e.History.Any(h => h.State == OutcomeState.CustomerApproved)).Select(e => e.Content.Version).DefaultIfEmpty(0).Max();
            await using var q = Query(c, t, "SELECT version FROM synthetic_outcome_priority.outcome_highwater WHERE outcome_id=@id", ("id", group.Key));
            var actual = await q.ExecuteScalarAsync(ct);
            if (high == 0 ? actual is not null : actual is null || Convert.ToInt64(actual, CultureInfo.InvariantCulture) != high) throw new OutcomeIntegrityException();
            if (high != 0) waters.Add(new(group.Key, high));
            var versions = group.Select(e => e.Content.Version).ToArray();
            if (!versions.SequenceEqual(Enumerable.Range(1, versions.Length).Select(i => (long)i))) throw new OutcomeIntegrityException();
        }
        await using var revision = Query(c, t, "SELECT revision FROM synthetic_outcome_priority.registry_current WHERE singleton=true");
        var rev = Convert.ToInt64(await revision.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        if (rev != commands) throw new OutcomeIntegrityException();
        var snapshot = new OutcomeRegistrySnapshot(scope, rev, entries.ToImmutable(), waters.ToImmutable(), "");
        return snapshot with { ContentDigest = OutcomePriorityCanonical.Digest(snapshot) };
    }
    private async Task<ImmutableArray<OutcomeEvent>> OutcomeHistory(NpgsqlConnection c, NpgsqlTransaction t, OutcomeContentVersion content, CancellationToken ct)
    {
        var events = ImmutableArray.CreateBuilder<OutcomeEvent>();
        await using var q = Query(c, t, "SELECT revision,event_json,event_digest FROM synthetic_outcome_priority.outcome_events WHERE outcome_id=@id AND version=@version ORDER BY revision", ("id", content.OutcomeId), ("version", content.Version));
        await using var r = await q.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var ev = OutcomePriorityCanonical.Parse<OutcomeEvent>(r.GetString(1));
            if (ev.Revision != r.GetInt64(0) || ev.Revision != events.Count + 1 || ev.OutcomeId != content.OutcomeId || ev.Version != content.Version || ev.ContentDigest != content.ContentDigest || OutcomePriorityCanonical.Digest(ev) != r.GetString(2) || !ValidEvent(ev, events.Count == 0 ? null : events[^1])) throw new OutcomeIntegrityException();
            events.Add(ev);
        }
        return events.ToImmutable();
    }
    private static bool ValidEvent(OutcomeEvent ev, OutcomeEvent? previous)
    {
        if (ev.EventId == Guid.Empty || !OutcomePriorityPolicy.Id(ev.ActorId) || !OutcomePriorityPolicy.Text(ev.Reason) || ev.RecordedAtUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(ev.Kind) || !Enum.IsDefined(ev.State) || !Enum.IsDefined(ev.ActorRole)) return false;
        if (previous is not null && ev.RecordedAtUtc < previous.RecordedAtUtc) return false;
        return ev.State switch
        {
            OutcomeState.Draft => previous is null && ev.Kind == OutcomeKind.CreateDraft && ev.ActorRole == OutcomeRole.Consultant && ev.ReviewedEventId is null && ev.SuccessorVersion is null,
            OutcomeState.ConsultantReviewed => previous?.State == OutcomeState.Draft && ev.Kind == OutcomeKind.Review && ev.ActorRole == OutcomeRole.Consultant && ev.ReviewedEventId is null && ev.SuccessorVersion is null,
            OutcomeState.CustomerApproved => previous?.State == OutcomeState.ConsultantReviewed && ev.Kind == OutcomeKind.Approve && ev.ActorRole == OutcomeRole.CustomerOutcomeApprover && ev.ActorId == OutcomePriorityContract.ApproverId && ev.ReviewedEventId == previous.EventId && ev.SuccessorVersion is null,
            OutcomeState.Superseded => previous?.State == OutcomeState.CustomerApproved && ev.Kind == OutcomeKind.Approve && ev.ActorRole == OutcomeRole.CustomerOutcomeApprover && ev.ActorId == OutcomePriorityContract.ApproverId && ev.SuccessorVersion > ev.Version && ev.ReviewedEventId is null,
            OutcomeState.Retired => ev.Kind == OutcomeKind.Retire && ev.SuccessorVersion is null && ev.ReviewedEventId is null && (previous?.State is OutcomeState.Draft or OutcomeState.ConsultantReviewed ? ev.ActorRole == OutcomeRole.Consultant : previous?.State == OutcomeState.CustomerApproved && ev.ActorRole == OutcomeRole.CustomerOutcomeApprover && ev.ActorId == OutcomePriorityContract.ApproverId),
            _ => false
        };
    }
    private async Task<LockedOutcomeSet?> LoadLock(NpgsqlConnection c, NpgsqlTransaction t, Guid run, OutcomeAuthority a, CancellationToken ct)
    {
        await using var q = Query(c, t, "SELECT lock_json,lock_digest FROM synthetic_outcome_priority.run_locks WHERE run_id=@run", ("run", run));
        await using var r = await q.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var result = OutcomePriorityCanonical.Parse<LockedOutcomeSet>(r.GetString(0));
        if (!ValidLock(result) || result.RunId != run || result.Scope != scope || result.ContentDigest != r.GetString(1)) throw new OutcomeIntegrityException();
        foreach (var item in result.Outcomes) if (OutcomePriorityPolicy.Authorize(a, scope, OutcomeAction.ReadOutcome, item.Content.CategoryId) is not null) throw new OutcomeAccessException();
        return result;
    }
    public static bool ValidLock(LockedOutcomeSet l)
    {
        if (l is null || l.SchemaVersion != "synthetic-outcome-lock-v1" || l.Scope != OutcomeScope.Fixed || l.RunId == Guid.Empty || l.ContractDigest != OutcomePriorityContract.ContractDigest || l.Outcomes.IsDefault || l.Outcomes.Length > 64 || !OutcomePriorityPolicy.ValidDigest(l.ContentDigest) || OutcomePriorityCanonical.Digest(l with { ContentDigest = "" }) != l.ContentDigest || l.Outcomes.Select(o => o.Content.OutcomeId).Distinct(StringComparer.Ordinal).Count() != l.Outcomes.Length) return false;
        foreach (var item in l.Outcomes)
        {
            if (!OutcomePriorityPolicy.Content(item.Content) || item.ApprovalHistory.IsDefaultOrEmpty || OutcomePriorityCanonical.Json(item.ApprovalHistory[^1]) != OutcomePriorityCanonical.Json(item.Approval) || item.Approval.State != OutcomeState.CustomerApproved) return false;
            OutcomeEvent? previous = null;
            foreach (var ev in item.ApprovalHistory)
            { if (ev.Revision != (previous?.Revision ?? 0) + 1 || ev.OutcomeId != item.Content.OutcomeId || ev.Version != item.Content.Version || ev.ContentDigest != item.Content.ContentDigest || !ValidEvent(ev, previous)) return false; previous = ev; }
        }
        return true;
    }
    public static ImmutableArray<ScoringOutcome>? ToScoringOutcomes(LockedOutcomeSet l) => ValidLock(l) ? l.Outcomes.Select(o => new ScoringOutcome(o.Content.OutcomeId, true)).ToImmutableArray() : null;
    public static OutcomePriorityIssue? ValidateApplicability(LockedOutcomeSet l, IReadOnlyCollection<CoverageKey> expectedKeys) => !ValidLock(l) || expectedKeys is null || expectedKeys.Distinct().Count() != expectedKeys.Count || l.Outcomes.SelectMany(o => o.Content.UnitLinks).Any(k => !expectedKeys.Contains(k)) ? OutcomePriorityIssue.IntegrityMismatch : null;
    private static bool SameSelections(LockedOutcomeSet l, ImmutableArray<OutcomeSelection> selections) => l.Outcomes.Length == selections.Length && l.Outcomes.All(o => selections.Any(s => s.OutcomeId == o.Content.OutcomeId && s.Version == o.Content.Version && s.ContentDigest == o.Content.ContentDigest && s.Revision == o.Approval.Revision && s.ApprovalEventId == o.Approval.EventId));
    private async Task<PlanningSnapshot> LoadPlanning(NpgsqlConnection c, NpgsqlTransaction t, Phase1BPlanningSource source, OutcomeAuthority a, CancellationToken ct)
    {
        var sources = new Dictionary<string, Phase1BPlanningSource>(StringComparer.Ordinal);
        await using (var q = Query(c, t, "SELECT source_digest,source_json FROM synthetic_outcome_priority.planning_sources WHERE run_id=@run", ("run", source.RunId)))
        await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
        {
            var old = OutcomePriorityCanonical.Parse<Phase1BPlanningSource>(r.GetString(1));
            if (!OutcomePriorityPolicy.Source(old) || old.SourceDigest != r.GetString(0) || old.RunId != source.RunId || old.Scope != source.Scope || old.RunInputDigest != source.RunInputDigest || old.SourceRevision > source.SourceRevision || old.SourceRevision == source.SourceRevision && old.SourceDigest != source.SourceDigest || old.Options.Any(o => !source.Options.Any(n => n.OptionId == o.OptionId && n.FindingId == o.FindingId && n.CategoryId == o.CategoryId))) throw new OutcomeIntegrityException();
            foreach (var option in old.Options) if (OutcomePriorityPolicy.Authorize(a, scope, OutcomeAction.ReadPlanning, option.CategoryId) is not null) throw new OutcomeAccessException();
            sources.Add(old.SourceDigest, old);
        }
        var histories = new Dictionary<string, List<PlanningEvent>>(StringComparer.Ordinal);
        await using (var q = Query(c, t, "SELECT option_id,revision,event_json,event_digest FROM synthetic_outcome_priority.planning_events WHERE run_id=@run ORDER BY option_id COLLATE \"C\",revision", ("run", source.RunId)))
        await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct))
        {
            var option = r.GetString(0); var ev = OutcomePriorityCanonical.Parse<PlanningEvent>(r.GetString(2));
            if (!histories.TryGetValue(option, out var list)) histories.Add(option, list = []);
            if (!sources.TryGetValue(ev.SourceDigest, out var old) || !old.Options.Any(o => o.OptionId == option) || ev.Revision != list.Count + 1 || ev.Revision != r.GetInt64(1) || ev.EventId == Guid.Empty || !Enum.IsDefined(ev.Kind) || !OutcomePriorityPolicy.Id(ev.ActorId) || !OutcomePriorityPolicy.Text(ev.Reason) || ev.RecordedAtUtc.Offset != TimeSpan.Zero || list.Count > 0 && list[^1].RecordedAtUtc > ev.RecordedAtUtc || OutcomePriorityCanonical.Digest(ev) != r.GetString(3)) throw new OutcomeIntegrityException();
            var projected = ProjectPlanning(old.Options.Single(o => o.OptionId == option), old.SourceDigest, list.ToImmutableArray());
            var cmd = new PlanningCommand(ev.EventId, ev.Kind, option, list.Count, ev.SourceDigest, ev.PriorityOverride, ev.ReplacementSize, ev.Assumptions, ev.Reason);
            if (!OutcomePriorityPolicy.Command(cmd) || !PlanningTransition(projected, ev.Kind)) throw new OutcomeIntegrityException();
            list.Add(ev);
        }
        var currents = new Dictionary<string, long>(StringComparer.Ordinal);
        await using (var q = Query(c, t, "SELECT option_id,revision FROM synthetic_outcome_priority.planning_current WHERE run_id=@run", ("run", source.RunId)))
        await using (var r = await q.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) currents.Add(r.GetString(0), r.GetInt64(1));
        if (currents.Count != histories.Count || histories.Any(h => !currents.TryGetValue(h.Key, out var revision) || revision != h.Value.Count) || histories.Keys.Any(id => !source.Options.Any(o => o.OptionId == id))) throw new OutcomeIntegrityException();
        var entries = source.Options.Select(o => ProjectPlanning(o, source.SourceDigest, histories.TryGetValue(o.OptionId, out var list) ? list.ToImmutableArray() : [])).OrderByDescending(e => e.OriginalPriority.RawPriority.HasValue).ThenByDescending(e => e.OriginalPriority.RawPriority).ThenBy(e => e.Original.FindingId, StringComparer.Ordinal).ThenBy(e => e.Original.OptionId, StringComparer.Ordinal).ToImmutableArray();
        return new(source, entries);
    }
    private static PlanningEntry ProjectPlanning(PlanningSourceOption original, string digest, ImmutableArray<PlanningEvent> history)
    {
        var priority = PriorityProjector.Project(original.Factors) ?? throw new OutcomeIntegrityException();
        var originalEffort = original.Factors.OriginalEffort is { } size ? PriorityProjector.Estimate(size) : null;
        var effective = priority.OriginalBand; var effort = originalEffort; bool priorityOverride = false, effortOverride = false, approvedOriginal = false;
        var approval = EffortApprovalState.Proposed;
        foreach (var ev in history.Where(e => e.SourceDigest == digest))
        {
            switch (ev.Kind)
            {
                case PlanningKind.ApproveOriginalEffort: approvedOriginal = true; approval = EffortApprovalState.ApprovedOriginal; break;
                case PlanningKind.ReplaceEffort: effort = PriorityProjector.Estimate(ev.ReplacementSize!.Value); effortOverride = true; approval = EffortApprovalState.ApprovedReplacement; break;
                case PlanningKind.WithdrawEffortOverride: effort = originalEffort; effortOverride = false; approval = approvedOriginal ? EffortApprovalState.ApprovedOriginal : EffortApprovalState.Proposed; break;
                case PlanningKind.OverridePriority: effective = ev.PriorityOverride; priorityOverride = true; break;
                case PlanningKind.WithdrawPriorityOverride: effective = priority.OriginalBand; priorityOverride = false; break;
            }
        }
        return new(original, digest, history.Length, priority, effective, originalEffort, effort, approval, priorityOverride, effortOverride, history);
    }
    private static async Task AddOutcomeEvent(NpgsqlConnection c, NpgsqlTransaction t, OutcomeEvent ev, CancellationToken ct)
    {
        await Sql(c, t, "INSERT INTO synthetic_outcome_priority.outcome_events VALUES(@id,@version,@revision,@json,@digest)", ct, ("id", ev.OutcomeId), ("version", ev.Version), ("revision", ev.Revision), ("json", OutcomePriorityCanonical.Json(ev)), ("digest", OutcomePriorityCanonical.Digest(ev)));
        await Sql(c, t, "INSERT INTO synthetic_outcome_priority.outcome_current VALUES(@id,@version,@revision,@state) ON CONFLICT(outcome_id,version) DO UPDATE SET revision=excluded.revision,state=excluded.state", ct, ("id", ev.OutcomeId), ("version", ev.Version), ("revision", ev.Revision), ("state", ev.State.ToString()));
    }
    private static async Task<(OutcomePriorityIssue? Issue, T? Receipt)> Receipt<T>(NpgsqlConnection c, NpgsqlTransaction t, Guid id, string actor, string semantic, string kind, CancellationToken ct) where T : class
    {
        await using var q = Query(c, t, "SELECT actor_id,command_digest,receipt_kind,receipt_json,receipt_digest FROM synthetic_outcome_priority.receipts WHERE event_id=@event", ("event", id));
        await using var r = await q.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return (null, null);
        var json = r.GetString(3);
        if (OutcomePriorityCanonical.Hash(json) != r.GetString(4)) throw new OutcomeIntegrityException();
        if (r.GetString(0) != actor || r.GetString(1) != semantic || r.GetString(2) != kind) return (OutcomePriorityIssue.EventConflict, null);
        return (null, OutcomePriorityCanonical.Parse<T>(json));
    }
    private static Task SaveReceipt<T>(NpgsqlConnection c, NpgsqlTransaction t, Guid id, string actor, string semantic, string kind, T receipt, CancellationToken ct) =>
        Sql(c, t, "INSERT INTO synthetic_outcome_priority.receipts VALUES(@event,@actor,@command,@kind,@json,@digest)", ct, ("event", id), ("actor", actor), ("command", semantic), ("kind", kind), ("json", OutcomePriorityCanonical.Json(receipt)), ("digest", OutcomePriorityCanonical.Digest(receipt)));
    private static async Task<DateTimeOffset> Now(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    { await using var q = Query(c, t, "SELECT clock_timestamp()"); return new DateTimeOffset((DateTime)(await q.ExecuteScalarAsync(ct))!); }
    private static NpgsqlCommand Query(NpgsqlConnection c, NpgsqlTransaction t, string sql, params (string Name, object Value)[] parameters)
    { var q = new NpgsqlCommand(sql, c, t); foreach (var p in parameters) q.Parameters.AddWithValue(p.Name, p.Value); return q; }
    private static Task Sql(NpgsqlConnection c, NpgsqlTransaction t, string sql, CancellationToken ct, params (string Name, object Value)[] parameters) => OutcomePriorityMigration.Execute(c, t, sql, ct, parameters);
    private static bool Integrity(Exception e) => e is OutcomeIntegrityException or JsonException or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException or KeyNotFoundException;
    private sealed class OutcomeIntegrityException : Exception;
    private sealed class OutcomeAccessException : Exception;
}
