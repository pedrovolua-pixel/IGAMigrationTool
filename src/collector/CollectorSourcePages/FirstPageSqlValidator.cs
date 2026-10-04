using System.Text;
using CollectorSafety;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace CollectorSourcePages;

/// <summary>Distinct no-boundary AST family. Does not authorize source access.</summary>
public static class FirstPageSqlValidator
{
    public static StrictKeysetSqlDecision Evaluate(string? sql, StrictKeysetSqlPolicy? policy) => Check(sql, policy, first: true);
    internal static StrictKeysetSqlDecision Check(string? sql, StrictKeysetSqlPolicy? policy, bool first)
    {
        if (string.IsNullOrWhiteSpace(sql) || policy?.ProjectedColumns is null) return StrictKeysetSqlDecision.InvalidInput;
        try { _ = new UTF8Encoding(false, true).GetByteCount(sql); }
        catch (EncoderFallbackException) { return StrictKeysetSqlDecision.InvalidInput; }
        var columns = policy.ProjectedColumns.ToArray();
        if (!Identifier(policy.Schema) || !Identifier(policy.Table) || !Identifier(policy.StableKey) ||
            columns.Length == 0 || columns.Any(c => !Identifier(c)) || columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length ||
            !columns.Contains(policy.StableKey, StringComparer.Ordinal) || !Parameter(policy.PageSizeParameter) ||
            !Parameter(policy.BoundaryParameter) || string.Equals(policy.PageSizeParameter, policy.BoundaryParameter, StringComparison.OrdinalIgnoreCase))
            return StrictKeysetSqlDecision.InvalidInput;
        var script = new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(sql), out var errors) as TSqlScript;
        if (errors.Count != 0 || script?.Batches.Count != 1 || script.Batches[0].Statements.Count != 1 ||
            script.Batches[0].Statements[0] is not SelectStatement select || select.QueryExpression is not QuerySpecification query ||
            select.Into is not null || select.WithCtesAndXmlNamespaces is not null || select.OptimizerHints.Count != 0 ||
            select.On is not null || select.ComputeClauses.Count != 0 || query.UniqueRowFilter != UniqueRowFilter.NotSpecified ||
            query.TopRowFilter is null || query.TopRowFilter.Percent || query.TopRowFilter.WithTies || query.TopRowFilter.WithApproximate ||
            Unwrap(query.TopRowFilter.Expression) is not VariableReference top || top.Name != policy.PageSizeParameter ||
            query.ForClause is not null || query.OffsetClause is not null || query.GroupByClause is not null ||
            query.HavingClause is not null || query.WindowClause is not null || query.FromClause?.TableReferences.Count != 1 ||
            query.FromClause.TableReferences[0] is not NamedTableReference table || table.TableHints.Count != 0 || table.TemporalClause is not null || table.TableSampleClause is not null ||
            table.SchemaObject.Identifiers.Count != 2 || table.SchemaObject.Identifiers[0].Value != policy.Schema ||
            table.SchemaObject.Identifiers[1].Value != policy.Table || table.Alias is not null && !Identifier(table.Alias.Value) ||
            query.SelectElements.Count != columns.Length || query.OrderByClause?.OrderByElements.Count != 1 ||
            first && query.WhereClause is not null || !first && query.WhereClause is null)
            return StrictKeysetSqlDecision.UnsafeSql;
        for (var i = 0; i < columns.Length; i++)
            if (query.SelectElements[i] is not SelectScalarExpression { ColumnName: null, Expression: ColumnReferenceExpression column } ||
                !Column(column, table, columns[i])) return StrictKeysetSqlDecision.UnsafeSql;
        var order = query.OrderByClause.OrderByElements[0];
        if (order.SortOrder is not SortOrder.NotSpecified and not SortOrder.Ascending ||
            order.Expression is not ColumnReferenceExpression key || !Column(key, table, policy.StableKey)) return StrictKeysetSqlDecision.UnsafeSql;
        return StrictKeysetSqlDecision.StructurallyReady;
    }
    private static bool Identifier(string? value) => value is { Length: > 0 and <= 128 } &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
    private static bool Parameter(string? value) => value is { Length: > 1 and <= 129 } && value[0] == '@' && Identifier(value[1..]);
    private static bool Column(ColumnReferenceExpression column, NamedTableReference table, string expected)
    {
        var parts = column.MultiPartIdentifier?.Identifiers;
        return parts is { Count: >= 1 and <= 2 } && parts[^1].Value == expected &&
            (parts.Count == 1 || parts[0].Value == table.SchemaObject.Identifiers[^1].Value || parts[0].Value == table.Alias?.Value);
    }
    private static ScalarExpression? Unwrap(ScalarExpression? expression)
    {
        // Iterative syntax unwrapping avoids recursive native stack growth.
        while (expression is ParenthesisExpression wrapped) expression = wrapped.Expression;
        return expression;
    }
}
