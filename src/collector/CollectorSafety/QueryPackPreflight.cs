using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CollectorSafety;

public sealed record QueryPackField(string Name, string SqlType, bool Nullable, FieldClassification Classification)
{
    public override string ToString() => nameof(QueryPackField);
}

public sealed record QueryPackParameter(string Name, string SqlType, bool Nullable)
{
    public override string ToString() => nameof(QueryPackParameter);
}

public sealed record QueryPackPolicyBinding(string Id, string Version, string Sha256)
{
    public override string ToString() => nameof(QueryPackPolicyBinding);
}

public sealed record QueryPackDescriptor(
    Guid PackId, string PackVersion, string QueryId, string QueryVersion, string Sql, string SqlSha256,
    QueryApplicabilityRule Applicability, string Schema, string Table, string Category,
    IReadOnlyCollection<QueryPackField> Fields, QueryPackPolicyBinding Policy,
    string MinimumReadSetId, string MinimumReadSetVersion,
    string StableKey, string KeyUniquenessReviewReference,
    string PageSizeParameter, string BoundaryParameter, IReadOnlyCollection<QueryPackParameter> Parameters,
    int MaximumPageSize, long MaximumRows, TimeSpan MaximumDuration, string QueryReviewReference)
{
    public override string ToString() => nameof(QueryPackDescriptor);
}

/// <summary>Trusted caller bindings, not a source-access grant or signature assertion.</summary>
public sealed record QueryPackExpectedBindings(
    Guid PackId, string PackVersion, string QueryId, string QueryVersion, string SqlSha256,
    SourceBuildClaim Source, FieldPolicySnapshot Policy, string MinimumReadSetId, string MinimumReadSetVersion)
{
    public override string ToString() => nameof(QueryPackExpectedBindings);
}

public enum QueryPackPreflightDecision
{
    InvalidInput, BindingMismatch, SqlDigestMismatch, UnsupportedSource, FieldRejected,
    ParameterRejected, BudgetRejected, UnsafeSql, StructurallyReady
}

public readonly record struct QueryPackPreflightResult(QueryPackPreflightDecision Decision)
{
    public bool StructurallyReady => Decision == QueryPackPreflightDecision.StructurallyReady;
}

