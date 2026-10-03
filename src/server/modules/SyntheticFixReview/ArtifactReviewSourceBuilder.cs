using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using RecommendationGuidance;
using SyntheticFixPackages;

namespace SyntheticFixReview;

public static class ArtifactReviewSourceBuilder
{
    public const string ProfileId = "synthetic-review-maturity-fix-review-equal-v1";
    public const string ApplicationVersion = "synthetic-fix-review-app-v1";
    public const string ContractDigest = "a0dca320bcf11dda2f03abc16387f75395caff48e9c917c6b58a2826eb5a8b0f";
    public const string PlanningTaskProfileId = "synthetic-review-maturity-planning-tasks-equal-v1";
    public const string PlanningTaskApplicationVersion = "synthetic-planning-tasks-app-v1";
    public const string PlanningTaskContractDigest = "f4d2c4c4974ac801d9b9a538065f1c1dd89f1519796edb384ea6f0506e027cf2";
    public const string TemplateDigest = "a40f3ccb1128581f36de236dbca3353097f4034b6738bcd01a98275229bee669";
    public static ArtifactReviewSourceResult Build(FixPackageSnapshot? packages)
    {
        if (packages is null) return new(ArtifactReviewIssue.SourceUnavailable, null);
        try
        {
            var rebuilt = FixPackageBuilder.Build(packages.Guidance);
            if (!rebuilt.Succeeded) return new(ArtifactReviewIssue.IntegrityMismatch, null);
            var actual = FixPackageBuilder.CanonicalPayload(packages);
            var verified = rebuilt.Snapshot!;
            if (!actual.AsSpan().SequenceEqual(FixPackageBuilder.CanonicalPayload(verified)) ||
                packages.CanonicalJson != Encoding.UTF8.GetString(actual) || packages.ContentDigest != verified.ContentDigest ||
                packages.ContentDigest != ArtifactReviewCanonical.Hash(packages.CanonicalJson)) return new(ArtifactReviewIssue.IntegrityMismatch, null);
            var source = verified.Guidance.Source;
            var scope = new ArtifactReviewScope(source.Scope.CustomerId, source.Scope.ProjectId, source.Scope.EnvironmentId);
            if (scope != ArtifactReviewScope.Fixed) return new(ArtifactReviewIssue.WrongScope, null);
            var versions = source.FrozenVersions;
            var planningTasks = source.ProfileId == PlanningTaskProfileId;
            var expectedApplication = planningTasks ? PlanningTaskApplicationVersion : ApplicationVersion;
            if ((!planningTasks && source.ProfileId != ProfileId) || source.RunState != "Scoring" || source.RunId == Guid.Empty || source.ReviewRunId != source.RunId ||
                source.RunRevision < 0 || source.RunRevision > ArtifactReviewPolicy.MaximumRevision || source.ReviewRunRevision != source.RunRevision ||
                versions.ValueKind != JsonValueKind.Object || versions.EnumerateObject().Count() != (planningTasks ? 14 : 13) ||
                versions.GetProperty("applicationVersion").GetString() != expectedApplication ||
                planningTasks && versions.GetProperty("planningTaskContractDigest").GetString() != PlanningTaskContractDigest ||
                versions.GetProperty("fixReviewContractDigest").GetString() != ContractDigest ||
                versions.GetProperty("fixPackageTemplateDigest").GetString() != TemplateDigest ||
                verified.TemplateDigest != TemplateDigest || verified.TemplateVersion != FixPackageBuilder.TemplateVersion)
                return new(ArtifactReviewIssue.SourceUnavailable, null);
            var revisions = verified.Guidance.Findings.Select(finding => new ArtifactReviewFindingRevision(finding.FindingId, finding.FindingRevision)).ToImmutableArray();
            if (revisions.Any(item => item.Revision < 0 || item.Revision > ArtifactReviewPolicy.MaximumRevision)) return new(ArtifactReviewIssue.RevisionOverflow, null);
            var binding = new ArtifactReviewSourceBinding(scope, source.RunId, source.RunRevision, source.RunInputDigest, source.BaselineId,
                source.ProfileId, expectedApplication, ContractDigest, verified.ContentDigest, verified.Guidance.ContentDigest, source.ReviewSnapshotDigest,
                verified.TemplateVersion, verified.TemplateDigest, revisions);
            var findings = verified.Guidance.Findings.ToDictionary(item => item.FindingId, StringComparer.Ordinal);
            var artifacts = verified.Packages.SelectMany(package => package.Options.SelectMany(option => option.Artifacts.Select(artifact =>
                new ArtifactReviewArtifact(package.FindingId, findings[package.FindingId].CategoryId, package.PackageId, option.ScopedOptionId,
                    artifact.ArtifactId, artifact.TemplateId, artifact.Kind, ArtifactReviewCanonical.Hash(artifact.Text)))))
                .OrderBy(item => item.FindingId, StringComparer.Ordinal).ThenBy(item => item.ArtifactId, StringComparer.Ordinal).ToImmutableArray();
            if (artifacts.Length > FixPackageBuilder.MaximumRecords || artifacts.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
                return new(ArtifactReviewIssue.IntegrityMismatch, null);
            return new(null, new ArtifactReviewSource(verified, binding, artifacts));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { return new(ArtifactReviewIssue.IntegrityMismatch, null); }
    }
    internal static ArtifactReviewSource Restore(string canonicalPackage)
    {
        using var document = JsonDocument.Parse(canonicalPackage);
        var guidance = ArtifactReviewCanonical.Parse<GuidanceSnapshot>(document.RootElement.GetProperty("guidance").GetRawText());
        var package = FixPackageBuilder.Build(guidance);
        if (!package.Succeeded || package.Snapshot!.CanonicalJson != canonicalPackage) throw new ArtifactReviewIntegrityException();
        var result = Build(package.Snapshot);
        return result.Source ?? throw new ArtifactReviewIntegrityException();
    }
}
public sealed class ArtifactReviewIntegrityException() : Exception("Synthetic artifact review integrity denied.");
