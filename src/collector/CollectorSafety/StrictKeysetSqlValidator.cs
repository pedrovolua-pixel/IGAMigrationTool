using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text;

namespace CollectorSafety;

public sealed record StrictKeysetSqlPolicy(
    string Schema, string Table, IReadOnlyCollection<string> ProjectedColumns,
    string StableKey, string PageSizeParameter, string BoundaryParameter)
{
    public override string ToString() => nameof(StrictKeysetSqlPolicy);
}

public enum StrictKeysetSqlDecision { InvalidInput, UnsafeSql, StructurallyReady }

/// <summary>
/// Pure structural preflight for the strict v1 single-key page family. This does
/// not establish key uniqueness, source authority, promotion or safe SQL impact.
/// </summary>
public static class StrictKeysetSqlValidator
{
    public static StrictKeysetSqlDecision Evaluate(string? sql, StrictKeysetSqlPolicy? policy)
    {
        if (string.IsNullOrWhiteSpace(sql) || policy is null || policy.ProjectedColumns is null)
        {
            return StrictKeysetSqlDecision.InvalidInput;
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetByteCount(sql);
        }
        catch (EncoderFallbackException)
        {
            return StrictKeysetSqlDecision.InvalidInput;
        }

        var columns = policy.ProjectedColumns.ToArray();
        if (!Identifier(policy.Schema) || !Identifier(policy.Table) || !Identifier(policy.StableKey) ||
            columns.Length == 0 || columns.Any(column => !Identifier(column)) ||
            columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length ||
            !columns.Contains(policy.StableKey, StringComparer.Ordinal) ||
            !Parameter(policy.PageSizeParameter) || !Parameter(policy.BoundaryParameter) ||
            string.Equals(policy.PageSizeParameter, policy.BoundaryParameter, StringComparison.OrdinalIgnoreCase))
        {
            return StrictKeysetSqlDecision.InvalidInput;
        }

        var shape = new StaticSqlShapePolicy(policy.Schema, policy.Table, columns,
            policy.PageSizeParameter, policy.BoundaryParameter);
        if (StaticSqlShapeValidator.Evaluate(sql, shape) != StaticSqlShapeDecision.StructurallyReadOnlyPage)
        {
            return StrictKeysetSqlDecision.UnsafeSql;
        }

        var script = new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(sql), out var errors)
            as TSqlScript;
        if (errors.Count != 0 || script?.Batches.Count != 1 ||
            script.Batches[0].Statements.Count != 1 ||
            script.Batches[0].Statements[0] is not SelectStatement select ||
            select.QueryExpression is not QuerySpecification query ||
            query.UniqueRowFilter != UniqueRowFilter.NotSpecified ||
            query.FromClause?.TableReferences.SingleOrDefault() is not NamedTableReference table ||
            table.Alias is not null && !Identifier(table.Alias.Value))
        {
            return StrictKeysetSqlDecision.UnsafeSql;
        }

        var projected = new List<string>();
        foreach (var element in query.SelectElements)
        {
            if (element is not SelectScalarExpression { ColumnName: null, Expression: ColumnReferenceExpression column } ||
                !Column(column, table, out var name))
            {
                return StrictKeysetSqlDecision.UnsafeSql;
            }
            projected.Add(name!);
        }

        if (projected.Count != columns.Length ||
            projected.Distinct(StringComparer.Ordinal).Count() != columns.Length ||
            projected.Any(column => !columns.Contains(column, StringComparer.Ordinal)) ||
            Unwrap(query.WhereClause?.SearchCondition) is not BooleanComparisonExpression predicate ||
            predicate.ComparisonType != BooleanComparisonType.GreaterThan ||
            Unwrap(predicate.FirstExpression) is not ColumnReferenceExpression key ||
            !Column(key, table, out var predicateKey) ||
            !string.Equals(predicateKey, policy.StableKey, StringComparison.Ordinal) ||
            Unwrap(predicate.SecondExpression) is not VariableReference boundary ||
            !string.Equals(boundary.Name, policy.BoundaryParameter, StringComparison.Ordinal) ||
            query.OrderByClause?.OrderByElements.Count != 1)
        {
            return StrictKeysetSqlDecision.UnsafeSql;
        }

        var order = query.OrderByClause.OrderByElements[0];
        return order.SortOrder is SortOrder.NotSpecified or SortOrder.Ascending &&
            order.Expression is ColumnReferenceExpression orderColumn &&
            Column(orderColumn, table, out var orderKey) &&
            string.Equals(orderKey, policy.StableKey, StringComparison.Ordinal)
            ? StrictKeysetSqlDecision.StructurallyReady : StrictKeysetSqlDecision.UnsafeSql;
    }

    internal static bool Identifier(string? value) => value is { Length: > 0 and <= 128 } &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    internal static bool Parameter(string? value) => value is { Length: > 1 and <= 129 } &&
        value[0] == '@' && Identifier(value[1..]);

    private static bool Column(ColumnReferenceExpression column, NamedTableReference table, out string? name)
    {
        name = null;
        var parts = column.MultiPartIdentifier?.Identifiers;
        if (parts is null || parts.Count is < 1 or > 2 || !Identifier(parts[^1].Value) ||
            parts.Count == 2 && !string.Equals(parts[0].Value, table.SchemaObject.Identifiers[^1].Value, StringComparison.Ordinal) &&
            !string.Equals(parts[0].Value, table.Alias?.Value, StringComparison.Ordinal))
        {
            return false;
        }
        name = parts[^1].Value;
        return true;
    }

    private static BooleanExpression? Unwrap(BooleanExpression? expression) => expression is BooleanParenthesisExpression wrapped
        ? Unwrap(wrapped.Expression) : expression;

    private static ScalarExpression? Unwrap(ScalarExpression? expression) => expression is ParenthesisExpression wrapped
        ? Unwrap(wrapped.Expression) : expression;
}
