using System.Collections.Immutable;
using SyntheticPlanningTasks;
using SyntheticTaskCsv;

namespace SyntheticTaskCsvIntegration;

public static class CsvExportCaptureAdapter
{
    public static CsvResult CreateEnvelope(PlanningTaskExportCapture capture, CsvVersionLocks versions)
    {
        try
        {
            if (capture is null || capture.Scope != PlanningTaskScope.Fixed || capture.Rows.IsDefault) return new(CsvIssue.InvalidInput, null);
            var source = capture.Source.ArtifactSource;
            if (source.RunId != capture.RunId || source.BaselineId != versions.BaselineId || source.ProfileId != versions.ProfileId ||
                source.RunInputDigest != versions.RunInputDigest || source.ApplicationVersion != versions.FrozenVersions.ApplicationVersion ||
                capture.Source.PlanningTaskContractDigest != versions.FrozenVersions.PlanningTaskContractDigest ||
                source.ContractDigest != versions.FrozenVersions.FixReviewContractDigest || source.TemplateDigest != versions.FrozenVersions.FixPackageTemplateDigest)
                return new(CsvIssue.VersionMismatch, null);
            var binding = new CsvSourceBinding(source.SourceDigest, source.GuidanceDigest, source.FindingReviewDigest, source.RunRevision,
                source.FindingRevisions.Select(item => new CsvFindingRevision(item.FindingId, item.Revision)).ToImmutableArray());
            var attestations = capture.Rows.SelectMany(row => row.CurrentAttestations.Select(item => new CsvAttestation(row.Identity.TaskId,
                item.ArtifactId, item.Revision, item.EventId, item.Kind?.ToString(), item.State.ToString(), item.SourceDigest))).ToImmutableArray();
            var rows = capture.Rows.Select(row => new CsvTaskRow(CsvCodec.ContractVersion, capture.RunId.ToString("D"), row.Identity.TaskId,
                row.Identity.FindingId, row.Identity.PackageId, row.Identity.ScopedOptionId, row.AssigneeId, row.Revision, row.Status.ToString(),
                row.Freshness.ToString(), CsvCodec.Utc(row.CreatedAtUtc), CsvCodec.Utc(row.PlannedAtUtc), source.SourceDigest,
                row.PlannedSourceDigest, "", CsvCodec.TaskLink(capture.RunId, row.Identity.TaskId), CsvCodec.FindingLink(capture.RunId, row.Identity.FindingId))).ToImmutableArray();
            return CsvCodec.Freeze(new(CsvCodec.SchemaVersion, CsvScope.Fixed, capture.RunId, versions, binding, attestations, rows, ""));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NullReferenceException)
        { return new(CsvIssue.InvalidInput, null); }
    }
    public static CsvIssue? Recheck(CsvEnvelope captured, CsvEnvelope current, PlanningTaskExportAuthority authority)
    {
        if (PlanningTaskExportPolicy.Authorize(authority, PlanningTaskScope.Fixed) is not null) return CsvIssue.Denied;
        if (!CsvCodec.Parse(CsvCanonical.Json(captured)).Succeeded || !CsvCodec.Parse(CsvCanonical.Json(current)).Succeeded) return CsvIssue.IntegrityMismatch;
        return CsvCanonical.Bytes(captured).AsSpan().SequenceEqual(CsvCanonical.Bytes(current)) ? null : CsvIssue.SourceConflict;
    }
}
