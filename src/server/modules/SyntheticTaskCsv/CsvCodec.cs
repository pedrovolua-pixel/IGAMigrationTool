using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SyntheticTaskCsv;

public static class CsvCodec
{
    public const string SchemaVersion = "synthetic-task-csv-envelope-v1";
    public const string ContractVersion = "synthetic-task-csv-v1";
    public const string ContractDigest = "108537455b4f4beac92b0cb24c526d261385662f8ff64dc6af1c5c172ef7ecc7";
    public const string TrustedOrigin = "http://localhost:5183";
    public const int MaximumRows = 1000, MaximumCellBytes = 2048, MaximumLinkBytes = 256, MaximumInputBytes = 1024 * 1024,
        MaximumOutputBytes = 4 * 1024 * 1024, MaximumDepth = 32;
    public const long MaximumRevision = 9_007_199_254_740_991;
    public static ImmutableArray<string> Headers { get; } = ["csv_contract_version", "run_id", "task_id", "finding_id", "package_id", "scoped_option_id",
        "assignee_id", "task_revision", "task_status", "plan_freshness", "created_at_utc", "planned_at_utc", "current_source_digest", "planned_source_digest",
        "export_snapshot_digest", "task_link", "finding_link"];
    public static string TaskLink(Guid runId, string taskId) => TrustedOrigin + "/?run=" + runId.ToString("D") + "&view=tasks&task=" + taskId;
    public static string FindingLink(Guid runId, string findingId) => TrustedOrigin + "/?run=" + runId.ToString("D") + "&view=findings&finding=" + findingId;
    public static string Utc(DateTimeOffset value) => value.Offset == TimeSpan.Zero ? value.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture) : throw new ArgumentException("UTC required.");
    public static bool ValidDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static CsvResult Freeze(CsvEnvelope? input)
    {
        try
        {
            if (input is null || input.Rows.IsDefault || input.SelectedAttestations.IsDefault) return new(CsvIssue.InvalidInput, null);
            var blank = input with { SnapshotDigest = "", Rows = input.Rows.IsDefault ? [] : input.Rows.Select(row => row with { ExportSnapshotDigest = "" }).OrderBy(row => row.TaskId, StringComparer.Ordinal).ToImmutableArray(),
                SelectedAttestations = input.SelectedAttestations.IsDefault ? [] : input.SelectedAttestations.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).ThenBy(item => item.TaskId, StringComparer.Ordinal).ToImmutableArray() };
            if (Validate(blank, false) is { } issue) return new(issue, null);
            var digest = CsvCanonical.Hash(CsvCanonical.Bytes(blank));
            var frozen = blank with { SnapshotDigest = digest, Rows = blank.Rows.Select(row => row with { ExportSnapshotDigest = digest }).ToImmutableArray() };
            if (CsvCanonical.Bytes(frozen).Length > MaximumInputBytes) return new(CsvIssue.LimitExceeded, null);
            return new(null, frozen);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or NullReferenceException or InvalidOperationException)
        { return new(CsvIssue.InvalidInput, null); }
    }
    public static CsvResult Parse(string? json)
    {
        try
        {
            if (json is null) return new(CsvIssue.InvalidInput, null);
            if (CsvCanonical.Utf8.GetByteCount(json) > MaximumInputBytes) return new(CsvIssue.LimitExceeded, null);
            var envelope = CsvCanonical.Parse<CsvEnvelope>(json);
            if (Validate(envelope, true) is { } issue) return new(issue, null);
            var frozen = Freeze(envelope);
            if (!frozen.Succeeded || !CsvCanonical.Bytes(envelope).AsSpan().SequenceEqual(CsvCanonical.Bytes(frozen.Envelope))) return new(CsvIssue.IntegrityMismatch, null);
            return frozen;
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or NullReferenceException or InvalidOperationException)
        { return new(CsvIssue.InvalidInput, null); }
    }
    public static CsvIssue? Validate(CsvEnvelope envelope, bool requireDigest = true)
    {
        if (envelope.SchemaVersion != SchemaVersion) return CsvIssue.VersionMismatch;
        if (envelope.Scope != CsvScope.Fixed) return CsvIssue.WrongScope;
        if (envelope.RunId == Guid.Empty || envelope.Versions is null || envelope.CurrentSourceBinding is null || envelope.Rows.IsDefault || envelope.SelectedAttestations.IsDefault) return CsvIssue.InvalidInput;
        var versions = envelope.Versions; var source = envelope.CurrentSourceBinding;
        var frozenVersions = versions.FrozenVersions; var locks = frozenVersions?.Phase1bLocks;
        if (versions.BaselineId != "synthetic-phase1b-baseline-v1" || versions.ProfileId != "synthetic-phase1b-combined-v1" ||
            frozenVersions is null || locks is null || frozenVersions.ProfileVersion != "synthetic-profile-v1" ||
            frozenVersions.ApplicationVersion != "synthetic-phase1b-app-v1" || frozenVersions.ScoringAlgorithmVersion != "pilot-health-v1" ||
            locks.SchemaVersion != "synthetic-phase1b-input-v1" || locks.CsvContractDigest != ContractDigest || locks.OutcomeContractDigest != "f7775ef74297f0b2e563c01d435bf4800fd07a71ea9ce18558cca95a3c7af5b8" ||
            locks.AiContractDigest != "471fdcb92780bac8f552366c988eab7d4f7498a123e1625aacc1dd2ecaf49af5" ||
            locks.PriorityPolicyVersion != "synthetic-priority-policy-v1") return CsvIssue.VersionMismatch;
        if (new[] { versions.RunInputDigest, frozenVersions.ScriptedResultsDigest, frozenVersions.AnalysisFixtureDigest,
            frozenVersions.MaturityFixtureDigest, frozenVersions.FixPackageTemplateDigest, frozenVersions.FixReviewContractDigest,
            frozenVersions.PlanningTaskContractDigest, locks.OutcomeLockDigest, locks.AiFixtureDigest, locks.AiMappingDigest,
            locks.AiPacketDigest, source.SourceDigest, source.GuidanceDigest, source.FindingReviewDigest }.Any(value => !ValidDigest(value)) ||
            new[] { frozenVersions.DesiredOutcomeVersion, frozenVersions.AiPolicyVersion, frozenVersions.PromptVersion,
                frozenVersions.ModelVersion, frozenVersions.WorkSchemaVersion, locks.SchemaVersion, locks.FixtureEpoch }.Any(value => string.IsNullOrWhiteSpace(value) || !ValidCell(value)) ||
            source.RunRevision < 0 || source.RunRevision > MaximumRevision || source.FindingRevisions.IsDefault) return CsvIssue.InvalidInput;
        if (source.FindingRevisions.Any(item => item is null || !ValidDigest(item.FindingId) || item.Revision < 0 || item.Revision > MaximumRevision) || !source.FindingRevisions.Select(item => item.FindingId).SequenceEqual(source.FindingRevisions.Select(item => item.FindingId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))) return CsvIssue.InvalidInput;
        if (envelope.Rows.Length > MaximumRows || envelope.SelectedAttestations.Length > MaximumRows * 3) return CsvIssue.LimitExceeded;
        if (requireDigest && !ValidDigest(envelope.SnapshotDigest)) return CsvIssue.IntegrityMismatch;
        if (!envelope.Rows.Select(row => row.TaskId).SequenceEqual(envelope.Rows.Select(row => row.TaskId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))) return CsvIssue.InvalidInput;
        foreach (var row in envelope.Rows)
        {
            if (row is null || row.CsvContractVersion != ContractVersion || row.RunId != envelope.RunId.ToString("D") || new[] { row.TaskId, row.FindingId, row.PackageId, row.ScopedOptionId, row.CurrentSourceDigest, row.PlannedSourceDigest }.Any(value => !ValidDigest(value)) || row.CurrentSourceDigest != source.SourceDigest || row.TaskRevision < 1 || row.TaskRevision > MaximumRevision || row.AssigneeId is not { Length: > 0 and <= 128 } || !row.AssigneeId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) return CsvIssue.InvalidInput;
            if (row.TaskStatus is not ("Planned" or "InProgress" or "Completed" or "Cancelled") || row.PlanFreshness is not ("CurrentPlan" or "NeedsReconfirmation") || !Timestamp(row.CreatedAtUtc) || !Timestamp(row.PlannedAtUtc)) return CsvIssue.InvalidInput;
            if (row.ExportSnapshotDigest != (requireDigest ? envelope.SnapshotDigest : "")) return CsvIssue.IntegrityMismatch;
            if (row.TaskLink != TaskLink(envelope.RunId, row.TaskId) || row.FindingLink != FindingLink(envelope.RunId, row.FindingId)) return CsvIssue.InvalidInput;
            if (new[] { row.TaskLink, row.FindingLink }.Any(link => CsvCanonical.Utf8.GetByteCount(link) > MaximumLinkBytes)) return CsvIssue.LimitExceeded;
            foreach (var cell in Cells(row)) if (!ValidCell(cell)) return CsvIssue.InvalidInput;
            var selected = envelope.SelectedAttestations.Where(item => item.TaskId == row.TaskId).ToArray();
            if (selected.Length != 3 || selected.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != 3) return CsvIssue.InvalidInput;
        }
        if (!envelope.SelectedAttestations.Select(item => (item.ArtifactId, item.TaskId)).SequenceEqual(envelope.SelectedAttestations.Select(item => (item.ArtifactId, item.TaskId)).Distinct().OrderBy(item => item.ArtifactId, StringComparer.Ordinal).ThenBy(item => item.TaskId, StringComparer.Ordinal))) return CsvIssue.InvalidInput;
        foreach (var item in envelope.SelectedAttestations)
        {
            if (item is null || !envelope.Rows.Any(row => row.TaskId == item.TaskId) || !ValidDigest(item.ArtifactId) || item.Revision < 0 || item.Revision > MaximumRevision || item.State is not ("Unverified" or "ReviewedForPlanning" or "NeedsReview")) return CsvIssue.InvalidInput;
            if (item.Revision == 0 ? item.EventId is not null || item.Kind is not null || item.SourceDigest is not null || item.State != "Unverified" : item.EventId is null || item.EventId == Guid.Empty || item.Kind is not ("ReviewForPlanning" or "WithdrawReview") || !ValidDigest(item.SourceDigest)) return CsvIssue.InvalidInput;
        }
        return null;
    }
    private static bool Timestamp(string? text) => DateTimeOffset.TryParseExact(text, "yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) && value.Offset == TimeSpan.Zero && Utc(value) == text;
    public static bool ValidCell(string? cell)
    {
        if (cell is null || cell.Any(char.IsControl)) return false;
        try { return CsvCanonical.Utf8.GetByteCount(cell) <= MaximumCellBytes; } catch (EncoderFallbackException) { return false; }
    }
    public static string QuoteCell(string cell)
    {
        if (!ValidCell(cell)) throw new ArgumentException("Invalid CSV cell.");
        var first = cell.SkipWhile(char.IsWhiteSpace).FirstOrDefault();
        if (first is '=' or '+' or '-' or '@') cell = "'" + cell;
        return "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
    public static ImmutableArray<string> Cells(CsvTaskRow row) => [row.CsvContractVersion, row.RunId, row.TaskId, row.FindingId, row.PackageId, row.ScopedOptionId, row.AssigneeId,
        row.TaskRevision.ToString(CultureInfo.InvariantCulture), row.TaskStatus, row.PlanFreshness, row.CreatedAtUtc, row.PlannedAtUtc, row.CurrentSourceDigest, row.PlannedSourceDigest,
        row.ExportSnapshotDigest, row.TaskLink, row.FindingLink];
    public static byte[] Render(CsvEnvelope envelope)
    {
        if (!Parse(CsvCanonical.Json(envelope)).Succeeded) throw new ArgumentException("Invalid frozen CSV envelope.");
        var text = new StringBuilder();
        void Row(IEnumerable<string> cells) { text.AppendJoin(',', cells.Select(QuoteCell)); text.Append("\r\n"); }
        Row(Headers); foreach (var row in envelope.Rows) Row(Cells(row));
        var bytes = CsvCanonical.Utf8.GetBytes(text.ToString());
        return bytes.Length > MaximumOutputBytes ? throw new ArgumentException("CSV output limit.") : bytes;
    }
    public static CsvVerificationResult Verify(CsvEnvelope envelope, ReadOnlySpan<byte> actual, Guid requestId)
    {
        try
        {
            if (requestId == Guid.Empty || actual.Length > MaximumOutputBytes) return new(CsvIssue.InvalidInput, null);
            var expected = Render(envelope);
            if (!actual.SequenceEqual(expected)) return new(CsvIssue.IntegrityMismatch, null);
            return new(null, new("synthetic-task-csv-render-receipt-v1", requestId, envelope.RunId, envelope.SnapshotDigest, CsvCanonical.Hash(actual), ContractVersion, envelope.Rows.Length, true));
        }
        catch (ArgumentException) { return new(CsvIssue.InvalidInput, null); }
    }
}
