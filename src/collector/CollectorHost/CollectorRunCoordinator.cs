using System.Security.Cryptography;
using System.Diagnostics;
using CollectorSafety;

namespace CollectorHost;

// Only a separately reviewed adapter may provide these claims and durable page storage.
// The shipped host supplies no such adapter, so it cannot read a source or stage evidence.
internal interface ICollectorRunAdapter
{
    Task<ApprovedCollectorRun?> LoadApprovedRunAsync(CollectorConfig config, CancellationToken cancellationToken);
    Task<CollectorPage> ReadPageAsync(string? afterBoundary, int pageSize, CancellationToken cancellationToken);
    Task StagePageAsync(string boundary, int rowCount, bool isTerminal, string digest,
        IReadOnlyList<MinimizedField> fields,
        CancellationToken cancellationToken);
}

internal sealed record ApprovedCollectorRun(
    Guid QueryPackId, int QueryPackVersion, string QueryPackSha256,
    SourceBuildClaim Source, QueryApplicabilityRule Query,
    PermissionProbe Permission, FieldPolicySnapshot FieldPolicy,
    string OrderingKey, int MaximumPageSize, long MaximumRows, TimeSpan MaximumDuration,
    string CheckpointPath, byte[] CheckpointKey);

internal sealed record CollectorPage(string Boundary, int RowCount,
    IReadOnlyList<FieldCandidate> Fields, bool HasMore);

internal enum CollectorRunOutcome
{
    Disabled, SourceContractPending, InvalidApproval, UnsupportedSource,
    PermissionBlocked, OverlapSkipped, CheckpointRejected, PageRejected,
    SourceFailed, StageFailed, Canceled, LimitReached, Completed
}

internal sealed record CollectorRunResult(CollectorRunOutcome Outcome, int CompletedPages,
    long CompletedRows, bool ExcessReadOnlyWarning);

