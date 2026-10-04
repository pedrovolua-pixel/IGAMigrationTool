using System.Collections.Frozen;
using CollectorSafety;

namespace CollectorSourcePages;

public sealed class SourceScope(Guid customerId, Guid projectId, Guid environmentId, Guid extractionId)
{
    public Guid CustomerId { get; } = customerId;
    public Guid ProjectId { get; } = projectId;
    public Guid EnvironmentId { get; } = environmentId;
    public Guid ExtractionId { get; } = extractionId;
    internal bool Valid => CustomerId != Guid.Empty && ProjectId != Guid.Empty && EnvironmentId != Guid.Empty && ExtractionId != Guid.Empty;
    internal bool SameAs(SourceScope other) => CustomerId == other.CustomerId && ProjectId == other.ProjectId && EnvironmentId == other.EnvironmentId && ExtractionId == other.ExtractionId;
    public override string ToString() => nameof(SourceScope);
}

public sealed class SourceQueryPair
{
    public SourceQueryPair(Guid pairId, QueryPackDescriptor continuation, string firstSql, string firstSqlSha256,
        int localPackRevision, int localPolicyRevision, string? approvedUidField, string schemaVersion,
        string normalizationVersion, string repeatabilityReviewReference)
    {
        PairId = pairId;
        Descriptor = Freeze.Descriptor(continuation);
        FirstSql = firstSql;
        FirstSqlSha256 = firstSqlSha256;
        LocalPackRevision = localPackRevision;
        LocalPolicyRevision = localPolicyRevision;
        ApprovedUidField = approvedUidField;
        SchemaVersion = schemaVersion;
        NormalizationVersion = normalizationVersion;
        RepeatabilityReviewReference = repeatabilityReviewReference;
    }
    public Guid PairId { get; }
    internal QueryPackDescriptor Descriptor { get; }
    public string FirstSql { get; }
    public string FirstSqlSha256 { get; }
    public int LocalPackRevision { get; }
    public int LocalPolicyRevision { get; }
    public string? ApprovedUidField { get; }
    public string SchemaVersion { get; }
    public string NormalizationVersion { get; }
    public string RepeatabilityReviewReference { get; }
    public IReadOnlyList<QueryPackField> Fields => (IReadOnlyList<QueryPackField>)Descriptor.Fields;
    internal bool SameAs(SourceQueryPair other) => PairId == other.PairId && FirstSql == other.FirstSql &&
        FirstSqlSha256 == other.FirstSqlSha256 && LocalPackRevision == other.LocalPackRevision &&
        LocalPolicyRevision == other.LocalPolicyRevision && ApprovedUidField == other.ApprovedUidField &&
        SchemaVersion == other.SchemaVersion && NormalizationVersion == other.NormalizationVersion &&
        RepeatabilityReviewReference == other.RepeatabilityReviewReference && Freeze.Same(Descriptor, other.Descriptor);
    public override string ToString() => nameof(SourceQueryPair);
}

public enum SourceQueryPhase { First, Continuation }
public sealed class SourcePageIdentity(SourceScope scope, Guid pairId, long pageOrdinal)
{
    public SourceScope Scope { get; } = scope;
    public Guid PairId { get; } = pairId;
    public long PageOrdinal { get; } = pageOrdinal;
    internal bool SameAs(SourcePageIdentity other) => Scope.SameAs(other.Scope) && PairId == other.PairId && PageOrdinal == other.PageOrdinal;
    public override string ToString() => nameof(SourcePageIdentity);
}

public sealed class SourcePageLimits(int maximumPageSize, long maximumRows, long maximumFieldBytes,
    long maximumPageBytes, long maximumTotalBytes, TimeSpan maximumDuration, TimeSpan commandTimeout, TimeSpan retention)
{
    public int MaximumPageSize { get; } = maximumPageSize;
    public long MaximumRows { get; } = maximumRows;
    public long MaximumFieldBytes { get; } = maximumFieldBytes;
    public long MaximumPageBytes { get; } = maximumPageBytes;
    public long MaximumTotalBytes { get; } = maximumTotalBytes;
    public TimeSpan MaximumDuration { get; } = maximumDuration;
    public TimeSpan CommandTimeout { get; } = commandTimeout;
    public TimeSpan Retention { get; } = retention;
    internal bool Valid => MaximumPageSize > 0 && MaximumRows >= MaximumPageSize && MaximumFieldBytes > 0 &&
        MaximumPageBytes >= MaximumFieldBytes && MaximumTotalBytes >= MaximumPageBytes &&
        MaximumDuration > TimeSpan.Zero && CommandTimeout > TimeSpan.Zero && CommandTimeout <= MaximumDuration &&
        Retention > TimeSpan.Zero && CommandTimeout.TotalMilliseconds <= uint.MaxValue - 1;
    internal bool Narrows(SourcePageLimits prior) => MaximumPageSize <= prior.MaximumPageSize && MaximumRows <= prior.MaximumRows &&
        MaximumFieldBytes <= prior.MaximumFieldBytes && MaximumPageBytes <= prior.MaximumPageBytes &&
        MaximumTotalBytes <= prior.MaximumTotalBytes && MaximumDuration <= prior.MaximumDuration &&
        CommandTimeout <= prior.CommandTimeout && Retention <= prior.Retention;
    public override string ToString() => nameof(SourcePageLimits);
}

