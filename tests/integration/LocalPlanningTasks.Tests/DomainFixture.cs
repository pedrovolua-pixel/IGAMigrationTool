using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using RecommendationGuidance;
using SyntheticFixPackages;
using SyntheticFixReview;
using SyntheticPlanningTasks;

internal sealed class DomainFixture
{
    internal readonly Guid RunId;
    internal GuidanceInput Current;
    internal bool Unavailable;
    internal int Calls;
    internal NpgsqlConnection? CapturedConnection;
    internal NpgsqlTransaction? CapturedTransaction;
    internal DomainFixture(string name = "normal", Guid? runId = null)
    {
        RunId = runId ?? Guid.NewGuid();
        var input = PortableCases.Input(name);
        Current = input with { Source = input.Source with { RunId = RunId, ReviewRunId = RunId } };
    }
    internal FixPackageSnapshot Package()
    {
        var guidance = RecommendationGuidanceBuilder.Build(Current);
        Check.That(guidance.Succeeded, "actual-domain-current-guidance-builder");
        var package = FixPackageBuilder.Build(guidance.Snapshot);
        Check.That(package.Succeeded, "actual-domain-current-original-package-builder");
        return package.Snapshot!;
    }
    internal ArtifactReviewSourceResult ArtifactSource() => ArtifactReviewSourceBuilder.Build(Package());
    internal Task<ArtifactReviewSourceResult> ReadArtifact(Guid run, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(run == RunId && !Unavailable ? ArtifactSource() : new(ArtifactReviewIssue.SourceUnavailable, null));
    }
    internal SyntheticFixReviewStore ArtifactStore(ISyntheticFixReviewCommitObserver? observer = null) =>
        new(OwnedDatabase.Connection, ArtifactReviewScope.Fixed, ReadArtifact, observer);
    internal SyntheticPlanningTaskStore Store(ISyntheticPlanningTaskCommitObserver? observer = null) =>
        new(OwnedDatabase.Connection, PlanningTaskScope.Fixed, ReadTask, observer);
    internal async Task<PlanningTaskSourceResult> ReadTask(NpgsqlConnection db, NpgsqlTransaction tx, Guid run, PlanningTaskAuthority authority, CancellationToken ct)
    {
        Calls++; CapturedConnection = db; CapturedTransaction = tx;
        Check.That(tx.Connection == db && db.State == System.Data.ConnectionState.Open, "actual-source-capture-same-live-connection-transaction");
        if (Unavailable || run != RunId) return new(PlanningTaskIssue.SourceUnavailable, null);
        var package = Package();
        var artifacts = ArtifactReviewSourceBuilder.Build(package);
        Check.That(artifacts.Succeeded, "actual-package-to-verified-artifact-source");
        var read = await ArtifactStore().ReadInTransactionAsync(db, tx, run,
            Policies.ArtifactConsultant with { ActorId = authority.ActorId }, artifacts.Source!, ct);
        if (!read.Succeeded) return new(PlanningTaskIssue.SourceUnavailable, null);
        Check.That(tx.Connection == db && db.State == System.Data.ConnectionState.Open, "module-attestation-read-keeps-caller-transaction-live");
        return PlanningTaskSourceBuilder.Build(package, read.Snapshot);
    }
    internal async Task Initialize()
    {
        await ArtifactStore().InitializeAsync();
        await Store().InitializeAsync();
    }
    internal async Task<PlanningTaskSnapshot> Read(PlanningTaskAuthority? authority = null)
    {
        var read = await Store().ReadAsync(RunId, authority ?? Policies.Consultant);
        Check.That(read.Succeeded, "actual-owned-task-store-ready-read");
        return read.Snapshot!;
    }
    internal async Task Review(PlanningTaskOption option)
    {
        var source = ArtifactSource().Source!;
        foreach (var id in option.Identity.ArtifactIds)
        {
            var current = await ArtifactStore().ReadAsync(RunId, Policies.ArtifactConsultant);
            var entry = current.Snapshot!.Entries.Single(e => e.Artifact.ArtifactId == id);
            if (entry.State == ArtifactReviewState.ReviewedForPlanning) continue;
            var accepted = await ArtifactStore().ApplyAsync(RunId, id, Policies.ArtifactConsultant,
                new(Guid.NewGuid(), ArtifactReviewKind.ReviewForPlanning, entry.Revision, source.Binding.SourceDigest, "Fictional actual artifact review before explicit planning"));
            Check.That(accepted.Succeeded, "actual-module-artifact-review-accepted-no-fake-positive-snapshot");
        }
    }
    internal async Task Withdraw(string id)
    {
        var read = await ArtifactStore().ReadAsync(RunId, Policies.ArtifactConsultant);
        var entry = read.Snapshot!.Entries.Single(e => e.Artifact.ArtifactId == id);
        Check.That((await ArtifactStore().ApplyAsync(RunId, id, Policies.ArtifactConsultant,
            new(Guid.NewGuid(), ArtifactReviewKind.WithdrawReview, entry.Revision, read.Snapshot.Source.SourceDigest, "Fictional actual selected withdrawal"))).Succeeded, "actual-module-artifact-withdrawal");
    }
    internal void Advance(bool restoreText = false, bool rejected = false)
    {
        var revision = Current.Findings[0].FindingRevision + 1;
        Current = Current with
        {
            Source = Current.Source with { ReviewSnapshotDigest = Expected.Hash("v14-finding-review-" + revision) },
            Findings = Current.Findings.Select(f => f with
            {
                FindingRevision = revision,
                CurrentState = rejected ? "Rejected" : f.CurrentState,
                PresentationTitle = restoreText ? f.OriginalTitle : "Changed fictional V14 presentation"
            }).ToImmutableArray()
        };
    }
    internal PlanningTaskCommand Command(PlanningTaskSnapshot snapshot, PlanningTaskOption option, PlanningTaskKind kind = PlanningTaskKind.Create, long revision = 0, Guid? eventId = null, string? reason = null) =>
        new(eventId ?? Guid.NewGuid(), kind, revision, snapshot.Source!.ArtifactSource.SourceDigest, option.CurrentAttestations,
            reason ?? "  Fictional V14 planning reason café 中文 😀\0NUL\r\nCRLF\rCR  ");
    internal Task<PlanningTaskApplyResult> Apply(PlanningTaskOption option, PlanningTaskCommand command, PlanningTaskAuthority? authority = null) =>
        Store().ApplyAsync(RunId, option.Identity.TaskId, authority ?? Policies.Consultant, command);
    internal void Verify(PlanningTaskSnapshot snapshot)
    {
        var package = Package();
        var original = JsonSerializer.SerializeToNode(package, V14Program.Web)!.AsObject();
        original.Remove("canonicalJson"); original.Remove("contentDigest");
        Check.Equal(Expected.Canonical(original), Expected.Canonical(Expected.FixPayload(original["guidance"]!.AsObject())), "dynamic-actual-package-independent-complete-recipe");
        Check.Equal(Expected.Canonical(JsonSerializer.SerializeToNode(snapshot.Source, V14Program.Web)), Expected.Canonical(Expected.TaskBinding(original)), "dynamic-actual-task-source-independent-complete-binding");
        foreach (var option in snapshot.Options)
        {
            Check.Equal(option.Identity.TaskId, Expected.TaskId(Expected.Binding(original), option.Identity.FindingId, option.Identity.ScopedOptionId), "dynamic-task-stable-scoped-independent-identity");
            Check.Equal(option.CurrentAttestations.Length, 3, "complete-selected-three-vector");
            Check.That(option.CurrentAttestations.Select(a => a.ArtifactId).SequenceEqual(option.Identity.ArtifactIds), "ordinal-selected-exact-artifact-identity-vector");
        }
        Check.Equal(package.Status, "Unverified", "task-source-never-changes-original-artifact-status");
    }
}
