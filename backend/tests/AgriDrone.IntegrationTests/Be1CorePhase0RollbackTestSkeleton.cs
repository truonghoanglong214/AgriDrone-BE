using Xunit;

namespace AgriDrone.IntegrationTests;

/// <summary>
/// Shared all-or-nothing probe for the approval, mapping-publication and
/// result-publication PostgreSQL scenarios activated by their implementation
/// steps. Keeping arrange/mutate/assert outside the transaction makes the same
/// rollback contract reusable across all three specialized DbContexts.
/// </summary>
public abstract class Be1CorePhase0RollbackTestSkeleton
{
    protected static async Task AssertRollsBackAsync(
        Func<CancellationToken, Task> arrangeOutsideTransaction,
        Func<Func<CancellationToken, Task>, CancellationToken, Task> executeInTransaction,
        Func<CancellationToken, Task> mutateInsideTransaction,
        Func<CancellationToken, Task> assertNoPartialWrites,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arrangeOutsideTransaction);
        ArgumentNullException.ThrowIfNull(executeInTransaction);
        ArgumentNullException.ThrowIfNull(mutateInsideTransaction);
        ArgumentNullException.ThrowIfNull(assertNoPartialWrites);

        await arrangeOutsideTransaction(cancellationToken);

        await Assert.ThrowsAsync<RollbackProbeException>(() =>
            executeInTransaction(
                async token =>
                {
                    await mutateInsideTransaction(token);
                    throw new RollbackProbeException();
                },
                cancellationToken));

        await assertNoPartialWrites(cancellationToken);
    }

    private sealed class RollbackProbeException : Exception;
}
