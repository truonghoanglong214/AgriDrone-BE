using AgriDrone.IntegrationContracts.Surveys;

namespace AgriDrone.Database.Surveys;

/// <summary>
/// A2.1 fail-closed defaults for Plan 2 release-proof and ledger-fact read ports until A2.4/A2.5
/// create and populate durable release receipts and financial recognition entries.
/// </summary>
internal sealed class UnavailableV3ResultReleaseProofReader : IV3ResultReleaseProofReader
{
    public Task<V3ResultReleaseProof?> GetByOrderIdAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<V3ResultReleaseProof?>(null);
    }

    public Task<V3ResultReleaseProof?> GetByPublicationIdAsync(
        Guid tenantId,
        Guid publicationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<V3ResultReleaseProof?>(null);
    }
}

internal sealed class UnavailableV3FinancialLedgerFactsReader : IV3FinancialLedgerFactsReader
{
    public Task<V3OrderLedgerFacts?> GetOrderLedgerFactsAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<V3OrderLedgerFacts?>(null);
    }
}