/// <summary>
/// Pure protected-descriptor preflight. Success means structural/metadata readiness
/// only; no source read, signing trust, promotion, reviewed grant or G2 follows.
/// </summary>
public static class QueryPackPreflight
{
    public static QueryPackPreflightResult Evaluate(QueryPackDescriptor? candidate, QueryPackExpectedBindings? expected)
    {
        if (candidate is null || expected is null || candidate.Fields is null || candidate.Parameters is null ||
            candidate.Applicability is null || candidate.Policy is null || expected.Source is null ||
            expected.Source.InstalledModules is null || expected.Policy is null ||
            expected.Policy.IncludedFields is null || expected.Policy.ExcludedCategories is null ||
            candidate.Applicability.SupportedExactBuilds is null ||
            candidate.Applicability.SupportedExactModuleVersions is null)
        {
            return Result(QueryPackPreflightDecision.InvalidInput);
        }

        // Freeze caller-owned collections once; no later check observes their live state.
        var fields = candidate.Fields.ToArray();
        var parameters = candidate.Parameters.ToArray();
        var source = expected.Source with { InstalledModules = expected.Source.InstalledModules.ToArray() };
        var applicability = candidate.Applicability with
        {
            SupportedExactBuilds = candidate.Applicability.SupportedExactBuilds.ToArray(),
            SupportedExactModuleVersions = candidate.Applicability.SupportedExactModuleVersions.ToArray()
        };
        var policy = expected.Policy with
        {
            IncludedFields = expected.Policy.IncludedFields.ToHashSet(),
            ExcludedCategories = expected.Policy.ExcludedCategories.ToHashSet(StringComparer.Ordinal)
        };

        if (candidate.PackId == Guid.Empty || expected.PackId == Guid.Empty ||
            !SemanticVersion(candidate.PackVersion) || !SemanticVersion(expected.PackVersion) ||
            !Text(candidate.QueryId) || !Text(expected.QueryId) ||
            !SemanticVersion(candidate.QueryVersion) || !SemanticVersion(expected.QueryVersion) ||
            !Text(candidate.MinimumReadSetId) || !Text(expected.MinimumReadSetId) ||
            !Version(candidate.MinimumReadSetVersion) || !Version(expected.MinimumReadSetVersion) ||
            !Text(candidate.KeyUniquenessReviewReference) || !Text(candidate.QueryReviewReference) ||
            !StrictKeysetSqlValidator.Identifier(candidate.Schema) || !StrictKeysetSqlValidator.Identifier(candidate.Table) ||
            !StrictKeysetSqlValidator.Identifier(candidate.StableKey) || !Text(candidate.Category) ||
            !Text(candidate.Policy.Id) || !Version(candidate.Policy.Version) || !Digest(candidate.Policy.Sha256) ||
            !Text(policy.PolicyId) || !Version(policy.Version) || !Digest(policy.Sha256) ||
            !Digest(candidate.SqlSha256) || !Digest(expected.SqlSha256) || string.IsNullOrWhiteSpace(candidate.Sql) ||
            policy.IncludedFields.Any(field => field is null || !Text(field.CategoryId) || !Text(field.FieldId)) ||
            policy.ExcludedCategories.Any(category => !Text(category)) ||
            !ValidApplicability(source, applicability))
        {
            return Result(QueryPackPreflightDecision.InvalidInput);
        }

        if (candidate.PackId != expected.PackId || !Equal(candidate.PackVersion, expected.PackVersion) ||
            !Equal(candidate.QueryId, expected.QueryId) || !Equal(candidate.QueryVersion, expected.QueryVersion) ||
            !Equal(candidate.QueryId, applicability.QueryId) ||
            !Equal(candidate.Policy.Id, policy.PolicyId) || !Equal(candidate.Policy.Version, policy.Version) ||
            !EqualDigest(candidate.Policy.Sha256, policy.Sha256) ||
            !Equal(candidate.MinimumReadSetId, expected.MinimumReadSetId) ||
            !Equal(candidate.MinimumReadSetVersion, expected.MinimumReadSetVersion))
        {
            return Result(QueryPackPreflightDecision.BindingMismatch);
        }

        var actualDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.Sql)));
        if (!EqualDigest(candidate.SqlSha256, expected.SqlSha256) || !EqualDigest(candidate.SqlSha256, actualDigest))
        {
            return Result(QueryPackPreflightDecision.SqlDigestMismatch);
        }
        if (QueryApplicability.Evaluate(source, applicability) != QueryApplicabilityDecision.Compatible)
        {
            return Result(QueryPackPreflightDecision.UnsupportedSource);
        }

        if (fields.Length == 0 || fields.Any(field => field is null ||
            !StrictKeysetSqlValidator.Identifier(field.Name) || !SqlType(field.SqlType) ||
            field.Classification != FieldClassification.ApprovedReference) ||
            fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Length ||
            fields.Any(field => FieldMinimizer.Evaluate(policy,
                new FieldCandidate(new FieldKey(candidate.Category, field.Name), field.Classification, null)).Disposition
                    != FieldDisposition.Included))
        {
            return Result(QueryPackPreflightDecision.FieldRejected);
        }
        var key = fields.SingleOrDefault(field => Equal(field.Name, candidate.StableKey));
        if (key is null || key.Nullable)
        {
            return Result(QueryPackPreflightDecision.FieldRejected);
        }

        if (!StrictKeysetSqlValidator.Parameter(candidate.PageSizeParameter) ||
            !StrictKeysetSqlValidator.Parameter(candidate.BoundaryParameter) ||
            string.Equals(candidate.PageSizeParameter, candidate.BoundaryParameter, StringComparison.OrdinalIgnoreCase) || parameters.Length != 2 ||
            parameters.Any(parameter => parameter is null || !StrictKeysetSqlValidator.Parameter(parameter.Name) ||
                parameter.Nullable || !SqlType(parameter.SqlType)) ||
            parameters.Select(parameter => parameter.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2 ||
            !parameters.Any(parameter => Equal(parameter.Name, candidate.PageSizeParameter) && parameter.SqlType == "int") ||
            !parameters.Any(parameter => Equal(parameter.Name, candidate.BoundaryParameter) && Equal(parameter.SqlType, key.SqlType)))
        {
            return Result(QueryPackPreflightDecision.ParameterRejected);
        }
        if (candidate.MaximumPageSize <= 0 || candidate.MaximumRows < candidate.MaximumPageSize ||
            candidate.MaximumDuration <= TimeSpan.Zero)
        {
            return Result(QueryPackPreflightDecision.BudgetRejected);
        }

        var sqlDecision = StrictKeysetSqlValidator.Evaluate(candidate.Sql,
            new StrictKeysetSqlPolicy(candidate.Schema, candidate.Table, fields.Select(field => field.Name).ToArray(),
                candidate.StableKey, candidate.PageSizeParameter, candidate.BoundaryParameter));
        return Result(sqlDecision == StrictKeysetSqlDecision.StructurallyReady
            ? QueryPackPreflightDecision.StructurallyReady : QueryPackPreflightDecision.UnsafeSql);
    }

    private static bool ValidApplicability(SourceBuildClaim source, QueryApplicabilityRule rule) =>
        Version(source.ExactBuild) && source.InstalledModules.All(module => module is not null &&
            Text(module.ModuleId) && Version(module.ExactVersion)) &&
        source.InstalledModules.Select(module => module.ModuleId).Distinct(StringComparer.Ordinal).Count() == source.InstalledModules.Count &&
        Text(rule.QueryId) && rule.SupportedExactBuilds.Count > 0 && rule.SupportedExactBuilds.All(Version) &&
        rule.SupportedExactBuilds.Distinct(StringComparer.Ordinal).Count() == rule.SupportedExactBuilds.Count &&
        rule.SupportedExactModuleVersions.All(Version) &&
        rule.SupportedExactModuleVersions.Distinct(StringComparer.Ordinal).Count() == rule.SupportedExactModuleVersions.Count &&
        (rule.ModuleId is null ? rule.SupportedExactModuleVersions.Count == 0 : Text(rule.ModuleId) && rule.SupportedExactModuleVersions.Count > 0);

    private static bool SqlType(string? value)
    {
        if (value is "int" or "bigint" or "smallint" or "tinyint" or "uniqueidentifier") return true;
        if (value is not { Length: > 0 and <= 20 } || value[^1] != ')') return false;
        var opening = value.IndexOf('(');
        if (opening <= 0) return false;
        var family = value[..opening];
        var limit = family == "nvarchar" ? 4000 : family is "varchar" or "binary" or "varbinary" ? 8000 : 0;
        var widthText = value[(opening + 1)..^1];
        return widthText.Length > 0 && widthText.All(char.IsAsciiDigit) &&
            int.TryParse(widthText, NumberStyles.None, CultureInfo.InvariantCulture, out var width) &&
            width > 0 && width <= limit && width.ToString(CultureInfo.InvariantCulture) == widthText;
    }

    private static bool Text(string? value) => value is { Length: > 0 and <= 128 } &&
        !string.IsNullOrWhiteSpace(value) && value == value.Trim() && !value.Any(char.IsControl);
    private static bool Version(string? value) => Text(value) && !value!.Any(char.IsWhiteSpace) &&
        !value!.Contains('*') && !value.Contains('?') &&
        value.Split('.').All(part => part.Length > 0 && !string.Equals(part, "x", StringComparison.OrdinalIgnoreCase));
    private static bool SemanticVersion(string? value) => Version(value) && value!.Split('.') is { Length: 3 } parts &&
        parts.All(part => part.All(char.IsAsciiDigit) && (part.Length == 1 || part[0] != '0'));
    private static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static bool Equal(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);
    private static bool EqualDigest(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static QueryPackPreflightResult Result(QueryPackPreflightDecision decision) => new(decision);
}
