using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using RecommendationGuidance;
using SyntheticFixPackages;
using SyntheticFixReview;

namespace SyntheticPlanningTasks;

public static class PlanningTaskSourceBuilder
{
    public const string ProfileId = "synthetic-review-maturity-planning-tasks-equal-v1";
    public const string ApplicationVersion = "synthetic-planning-tasks-app-v1";
    public const string ContractDigest = "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2";
    public static string TaskId(PlanningTaskScope scope, Guid runId, string findingId, string scopedOptionId) =>
        PlanningTaskCanonical.Digest(new { schemaVersion = "synthetic-planning-task-identity-v1", scope, runId, findingId, scopedOptionId });
    public static PlanningTaskSourceResult Build(FixPackageSnapshot? packages, ArtifactReviewSnapshot? artifacts)
    {
        if (packages is null || artifacts is null) return new(PlanningTaskIssue.SourceUnavailable, null);
        try
        {
            var rebuilt = FixPackageBuilder.Build(packages.Guidance);
            if (!rebuilt.Succeeded) return new(PlanningTaskIssue.IntegrityMismatch, null);
            var actual = FixPackageBuilder.CanonicalPayload(packages);
            var verified = rebuilt.Snapshot!;
            if (!actual.AsSpan().SequenceEqual(FixPackageBuilder.CanonicalPayload(verified)) || packages.CanonicalJson != Encoding.UTF8.GetString(actual) ||
                packages.ContentDigest != verified.ContentDigest || packages.ContentDigest != PlanningTaskCanonical.Hash(packages.CanonicalJson))
                return new(PlanningTaskIssue.IntegrityMismatch, null);
            var upstream = ArtifactReviewSourceBuilder.Build(verified);
            if (!upstream.Succeeded) return new(PlanningTaskIssue.SourceUnavailable, null);
            var source = upstream.Source!;
            var frozen = verified.Guidance.Source.FrozenVersions;
            var phase1b = source.Binding.ProfileId == Phase1BExportCompatibility.ProfileId;
            if ((phase1b ? source.Binding.ApplicationVersion != Phase1BExportCompatibility.ApplicationVersion || !Phase1BExportCompatibility.ValidFrozenVersions(frozen) : source.Binding.ProfileId != ProfileId || source.Binding.ApplicationVersion != ApplicationVersion || frozen.EnumerateObject().Count() != 14) ||
                frozen.GetProperty("planningTaskContractDigest").GetString() != ContractDigest ||
                PlanningTaskCanonical.Json(artifacts.Source) != PlanningTaskCanonical.Json(source.Binding) || string.IsNullOrWhiteSpace(artifacts.ActorId) || artifacts.Entries.IsDefault ||
                artifacts.Entries.Length != source.Artifacts.Length) return new(PlanningTaskIssue.IntegrityMismatch, null);
            for (var index = 0; index < artifacts.Entries.Length; index++)
            {
                var entry = artifacts.Entries[index];
                if (entry is null || entry.Artifact != source.Artifacts[index] || entry.History.IsDefault) throw new PlanningTaskIntegrityException();
                var eventIds = new HashSet<Guid>();
                ArtifactReviewEvent? previous = null;
                foreach (var item in entry.History)
                {
                    if (item is null || item.Revision != (previous?.Revision ?? 0) + 1 || item.Revision > PlanningTaskPolicy.MaximumRevision || item.EventId == Guid.Empty || !eventIds.Add(item.EventId) ||
                        !Enum.IsDefined(item.Kind) || !item.ActorRoles.SequenceEqual(["Consultant"]) || string.IsNullOrWhiteSpace(item.ActorId) || item.RecordedAtUtc.Offset != TimeSpan.Zero ||
                        ArtifactReviewPolicy.ValidateCommand(new(item.EventId, item.Kind, item.Revision - 1, item.Source.SourceDigest, item.Reason)) is not null ||
                        !Compatible(source.Binding, item.Source) || !Dominates(source.Binding, item.Source) || previous is not null && !Dominates(item.Source, previous.Source) ||
                        ArtifactReviewPolicy.Transition(ArtifactReviewPolicy.CurrentState(previous, item.Source.SourceDigest), item.Kind) is not null ||
                        item.RecordedState != (item.Kind == ArtifactReviewKind.ReviewForPlanning ? ArtifactReviewState.ReviewedForPlanning : ArtifactReviewState.Unverified))
                        throw new PlanningTaskIntegrityException();
                    previous = item;
                }
                var state = ArtifactReviewPolicy.CurrentState(previous, source.Binding.SourceDigest);
                if (entry.Revision != (previous?.Revision ?? 0) || entry.State != state || entry.CanReview != (state != ArtifactReviewState.ReviewedForPlanning) ||
                    entry.CanWithdraw != (state == ArtifactReviewState.ReviewedForPlanning)) throw new PlanningTaskIntegrityException();
            }
            var entries = artifacts.Entries.ToDictionary(entry => entry.Artifact.ArtifactId, StringComparer.Ordinal);
            var findings = verified.Guidance.Findings.ToDictionary(finding => finding.FindingId, StringComparer.Ordinal);
            var options = verified.Packages.SelectMany(package => package.Options.Select(option =>
            {
                var ids = option.Artifacts.Select(artifact => artifact.ArtifactId).Order(StringComparer.Ordinal).ToImmutableArray();
                if (ids.Length != 3 || !option.Artifacts.Select(artifact => artifact.Kind).Order(StringComparer.Ordinal).SequenceEqual(["Configuration", "Script", "Sql"])) throw new PlanningTaskIntegrityException();
                var vector = ids.Select(id => Attestation(entries[id], source.Binding.SourceDigest)).ToImmutableArray();
                if (!PlanningTaskPolicy.ValidVector(vector)) throw new PlanningTaskIntegrityException();
                var finding = findings[package.FindingId];
                var scope = new PlanningTaskScope(source.Binding.Scope.CustomerId, source.Binding.Scope.ProjectId, source.Binding.Scope.EnvironmentId);
                var identity = new PlanningTaskIdentity(TaskId(scope, source.Binding.RunId, finding.FindingId, option.ScopedOptionId), finding.FindingId, finding.CategoryId, package.PackageId, option.ScopedOptionId, ids);
                return new PlanningTaskOption(identity, finding.CurrentState, vector, finding.CurrentState != "Rejected" && vector.All(item => item.State == ArtifactReviewState.ReviewedForPlanning));
            })).OrderBy(option => option.Identity.TaskId, StringComparer.Ordinal).ToImmutableArray();
            // Detach the captured public record graph, including arrays, from the caller's snapshot object.
            var detached = PlanningTaskCanonical.Parse<ArtifactReviewSnapshot>(PlanningTaskCanonical.Json(artifacts));
            return new(null, new(verified, detached, new(source.Binding, ContractDigest), options));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException && exception is not OperationCanceledException)
        { return new(PlanningTaskIssue.IntegrityMismatch, null); }
    }
    internal static PlanningTaskSource Restore(string canonicalPackage, string artifactJson)
    {
        using var document = JsonDocument.Parse(canonicalPackage);
        var guidance = PlanningTaskCanonical.Parse<GuidanceSnapshot>(document.RootElement.GetProperty("guidance").GetRawText());
        var package = FixPackageBuilder.Build(guidance);
        if (!package.Succeeded || package.Snapshot!.CanonicalJson != canonicalPackage) throw new PlanningTaskIntegrityException();
        var result = Build(package.Snapshot, PlanningTaskCanonical.Parse<ArtifactReviewSnapshot>(artifactJson));
        if (!result.Succeeded || PlanningTaskCanonical.Json(result.Source!.Artifacts) != artifactJson) throw new PlanningTaskIntegrityException();
        return result.Source!;
    }
    internal static bool Dominates(ArtifactReviewSourceBinding newer, ArtifactReviewSourceBinding older) => newer.RunRevision >= older.RunRevision &&
        newer.FindingRevisions.Length == older.FindingRevisions.Length && newer.FindingRevisions.Zip(older.FindingRevisions).All(pair => pair.First.FindingId == pair.Second.FindingId && pair.First.Revision >= pair.Second.Revision);
    internal static bool SameRevision(ArtifactReviewSourceBinding first, ArtifactReviewSourceBinding second) => first.RunRevision == second.RunRevision && first.FindingRevisions.SequenceEqual(second.FindingRevisions);
    internal static bool Compatible(ArtifactReviewSourceBinding first, ArtifactReviewSourceBinding second) => first.Scope == second.Scope && first.RunId == second.RunId &&
        first.RunInputDigest == second.RunInputDigest && first.BaselineId == second.BaselineId && first.ProfileId == second.ProfileId && first.ApplicationVersion == second.ApplicationVersion &&
        first.ContractDigest == second.ContractDigest && first.TemplateVersion == second.TemplateVersion && first.TemplateDigest == second.TemplateDigest &&
        PlanningTaskPolicy.ValidDigest(second.SourceDigest) && PlanningTaskPolicy.ValidDigest(second.GuidanceDigest) && PlanningTaskPolicy.ValidDigest(second.FindingReviewDigest) &&
        second.RunRevision >= 0 && second.RunRevision <= PlanningTaskPolicy.MaximumRevision && !second.FindingRevisions.IsDefault && second.FindingRevisions.All(item => item is not null && PlanningTaskPolicy.ValidDigest(item.FindingId) && item.Revision >= 0 && item.Revision <= PlanningTaskPolicy.MaximumRevision) &&
        (!SameRevision(first, second) || first.SourceDigest == second.SourceDigest) &&
        (first.SourceDigest != second.SourceDigest || PlanningTaskCanonical.Json(first) == PlanningTaskCanonical.Json(second));
    internal static PlanningTaskAttestation Attestation(ArtifactReviewEntry entry, string sourceDigest)
    {
        var latest = entry.History.LastOrDefault();
        return new(entry.Artifact.ArtifactId, entry.Revision, latest?.EventId, latest?.Kind, ArtifactReviewPolicy.CurrentState(latest, sourceDigest), latest?.Source.SourceDigest);
    }
}
