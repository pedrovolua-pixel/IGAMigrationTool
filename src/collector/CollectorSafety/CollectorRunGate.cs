using System.Collections.Concurrent;

namespace CollectorSafety;

public enum CollectorRunGateDecision
{
    Completed,
    SkippedOverlap,
    InvalidScope
}

/// <summary>
/// Prevents overlapping in-process runs for the same trusted scope. It does
/// not schedule runs, persist a lease or authorize source access.
/// </summary>
public sealed class CollectorRunGate
{
    private readonly ConcurrentDictionary<string, byte> _activeScopes =
        new(StringComparer.Ordinal);

    public async Task<CollectorRunGateDecision> RunOnceAsync(
        string? scopeId,
        Func<CancellationToken, Task> run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return CollectorRunGateDecision.InvalidScope;
        }

        if (!_activeScopes.TryAdd(scopeId, 0))
        {
            return CollectorRunGateDecision.SkippedOverlap;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await run(cancellationToken).ConfigureAwait(false);
            return CollectorRunGateDecision.Completed;
        }
        finally
        {
            _activeScopes.TryRemove(scopeId, out _);
        }
    }
}