public sealed class SourcePageRequest(SourceScope scope, SourceQueryPair pair, long pageOrdinal,
    SourceQueryPhase phase, int requestedPageSize, SourceNativeValue? continuation, SourcePageLimits limits)
{
    public SourceScope Scope { get; } = scope;
    public SourceQueryPair Pair { get; } = pair;
    public long PageOrdinal { get; } = pageOrdinal;
    public SourceQueryPhase Phase { get; } = phase;
    public int RequestedPageSize { get; } = requestedPageSize;
    public SourceNativeValue? Continuation { get; } = continuation;
    public SourcePageLimits Limits { get; } = limits;
    public SourcePageIdentity Identity => new(Scope, Pair.PairId, PageOrdinal);
    public override string ToString() => nameof(SourcePageRequest);
}

public enum SourceNativeKind { Integer, UniqueIdentifier, Text, Binary, SqlNull }
public sealed class SourceNativeValue
{
    private readonly byte[]? _binary;
    private SourceNativeValue(SourceNativeKind kind, string sqlType, long integer = 0, Guid uniqueIdentifier = default,
        string? text = null, int nativeByteLength = 0, byte[]? binary = null)
    {
        Kind = kind; SqlType = sqlType; IntegerValue = integer; UniqueIdentifierValue = uniqueIdentifier;
        TextValue = text; NativeByteLength = nativeByteLength; _binary = binary?.ToArray();
    }
    public SourceNativeKind Kind { get; }
    public string SqlType { get; }
    public long IntegerValue { get; }
    public Guid UniqueIdentifierValue { get; }
    public string? TextValue { get; }
    public int NativeByteLength { get; }
    public byte[]? BinaryValue => _binary?.ToArray();
    public static SourceNativeValue Integer(string sqlType, long value) => new(SourceNativeKind.Integer, sqlType, integer: value);
    public static SourceNativeValue UniqueIdentifier(Guid value) => new(SourceNativeKind.UniqueIdentifier, "uniqueidentifier", uniqueIdentifier: value);
    public static SourceNativeValue Text(string sqlType, string value, int nativeByteLength) => new(SourceNativeKind.Text, sqlType, text: value, nativeByteLength: nativeByteLength);
    public static SourceNativeValue Binary(string sqlType, byte[] value) => new(SourceNativeKind.Binary, sqlType, binary: value);
    public static SourceNativeValue SqlNull(string sqlType) => new(SourceNativeKind.SqlNull, sqlType);
    internal bool SameAs(SourceNativeValue other) => Kind == other.Kind && SqlType == other.SqlType &&
        IntegerValue == other.IntegerValue && UniqueIdentifierValue == other.UniqueIdentifierValue &&
        TextValue == other.TextValue && NativeByteLength == other.NativeByteLength &&
        (_binary is null ? other._binary is null : other._binary is not null && _binary.SequenceEqual(other._binary));
    internal bool TryBytes(QueryPackField field, out long bytes)
    {
        bytes = 0;
        if (SqlType != field.SqlType) return false;
        if (Kind == SourceNativeKind.SqlNull) return field.Nullable;
        if (Kind == SourceNativeKind.Integer)
        {
            bytes = SqlType switch { "tinyint" => 1, "smallint" => 2, "int" => 4, "bigint" => 8, _ => 0 };
            return SqlType switch
            {
                "tinyint" => IntegerValue is >= 0 and <= 255,
                "smallint" => IntegerValue is >= short.MinValue and <= short.MaxValue,
                "int" => IntegerValue is >= int.MinValue and <= int.MaxValue,
                "bigint" => true,
                _ => false
            };
        }
        if (Kind == SourceNativeKind.UniqueIdentifier) { bytes = 16; return SqlType == "uniqueidentifier"; }
        var opening = SqlType.IndexOf('(');
        if (opening <= 0 || !SqlType.EndsWith(')') || !int.TryParse(SqlType.AsSpan(opening + 1, SqlType.Length - opening - 2), out var width) || width <= 0) return false;
        var family = SqlType[..opening];
        if (Kind == SourceNativeKind.Binary && _binary is not null)
        {
            bytes = _binary.Length;
            return family == "binary" && bytes == width || family == "varbinary" && bytes <= width;
        }
        if (Kind != SourceNativeKind.Text || TextValue is null || NativeByteLength < 0) return false;
        try { _ = new System.Text.UTF8Encoding(false, true).GetByteCount(TextValue); }
        catch (System.Text.EncoderFallbackException) { return false; }
        bytes = NativeByteLength;
        return family == "varchar" && bytes >= TextValue.Length && bytes <= width && (TextValue.Length != 0 || bytes == 0) ||
            family == "nvarchar" && bytes == (long)TextValue.Length * 2 && bytes <= (long)width * 2;
    }
    public override string ToString() => nameof(SourceNativeValue);
}

