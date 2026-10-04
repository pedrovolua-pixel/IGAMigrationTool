using CollectorSafety;

namespace CollectorSourcePages;

public enum SourceAuthorityState { Current, Missing, Revoked, Drifted, Unsupported }
public sealed class SourceAuthorityResolution(SourceAuthorityState state, SourceRegistryBinding? binding)
{
    public SourceAuthorityState State { get; } = state;
    public SourceRegistryBinding? Binding { get; } = binding;
    public override string ToString() => nameof(SourceAuthorityResolution);
}
public sealed class SourceRegistryBinding
{
    public SourceRegistryBinding(SourceScope scope, Guid pairId, QueryPackExpectedBindings expected,
        string trustedFirstSqlSha256, int trustedLocalPackRevision, int trustedLocalPolicyRevision,
        string? trustedUidField, string trustedSchemaVersion, string trustedNormalizationVersion,
        string trustedRepeatabilityReviewReference)
    {
        Scope = scope; PairId = pairId; Expected = Freeze.Expected(expected); FirstSqlSha256 = trustedFirstSqlSha256;
        LocalPackRevision = trustedLocalPackRevision; LocalPolicyRevision = trustedLocalPolicyRevision;
        UidField = trustedUidField; SchemaVersion = trustedSchemaVersion; NormalizationVersion = trustedNormalizationVersion;
        RepeatabilityReviewReference = trustedRepeatabilityReviewReference;
    }
    public SourceScope Scope { get; }
    public Guid PairId { get; }
    internal QueryPackExpectedBindings Expected { get; }
    public string FirstSqlSha256 { get; }
    public int LocalPackRevision { get; }
    public int LocalPolicyRevision { get; }
    public string? UidField { get; }
    public string SchemaVersion { get; }
    public string NormalizationVersion { get; }
    public string RepeatabilityReviewReference { get; }
    internal bool SameAs(SourceRegistryBinding other) => Scope.SameAs(other.Scope) && PairId == other.PairId &&
        FirstSqlSha256 == other.FirstSqlSha256 && LocalPackRevision == other.LocalPackRevision &&
        LocalPolicyRevision == other.LocalPolicyRevision && UidField == other.UidField && SchemaVersion == other.SchemaVersion &&
        NormalizationVersion == other.NormalizationVersion && RepeatabilityReviewReference == other.RepeatabilityReviewReference &&
        Freeze.Same(Expected, other.Expected);
    public override string ToString() => nameof(SourceRegistryBinding);
}
public enum SourceHistoryState { Initial, Previous, Unavailable }
public sealed class SourceHistoryResolution(SourceHistoryState state, DateTimeOffset originalStartedAtUtc, SourcePageReceipt? previous)
{
    public SourceHistoryState State { get; } = state;
    public DateTimeOffset OriginalStartedAtUtc { get; } = originalStartedAtUtc;
    public SourcePageReceipt? Previous { get; } = previous;
    public override string ToString() => nameof(SourceHistoryResolution);
}
public sealed class SourcePermissionReceipt(SourceScope scope, Guid pairId, Guid generation,
    string minimumReadSetId, string minimumReadSetVersion, PermissionProbe probe)
{
    public SourceScope Scope { get; } = scope;
    public Guid PairId { get; } = pairId;
    public Guid Generation { get; } = generation;
    public string MinimumReadSetId { get; } = minimumReadSetId;
    public string MinimumReadSetVersion { get; } = minimumReadSetVersion;
    internal PermissionProbe Probe { get; } = probe with { ObservedCapabilities = Array.AsReadOnly(probe.ObservedCapabilities.ToArray()) };
    public override string ToString() => nameof(SourcePermissionReceipt);
}
public sealed class SourceWarningIdentity(SourceScope scope, Guid pairId, long pageOrdinal, Guid generation,
    string minimumReadSetId, string minimumReadSetVersion)
{
    public SourceScope Scope { get; } = scope;
    public Guid PairId { get; } = pairId;
    public long PageOrdinal { get; } = pageOrdinal;
    public Guid Generation { get; } = generation;
    public string MinimumReadSetId { get; } = minimumReadSetId;
    public string MinimumReadSetVersion { get; } = minimumReadSetVersion;
    internal bool SameAs(SourceWarningIdentity other) => Scope.SameAs(other.Scope) && PairId == other.PairId &&
        PageOrdinal == other.PageOrdinal && Generation == other.Generation && MinimumReadSetId == other.MinimumReadSetId &&
        MinimumReadSetVersion == other.MinimumReadSetVersion;
    public override string ToString() => nameof(SourceWarningIdentity);
}
public sealed class SourceWarningReceipt(SourceWarningIdentity identity)
{
    public SourceWarningIdentity Identity { get; } = identity;
    public override string ToString() => nameof(SourceWarningReceipt);
}
public enum SourceImpactState { Continue, Unknown, Stop }
public enum SourceNativePurpose { Paging, ObjectIdentity }
public enum SourceNativeOrder { Less, Equal, Greater, Unsupported }
public sealed class SourceNativeComparisonReceipt(SourceRegistryBinding binding, SourceNativePurpose purpose,
    int fieldOrdinal, SourceNativeOrder order)
{
    public SourceRegistryBinding Binding { get; } = binding;
    public SourceNativePurpose Purpose { get; } = purpose;
    public int FieldOrdinal { get; } = fieldOrdinal;
    public SourceNativeOrder Order { get; } = order;
    public override string ToString() => nameof(SourceNativeComparisonReceipt);
}
public sealed class SourceValueClassificationReceipt(SourceRegistryBinding binding, int fieldOrdinal, FieldDisposition disposition)
{
    public SourceRegistryBinding Binding { get; } = binding;
    public int FieldOrdinal { get; } = fieldOrdinal;
    public FieldDisposition Disposition { get; } = disposition;
    public override string ToString() => nameof(SourceValueClassificationReceipt);
}
public sealed class SourceBoundParameter(string name, SourceNativeValue value)
{
    public string Name { get; } = name;
    public SourceNativeValue Value { get; } = value;
    public override string ToString() => nameof(SourceBoundParameter);
}
public sealed class SourceBoundCommand
{
    internal SourceBoundCommand(SourcePageRequest request, Guid generation, TimeSpan timeout)
    {
        Sql = request.Phase == SourceQueryPhase.First ? request.Pair.FirstSql : request.Pair.Descriptor.Sql;
        Phase = request.Phase; Identity = request.Identity; Generation = generation; Timeout = timeout;
        PageSize = new(request.Pair.Descriptor.PageSizeParameter, SourceNativeValue.Integer("int", request.RequestedPageSize));
        Boundary = request.Continuation is null ? null : new(request.Pair.Descriptor.BoundaryParameter, request.Continuation);
    }
    public string Sql { get; }
    public SourceQueryPhase Phase { get; }
    public SourcePageIdentity Identity { get; }
    public Guid Generation { get; }
    public TimeSpan Timeout { get; }
    public SourceBoundParameter PageSize { get; }
    public SourceBoundParameter? Boundary { get; }
    public override string ToString() => nameof(SourceBoundCommand);
}
public interface ITrustedSourceAuthority
{
    ValueTask<SourceAuthorityResolution> ResolveAsync(SourcePageRequest request, CancellationToken cancellationToken);
    ValueTask<SourceAuthorityState> RevalidateAsync(SourceRegistryBinding binding, Guid connectionGeneration, CancellationToken cancellationToken);
}
public interface ITrustedPageHistory
{
    ValueTask<SourceHistoryResolution> LoadAsync(SourcePageIdentity identity, CancellationToken cancellationToken);
}
public interface ISourcePageTransport
{
    ValueTask<ISourcePageConnection> OpenAsync(SourceRegistryBinding binding, CancellationToken cancellationToken);
}
public interface ISourcePageConnection : IAsyncDisposable
{
    Guid Generation { get; }
    ValueTask<ISourcePageReader> ExecuteAsync(SourceBoundCommand command, CancellationToken cancellationToken);
}
public interface ISourcePageReader : IAsyncDisposable
{
    SourceReturnedSchema Schema { get; }
    ValueTask<SourceReturnedRow?> ReadAsync(CancellationToken cancellationToken);
}
public interface ITrustedConnectionPermission
{
    ValueTask<SourcePermissionReceipt> ProbeAsync(SourceRegistryBinding binding, Guid generation, CancellationToken cancellationToken);
}
public interface IWarningAuditReceiptWriter
{
    ValueTask<SourceWarningReceipt?> CommitAsync(SourceWarningIdentity identity, CancellationToken cancellationToken);
}
public interface ITrustedImpactGate
{
    ValueTask<SourceImpactState> CheckAsync(SourceRegistryBinding binding, Guid generation, CancellationToken cancellationToken);
}
public interface INativeKeySemantics
{
    SourceNativeComparisonReceipt Compare(SourceRegistryBinding binding, SourceNativePurpose purpose, int fieldOrdinal,
        QueryPackField field, SourceNativeValue left, SourceNativeValue right);
}
public interface ITrustedReturnedValuePolicy
{
    SourceValueClassificationReceipt Classify(SourceRegistryBinding binding, int fieldOrdinal, QueryPackField field, SourceNativeValue value);
}
