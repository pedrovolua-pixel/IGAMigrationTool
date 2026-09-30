using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace CollectorSafety;

public sealed record StaticSqlShapePolicy(
    string Schema,
    string Table,
    IReadOnlyCollection<string> AllowedColumns,
    string PageSizeParameter,
    string BoundaryParameter);

public enum StaticSqlShapeDecision
{
    StructurallyReadOnlyPage,
    InvalidInput,
    ParseFailure,
    MultipleStatements,
    UnsafeShape
}

/// <summary>
/// Conservative AST preflight for a single-table, parameterized SELECT page.
/// It is only one check in query-pack validation and never authorizes SQL.
/// </summary>
public static class StaticSqlShapeValidator
{
    public static StaticSqlShapeDecision Evaluate(string? sql, StaticSqlShapePolicy? policy)
    {
        if (string.IsNullOrWhiteSpace(sql) || !ValidPolicy(policy))
        {
            return StaticSqlShapeDecision.InvalidInput;
        }

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        var parsed = parser.Parse(new StringReader(sql), out var parseErrors);
        if (parseErrors.Count > 0 || parsed is not TSqlScript script)
        {
            return StaticSqlShapeDecision.ParseFailure;
        }

        if (script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1)
        {
            return StaticSqlShapeDecision.MultipleStatements;
        }

        if (script.Batches[0].Statements[0] is not SelectStatement select ||
            select.QueryExpression is not QuerySpecification query ||
            select.Into is not null || select.WithCtesAndXmlNamespaces is not null ||
            select.OptimizerHints.Count > 0 || select.On is not null ||
            select.ComputeClauses.Count > 0 ||
            query.TopRowFilter is null || query.TopRowFilter.Percent ||
            query.TopRowFilter.WithTies || query.TopRowFilter.WithApproximate ||
            !PageSizeBound(query.TopRowFilter.Expression, policy!.PageSizeParameter) ||
            query.ForClause is not null || query.OffsetClause is not null ||
            query.GroupByClause is not null || query.HavingClause is not null ||
            query.WindowClause is not null ||
            query.FromClause?.TableReferences.Count != 1 ||
            query.FromClause.TableReferences[0] is not NamedTableReference table ||
            !MatchesTable(table, policy) ||
            query.SelectElements.Count == 0 ||
            query.SelectElements.Any(element => element is not SelectScalarExpression
            {
                Expression: ColumnReferenceExpression
            }) ||
            query.WhereClause is null || query.OrderByClause?.OrderByElements.Count is not > 0 ||
            query.OrderByClause.OrderByElements.Any(element => element.Expression is not ColumnReferenceExpression))
        {
            return StaticSqlShapeDecision.UnsafeShape;
        }

        var visitor = new ReadOnlyShapeVisitor(policy);
        query.Accept(visitor);
        return visitor.IsSafe
            ? StaticSqlShapeDecision.StructurallyReadOnlyPage
            : StaticSqlShapeDecision.UnsafeShape;
    }

    private static bool ValidPolicy(StaticSqlShapePolicy? policy) =>
        policy is not null &&
        !string.IsNullOrWhiteSpace(policy.Schema) &&
        !string.IsNullOrWhiteSpace(policy.Table) &&
        policy.AllowedColumns is { Count: > 0 } &&
        policy.AllowedColumns.All(column => !string.IsNullOrWhiteSpace(column)) &&
        ValidParameter(policy.PageSizeParameter) &&
        ValidParameter(policy.BoundaryParameter) &&
        !string.Equals(policy.PageSizeParameter, policy.BoundaryParameter, StringComparison.Ordinal);

    private static bool ValidParameter(string? value) =>
        value is { Length: > 1 } && value[0] == '@' &&
        value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static bool PageSizeBound(ScalarExpression? expression, string parameter) =>
        expression switch
        {
            VariableReference variable => string.Equals(variable.Name, parameter, StringComparison.Ordinal),
            ParenthesisExpression wrapped => PageSizeBound(wrapped.Expression, parameter),
            _ => false
        };

    private static bool MatchesTable(NamedTableReference table, StaticSqlShapePolicy policy)
    {
        var parts = table.SchemaObject.Identifiers;
        return parts.Count == 2 &&
            string.Equals(parts[0].Value, policy.Schema, StringComparison.Ordinal) &&
            string.Equals(parts[1].Value, policy.Table, StringComparison.Ordinal) &&
            table.TableHints.Count == 0;
    }

    private sealed class ReadOnlyShapeVisitor(StaticSqlShapePolicy policy) : TSqlFragmentVisitor
    {
        private bool _boundarySeen;
        private bool _pageSizeSeen;
        private int _queryCount;

        public bool IsSafe => _queryCount == 1 && _boundarySeen && _pageSizeSeen && !Unsafe;

        private bool Unsafe { get; set; }

        public override void ExplicitVisit(QuerySpecification node)
        {
            _queryCount++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            var parts = node.MultiPartIdentifier?.Identifiers;
            if (parts is null || parts.Count is < 1 or > 2 ||
                !policy.AllowedColumns.Contains(parts[^1].Value, StringComparer.Ordinal))
            {
                Unsafe = true;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(VariableReference node)
        {
            if (string.Equals(node.Name, policy.PageSizeParameter, StringComparison.Ordinal))
            {
                _pageSizeSeen = true;
            }
            else if (string.Equals(node.Name, policy.BoundaryParameter, StringComparison.Ordinal))
            {
                _boundarySeen = true;
            }
            else
            {
                Unsafe = true;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            Unsafe = true;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            Unsafe = true;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(GlobalVariableExpression node)
        {
            Unsafe = true;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(NextValueForExpression node)
        {
            Unsafe = true;
            base.ExplicitVisit(node);
        }
    }
}