public sealed class SourceColumnMetadata(string name, string sqlType, bool nullable)
{
    public string Name { get; } = name;
    public string SqlType { get; } = sqlType;
    public bool Nullable { get; } = nullable;
    public override string ToString() => nameof(SourceColumnMetadata);
}
public sealed class SourceReturnedSchema(IEnumerable<SourceColumnMetadata> fields)
{
    public IReadOnlyList<SourceColumnMetadata> Fields { get; } = Array.AsReadOnly(fields.ToArray());
    public override string ToString() => nameof(SourceReturnedSchema);
}
public sealed class SourceReturnedRow(long rowOrdinal, IEnumerable<SourceNativeValue> values)
{
    public long RowOrdinal { get; } = rowOrdinal;
    public IReadOnlyList<SourceNativeValue> Values { get; } = Array.AsReadOnly(values.ToArray());
    public override string ToString() => nameof(SourceReturnedRow);
}

public sealed class SourceProvenance
{
    internal SourceProvenance(SourcePageRequest request, SourceRegistryBinding binding, long rowOrdinal,
        int fieldOrdinal, DateTimeOffset extractedAtUtc, FieldDisposition disposition)
    {
        var pair = request.Pair; var descriptor = pair.Descriptor; var field = pair.Fields[fieldOrdinal];
        Scope = request.Scope; PairId = pair.PairId; PackId = descriptor.PackId; PackVersion = descriptor.PackVersion;
        QueryId = descriptor.QueryId; QueryVersion = descriptor.QueryVersion; Phase = request.Phase;
        PageOrdinal = request.PageOrdinal; RowOrdinal = rowOrdinal; FieldOrdinal = fieldOrdinal;
        Schema = descriptor.Schema; Table = descriptor.Table; Field = field.Name; SqlType = field.SqlType;
        PolicyId = descriptor.Policy.Id; PolicyVersion = descriptor.Policy.Version; SchemaVersion = pair.SchemaVersion;
        NormalizationVersion = pair.NormalizationVersion; ExactBuild = binding.Expected.Source.ExactBuild;
        InstalledModules = Array.AsReadOnly(binding.Expected.Source.InstalledModules.Select(m => new SourceModuleVersion(m.ModuleId, m.ExactVersion)).ToArray());
        ExtractedAtUtc = extractedAtUtc; FieldDisposition = disposition;
    }
    public SourceScope Scope { get; }
    public Guid PairId { get; }
    public Guid PackId { get; }
    public string PackVersion { get; }
    public string QueryId { get; }
    public string QueryVersion { get; }
    public SourceQueryPhase Phase { get; }
    public long PageOrdinal { get; }
    public long RowOrdinal { get; }
    public int FieldOrdinal { get; }
    public string Schema { get; }
    public string Table { get; }
    public string Field { get; }
    public string SqlType { get; }
    public string PolicyId { get; }
    public string PolicyVersion { get; }
    public string SchemaVersion { get; }
    public string NormalizationVersion { get; }
    public string ExactBuild { get; }
    public IReadOnlyList<SourceModuleVersion> InstalledModules { get; }
    public DateTimeOffset ExtractedAtUtc { get; }
    public FieldDisposition FieldDisposition { get; }
    public override string ToString() => nameof(SourceProvenance);
}
public sealed class SourceModuleVersion(string moduleId, string exactVersion)
{
    public string ModuleId { get; } = moduleId;
    public string ExactVersion { get; } = exactVersion;
    public override string ToString() => nameof(SourceModuleVersion);
}
public sealed class SourceMinimizedField
{
    internal SourceMinimizedField(SourceProvenance provenance, FieldDisposition disposition, SourceNativeValue? value)
    { Provenance = provenance; Disposition = disposition; Value = disposition == FieldDisposition.Included ? value : null; }
    public SourceProvenance Provenance { get; }
    public FieldDisposition Disposition { get; }
    public SourceNativeValue? Value { get; }
    public override string ToString() => nameof(SourceMinimizedField);
}
public sealed class SourceMinimizedRow
{
    internal SourceMinimizedRow(long rowOrdinal, IEnumerable<SourceMinimizedField> fields)
    { RowOrdinal = rowOrdinal; Fields = Array.AsReadOnly(fields.ToArray()); }
    public long RowOrdinal { get; }
    public IReadOnlyList<SourceMinimizedField> Fields { get; }
    public override string ToString() => nameof(SourceMinimizedRow);
}
public sealed class SourceRowReference(SourcePageIdentity page, long rowOrdinal)
{
    public SourcePageIdentity Page { get; } = page;
    public long RowOrdinal { get; } = rowOrdinal;
    public override string ToString() => nameof(SourceRowReference);
}
public enum SourceConflictKind { PagingKeyTie, PagingOrder, ObjectUid }
public sealed class SourceConflict(SourceConflictKind kind, SourceRowReference first, SourceRowReference second)
{
    public SourceConflictKind Kind { get; } = kind;
    public SourceRowReference First { get; } = first;
    public SourceRowReference Second { get; } = second;
    public override string ToString() => nameof(SourceConflict);
}
public sealed class SourcePage
{
    internal SourcePage(SourcePageRequest request, IEnumerable<SourceMinimizedRow> rows, IEnumerable<SourceConflict> conflicts,
        bool terminal, SourceNativeValue? nextContinuation, long observedBytes, DateTimeOffset extractedAtUtc)
    {
        Identity = request.Identity; Phase = request.Phase; RequestedPageSize = request.RequestedPageSize;
        Rows = Array.AsReadOnly(rows.ToArray()); Conflicts = Array.AsReadOnly(conflicts.ToArray()); Terminal = terminal;
        NextContinuation = nextContinuation; ObservedBytes = observedBytes; ExtractedAtUtc = extractedAtUtc;
    }
    public SourcePageIdentity Identity { get; }
    public SourceQueryPhase Phase { get; }
    public int RequestedPageSize { get; }
    public IReadOnlyList<SourceMinimizedRow> Rows { get; }
    public IReadOnlyList<SourceConflict> Conflicts { get; }
    public bool Terminal { get; }
    public SourceNativeValue? NextContinuation { get; }
    public long ObservedBytes { get; }
    public DateTimeOffset ExtractedAtUtc { get; }
    public override string ToString() => nameof(SourcePage);
}
public sealed class SourcePageReceipt
{
    internal SourcePageReceipt(SourcePageRequest request, SourceRegistryBinding binding, SourcePage page,
        DateTimeOffset originalStartedAtUtc, long cumulativeRows, long cumulativeBytes, bool rowCap)
    {
        Identity = page.Identity; Pair = request.Pair; Binding = binding; Limits = request.Limits;
        NextContinuation = page.NextContinuation; Terminal = page.Terminal; RowCap = rowCap;
        LastRowOrdinal = page.Rows.Count - 1; OriginalStartedAtUtc = originalStartedAtUtc;
        CumulativeRows = cumulativeRows; CumulativeBytes = cumulativeBytes;
    }
    public SourcePageIdentity Identity { get; }
    public SourceQueryPair Pair { get; }
    public SourceRegistryBinding Binding { get; }
    public SourcePageLimits Limits { get; }
    public SourceNativeValue? NextContinuation { get; }
    public bool Terminal { get; }
    public bool RowCap { get; }
    public long LastRowOrdinal { get; }
    public DateTimeOffset OriginalStartedAtUtc { get; }
    public long CumulativeRows { get; }
    public long CumulativeBytes { get; }
    public override string ToString() => nameof(SourcePageReceipt);
}
public enum SourcePageOutcome { PageReady, Partial, Refused, Quarantined, Canceled, TimedOut, Expired, Disconnected }
public enum SourcePageReason
{
    None, InvalidInput, ConcurrentCall, AuthorityMissing, AuthorityRevoked, RegistryMismatch, InvalidQueryPair,
    HistoryMismatch, PermissionBlocked, WarningAuditMissing, ImpactUnknown, ImpactStopped, RowCap, FieldByteCap,
    PageByteCap, TotalByteCap, SchemaMismatch, NativeValueInvalid, NativeOrderUnsupported, PagingKeyConflict,
    PagingOrderViolation, ClassifiedContent, TransportFailure, PortFailure, Deadline, Retention
}
public sealed class SourcePlannedGap(int fieldOrdinal, FieldDisposition disposition)
{
    public int FieldOrdinal { get; } = fieldOrdinal;
    public FieldDisposition Disposition { get; } = disposition;
    public override string ToString() => nameof(SourcePlannedGap);
}
public readonly record struct SourcePageCounters(long AuthorityResolutions, long AuthorityRevalidations, long HistoryLoads,
    long ConnectionOpens, long PermissionProbes, long WarningAudits, long ImpactChecks, long Executions,
    long ReadCalls, long RowsObserved, long BytesObserved, long ValueClassifications);