internal sealed class CollectorRunCoordinator(ICollectorRunAdapter adapter)
{
    public async Task<CollectorRunResult> RunAsync(string configPath, CollectorConfig config,
        CancellationToken cancellationToken)
    {
        if (!config.Enabled)
        {
            return Result(CollectorRunOutcome.Disabled);
        }

        ApprovedCollectorRun? approved;
        try
        {
            approved = await adapter.LoadApprovedRunAsync(config, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(CollectorRunOutcome.Canceled);
        }
        catch (OperationCanceledException)
        {
            return Result(CollectorRunOutcome.SourceFailed);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
               InvalidDataException or CryptographicException)
        {
            return Result(CollectorRunOutcome.SourceFailed);
        }
        if (approved is null)
        {
            return Result(CollectorRunOutcome.SourceContractPending);
        }

        if (!ValidApproval(config, approved))
        {
            return Result(CollectorRunOutcome.InvalidApproval);
        }

        if (QueryApplicability.Evaluate(approved.Source, approved.Query) != QueryApplicabilityDecision.Compatible)
        {
            return Result(CollectorRunOutcome.UnsupportedSource);
        }

        var permission = PermissionAttestation.Evaluate(approved.Permission);
        if (permission.BlocksEvidenceQueries)
        {
            return Result(CollectorRunOutcome.PermissionBlocked);
        }

        using var lease = CrossProcessRunLease.TryAcquire(configPath, config.ScopeId);
        if (lease is null)
        {
            return Result(CollectorRunOutcome.OverlapSkipped);
        }

        // The pack and policy digests form part of the authenticated ledger context.
        var context = new PageCheckpointContext(approved.Query.QueryId,
            $"{approved.QueryPackId:D}/{approved.QueryPackVersion}/{approved.QueryPackSha256}",
            config.ExactBuild, config.ScopeId.ToString("D"),
            $"{config.FieldPolicyId:D}/{config.FieldPolicyVersion}/{config.FieldPolicySha256}",
            approved.OrderingKey);
        List<PageCheckpoint> checkpoints;
        try
        {
            checkpoints = [.. EncryptedCheckpointStore.Load(approved.CheckpointPath, context,
                approved.CheckpointKey) ?? []];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
               InvalidDataException or CryptographicException)
        {
            return Result(CollectorRunOutcome.CheckpointRejected);
        }

        var started = Stopwatch.StartNew();
        var rows = checkpoints.Sum(page => (long)page.RowCount);
        var policy = new PageBudgetPolicy(Math.Min(config.MaxPageSize, approved.MaximumPageSize),
            Math.Min(config.MaxRows, approved.MaximumRows),
            TimeSpan.FromSeconds(Math.Min(config.MaxDurationSeconds, approved.MaximumDuration.TotalSeconds)));
        if (rows > policy.MaximumRows)
        {
            return new CollectorRunResult(CollectorRunOutcome.CheckpointRejected, checkpoints.Count, rows,
                permission.RequiresWarningAndAudit);
        }

        if (checkpoints.LastOrDefault()?.IsTerminal == true)
        {
            return new CollectorRunResult(CollectorRunOutcome.Completed, checkpoints.Count, rows,
                permission.RequiresWarningAndAudit);
        }

        var boundary = checkpoints.LastOrDefault()?.PageBoundary;

        while (true)
        {
            var budget = PageBudget.Evaluate(policy,
                new PageBudgetSnapshot(rows, started.Elapsed, cancellationToken.IsCancellationRequested));
            if (budget.Decision != PageBudgetDecision.Permit)
            {
                return new CollectorRunResult(budget.Decision == PageBudgetDecision.Canceled
                    ? CollectorRunOutcome.Canceled : CollectorRunOutcome.LimitReached, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            var requested = budget.PermittedPageSize;
            CollectorPage page;
            var remaining = policy.MaximumDuration - started.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return new CollectorRunResult(CollectorRunOutcome.LimitReached, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            using var pageTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pageTimeout.CancelAfter(remaining);
            try
            {
                page = await adapter.ReadPageAsync(boundary, requested, pageTimeout.Token);
            }
            catch (OperationCanceledException) when (pageTimeout.IsCancellationRequested)
            {
                return new CollectorRunResult(cancellationToken.IsCancellationRequested
                    ? CollectorRunOutcome.Canceled : CollectorRunOutcome.LimitReached,
                    checkpoints.Count, rows, permission.RequiresWarningAndAudit);
            }
            catch (OperationCanceledException)
            {
                return new CollectorRunResult(CollectorRunOutcome.SourceFailed, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                   InvalidDataException or CryptographicException)
            {
                return new CollectorRunResult(CollectorRunOutcome.SourceFailed, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            if (page is null || string.IsNullOrWhiteSpace(page.Boundary) ||
                page.RowCount < 0 || page.RowCount > requested || page.Fields is null ||
                (page.HasMore && page.RowCount == 0) ||
                (boundary is not null && page.Boundary == boundary))
            {
                return new CollectorRunResult(CollectorRunOutcome.PageRejected, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            var fields = page.Fields.Select(field => FieldMinimizer.Evaluate(approved.FieldPolicy, field)).ToArray();
            if (fields.Any(field => field.Disposition is FieldDisposition.Prohibited or FieldDisposition.Unclassified))
            {
                return new CollectorRunResult(CollectorRunOutcome.PageRejected, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            // Digest only minimized fields; prohibited and excluded source values never enter the ledger.
            var isTerminal = !page.HasMore;
            var digest = MinimizedPageDigest.Compute(page.Boundary, page.RowCount, isTerminal, fields);
            var checkpoint = new PageCheckpoint(context, page.Boundary, digest, page.RowCount, isTerminal);
            if (PageCheckpointVerifier.Evaluate(context, checkpoints, checkpoint) != PageCheckpointDecision.NewPage)
            {
                return new CollectorRunResult(CollectorRunOutcome.CheckpointRejected, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            // A future adapter must durably and idempotently stage before the ledger advances.
            // The same deadline bounds reading and staging of this page.
            if (pageTimeout.IsCancellationRequested)
            {
                return new CollectorRunResult(cancellationToken.IsCancellationRequested
                    ? CollectorRunOutcome.Canceled : CollectorRunOutcome.LimitReached,
                    checkpoints.Count, rows, permission.RequiresWarningAndAudit);
            }

            try
            {
                await adapter.StagePageAsync(page.Boundary, page.RowCount, isTerminal, digest, fields,
                    pageTimeout.Token);
            }
            catch (OperationCanceledException) when (pageTimeout.IsCancellationRequested)
            {
                return new CollectorRunResult(cancellationToken.IsCancellationRequested
                    ? CollectorRunOutcome.Canceled : CollectorRunOutcome.LimitReached,
                    checkpoints.Count, rows, permission.RequiresWarningAndAudit);
            }
            catch (OperationCanceledException)
            {
                return new CollectorRunResult(CollectorRunOutcome.StageFailed, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                   InvalidDataException or CryptographicException)
            {
                return new CollectorRunResult(CollectorRunOutcome.StageFailed, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }

            checkpoints.Add(checkpoint);
            try
            {
                EncryptedCheckpointStore.Save(approved.CheckpointPath, context, checkpoints,
                    approved.CheckpointKey);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                   InvalidDataException or CryptographicException)
            {
                return new CollectorRunResult(CollectorRunOutcome.CheckpointRejected, checkpoints.Count - 1,
                    rows, permission.RequiresWarningAndAudit);
            }
            rows += page.RowCount;
            boundary = page.Boundary;
            if (!page.HasMore)
            {
                return new CollectorRunResult(CollectorRunOutcome.Completed, checkpoints.Count, rows,
                    permission.RequiresWarningAndAudit);
            }
        }
    }

    private static bool ValidApproval(CollectorConfig config, ApprovedCollectorRun run) =>
        run.QueryPackId == config.QueryPackId &&
        run.QueryPackVersion == config.QueryPackVersion &&
        string.Equals(run.QueryPackSha256, config.QueryPackSha256, StringComparison.OrdinalIgnoreCase) &&
        run.Source is not null && run.Source.ExactBuild == config.ExactBuild &&
        run.Query is not null && run.Permission is not null &&
        run.FieldPolicy is not null &&
        run.FieldPolicy.PolicyId == config.FieldPolicyId.ToString("D") &&
        run.FieldPolicy.Version == config.FieldPolicyVersion.ToString() &&
        string.Equals(run.FieldPolicy.Sha256, config.FieldPolicySha256, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(run.OrderingKey) &&
        run.MaximumPageSize > 0 && run.MaximumRows > 0 &&
        run.MaximumDuration > TimeSpan.Zero &&
        Path.IsPathFullyQualified(run.CheckpointPath) &&
        run.CheckpointKey is { Length: 32 };

    private static CollectorRunResult Result(CollectorRunOutcome outcome) => new(outcome, 0, 0, false);
}

internal sealed class PendingCollectorRunAdapter : ICollectorRunAdapter
{
    public Task<ApprovedCollectorRun?> LoadApprovedRunAsync(CollectorConfig config,
        CancellationToken cancellationToken) => Task.FromResult<ApprovedCollectorRun?>(null);

    public Task<CollectorPage> ReadPageAsync(string? afterBoundary, int pageSize,
        CancellationToken cancellationToken) => throw new InvalidOperationException("Source contract is pending.");

    public Task StagePageAsync(string boundary, int rowCount, bool isTerminal, string digest,
        IReadOnlyList<MinimizedField> fields,
        CancellationToken cancellationToken) => throw new InvalidOperationException("Delivery contract is pending.");
}
