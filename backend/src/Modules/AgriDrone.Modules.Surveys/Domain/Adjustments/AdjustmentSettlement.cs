using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum AdjustmentSettlementType { AdditionalPayment, Refund }
public enum AdjustmentSettlementStatus { Pending, Processing, Confirmed, Failed }

public sealed class AdjustmentSettlement : Entity
{
    private AdjustmentSettlement() { }
    public Guid PriceAdjustmentId { get; private set; }
    public AdjustmentSettlementType Type { get; private set; }
    public AdjustmentSettlementStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string? ProviderReference { get; private set; }
    public string? FailureReason { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public PriceAdjustment PriceAdjustment { get; private set; } = null!;

    public static AdjustmentSettlement Create(
        Guid priceAdjustmentId,
        AdjustmentSettlementType type,
        decimal amount,
        CurrencyCode currency,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(priceAdjustmentId);
        DomainGuard.Utc(createdAt);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return new AdjustmentSettlement
        {
            Id = Guid.CreateVersion7(),
            PriceAdjustmentId = priceAdjustmentId,
            Type = type,
            Status = AdjustmentSettlementStatus.Pending,
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            Currency = currency.Value,
            IdempotencyKey = idempotencyKey.Trim(),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void MarkProcessing(string? providerReference, DateTimeOffset updatedAt, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status != AdjustmentSettlementStatus.Pending)
        {
            throw new InvalidOperationException("Only pending settlements can move to processing.");
        }

        DomainGuard.Utc(updatedAt);
        Status = AdjustmentSettlementStatus.Processing;
        ProviderReference = providerReference?.Trim();
        UpdatedAt = updatedAt;
        Version++;
    }

    public void Confirm(string? providerReference, DateTimeOffset confirmedAt, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status != AdjustmentSettlementStatus.Pending && Status != AdjustmentSettlementStatus.Processing)
        {
            throw new InvalidOperationException("Only pending or processing settlements can be confirmed.");
        }

        DomainGuard.Utc(confirmedAt);
        Status = AdjustmentSettlementStatus.Confirmed;
        ProviderReference = providerReference?.Trim() ?? ProviderReference;
        UpdatedAt = confirmedAt;
        Version++;
    }

    public void Fail(string failureReason, DateTimeOffset failedAt, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status != AdjustmentSettlementStatus.Pending && Status != AdjustmentSettlementStatus.Processing)
        {
            throw new InvalidOperationException("Only pending or processing settlements can fail.");
        }

        DomainGuard.Utc(failedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
        Status = AdjustmentSettlementStatus.Failed;
        FailureReason = failureReason.Trim();
        UpdatedAt = failedAt;
        Version++;
    }

    private void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException("Adjustment settlement version conflict.");
        }
    }
}