public sealed class SourcePageResult
{
    internal SourcePageResult(SourcePageOutcome outcome, SourcePageReason reason, SourcePageCounters counters,
        SourcePage? page = null, SourcePageReceipt? receipt = null, IEnumerable<SourceConflict>? conflicts = null,
        IEnumerable<SourcePlannedGap>? gaps = null)
    {
        Outcome = outcome; Reason = reason; Counters = counters; Page = page; Receipt = receipt;
        Conflicts = Array.AsReadOnly((conflicts ?? []).ToArray()); PlannedGaps = Array.AsReadOnly((gaps ?? []).ToArray());
    }
    public SourcePageOutcome Outcome { get; }
    public SourcePageReason Reason { get; }
    public SourcePageCounters Counters { get; }
    public SourcePage? Page { get; }
    public SourcePageReceipt? Receipt { get; }
    public IReadOnlyList<SourceConflict> Conflicts { get; }
    public IReadOnlyList<SourcePlannedGap> PlannedGaps { get; }
    public override string ToString() => nameof(SourcePageResult);
}

internal static class Freeze
{
    internal static QueryPackDescriptor Descriptor(QueryPackDescriptor value) => value with
    {
        Fields = Array.AsReadOnly(value.Fields.ToArray()),
        Parameters = Array.AsReadOnly(value.Parameters.ToArray()),
        Applicability = value.Applicability with
        { SupportedExactBuilds = Array.AsReadOnly(value.Applicability.SupportedExactBuilds.ToArray()), SupportedExactModuleVersions = Array.AsReadOnly(value.Applicability.SupportedExactModuleVersions.ToArray()) }
    };
    internal static QueryPackExpectedBindings Expected(QueryPackExpectedBindings value) => value with
    {
        Source = value.Source with { InstalledModules = Array.AsReadOnly(value.Source.InstalledModules.ToArray()) },
        Policy = value.Policy with
        { IncludedFields = value.Policy.IncludedFields.ToFrozenSet(), ExcludedCategories = value.Policy.ExcludedCategories.ToFrozenSet(StringComparer.Ordinal) }
    };
    internal static bool Same(QueryPackDescriptor a, QueryPackDescriptor b) =>
        a with { Fields = b.Fields, Parameters = b.Parameters, Applicability = b.Applicability } == b &&
        a.Fields.SequenceEqual(b.Fields) && a.Parameters.SequenceEqual(b.Parameters) &&
        a.Applicability with { SupportedExactBuilds = b.Applicability.SupportedExactBuilds, SupportedExactModuleVersions = b.Applicability.SupportedExactModuleVersions } == b.Applicability &&
        a.Applicability.SupportedExactBuilds.SequenceEqual(b.Applicability.SupportedExactBuilds) &&
        a.Applicability.SupportedExactModuleVersions.SequenceEqual(b.Applicability.SupportedExactModuleVersions);
    internal static bool Same(QueryPackExpectedBindings a, QueryPackExpectedBindings b) =>
        a with { Source = b.Source, Policy = b.Policy } == b && a.Source.ExactBuild == b.Source.ExactBuild &&
        a.Source.InstalledModules.SequenceEqual(b.Source.InstalledModules) &&
        a.Policy.PolicyId == b.Policy.PolicyId && a.Policy.Version == b.Policy.Version && a.Policy.Sha256 == b.Policy.Sha256 &&
        a.Policy.IncludedFields.SetEquals(b.Policy.IncludedFields) && a.Policy.ExcludedCategories.SetEquals(b.Policy.ExcludedCategories);
}
