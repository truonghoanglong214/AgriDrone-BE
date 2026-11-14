using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CancellationRetention : Entity
{
    private CancellationRetention() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CancellationDecisionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTimeOffset RecordedAt { get; private set; }

    public static CancellationRetention Create(
        Guid id,
        Guid surveyOrderId,
        Guid tenantId,
        Guid cancellationDecisionId,
        decimal amount,
        string currency,
        string reason,
        DateTimeOffset recordedAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(cancellationDecisionId, nameof(cancellationDecisionId));
        DomainGuard.Utc(recordedAt, nameof(recordedAt));

        var validatedAmount = PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));
        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reason, 500, nameof(reason));

        return new CancellationRetention
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            CancellationDecisionId = cancellationDecisionId,
            Amount = validatedAmount,
            Currency = validatedCurrency,
            Reason = normalizedReason,
            RecordedAt = recordedAt
        };
    }
}
