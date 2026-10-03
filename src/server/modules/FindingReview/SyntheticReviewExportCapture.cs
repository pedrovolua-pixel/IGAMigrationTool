using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace FindingReview;

public enum SyntheticReviewExportRole { Consultant, Auditor }
public sealed record SyntheticReviewExportAuthority(string ActorId, SyntheticReviewScope Scope,
    SyntheticReviewExportRole Role, ImmutableArray<string> Categories, bool Authenticated, bool Active,
    bool AssignmentActive, bool Revoked, bool TaskExportGranted, bool CustomerExportAllowed,
    bool AuditorScopedExportGranted, bool ResourceAvailable);
public sealed record SyntheticReviewExportFinding(string FindingId, string Category, long Revision, string State);
/// <summary>Only metadata serializes. Verified protected proof is an internal in-process export adapter input.</summary>
public sealed record SyntheticReviewExportCapture(string SnapshotDigest, ImmutableArray<SyntheticReviewExportFinding> Findings,
    [property: JsonIgnore] SyntheticReviewSnapshot Snapshot);
public sealed record SyntheticReviewExportResult(SyntheticReviewIssue? Issue, SyntheticReviewExportCapture? Capture)
{ public bool Succeeded => Issue is null && Capture is not null; }

public sealed partial class SyntheticReviewStore
{
    public async Task<SyntheticReviewExportResult> ReadForExportInTransactionAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid runId, SyntheticReviewExportAuthority authority, CancellationToken cancellationToken = default)
    {
        if (authority is null || authority.Scope != SyntheticReviewScope.Fixed) return new(SyntheticReviewIssue.WrongScope, null);
        if (string.IsNullOrWhiteSpace(authority.ActorId) || authority.ActorId.Length > 256 || !Enum.IsDefined(authority.Role) ||
            !authority.Authenticated || !authority.Active || !authority.AssignmentActive || authority.Revoked || !authority.TaskExportGranted ||
            !authority.CustomerExportAllowed || authority.Role == SyntheticReviewExportRole.Auditor && !authority.AuditorScopedExportGranted ||
            !authority.ResourceAvailable || authority.Categories.IsDefaultOrEmpty || authority.Categories.Any(c => c is not ("SECURITY" or "OPERATIONS")))
            return new(SyntheticReviewIssue.Denied, null);
        if (runId == Guid.Empty || !ValidSharedConnection(connection, transaction)) return new(SyntheticReviewIssue.InvalidInput, null);
        await SyntheticSourceFence.SyntheticRunSourceFence.AcquireAsync(connection, transaction,
            authority.Scope.CustomerId, authority.Scope.ProjectId, authority.Scope.EnvironmentId, runId, cancellationToken);
        if (await SyntheticReviewMigration.VerifyAsync(connection, transaction, authority.Scope, cancellationToken) is { } issue) return new(issue, null);
        try
        {
            var seed = await ReadSeed(connection, transaction, authority.Scope, runId, cancellationToken);
            if (seed is null) return new(SyntheticReviewIssue.NotFound, null);
            if (seed.ResourceState != SyntheticReviewResourceState.Mutable || seed.Findings.Any(f => !authority.Categories.Contains(f.CategoryId)))
                return new(SyntheticReviewIssue.Denied, null);
            var findings = ImmutableArray.CreateBuilder<SyntheticReviewedFinding>();
            foreach (var original in seed.Findings) findings.Add(await ReadFinding(connection, transaction, seed, original.FindingId, false, cancellationToken));
            var snapshot = new SyntheticReviewSnapshot(seed, findings.ToImmutable(), "");
            snapshot = snapshot with { SnapshotDigest = SyntheticReviewDigest.Compute(snapshot) };
            return new(null, new(snapshot.SnapshotDigest, snapshot.Findings.Select(f => new SyntheticReviewExportFinding(f.Seed.FindingId,
                f.Seed.CategoryId, f.Current.Revision, f.Current.State.ToString())).ToImmutableArray(), snapshot));
        }
        catch (SyntheticReviewIntegrityException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
        catch (JsonException) { return new(SyntheticReviewIssue.IntegrityMismatch, null); }
    }
}
