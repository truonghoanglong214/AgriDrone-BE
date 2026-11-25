using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyFundingReversal : Entity
{
    private SurveyFundingReversal() { }

    public Guid SurveyPaymentId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid DepositObligationId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ProviderEventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public Guid? FundingReceiptId { get; private set; }
    public decimal? CashAmount { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyFundingReversal Create(
        Guid id,
        Guid surveyPaymentId,
        Guid surveyOrderId,
        Guid depositObligationId,
        string provider,
        string providerEventId,
        string eventType,
        decimal amount,
        string currency,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt,
        Guid? fundingReceiptId = null,
        decimal? cashAmount = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyPaymentId, nameof(surveyPaymentId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(depositObligationId, nameof(depositObligationId));
        DomainGuard.Utc(occurredAt, nameof(occurredAt));
        DomainGuard.Utc(createdAt, nameof(createdAt));
        if (createdAt < occurredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(createdAt), "CreatedAt cannot precede OccurredAt.");
        }

        if (cashAmount.HasValue)
        {
            PolicyValidationGuard.ValidatePositiveWholeVnd(cashAmount.Value, nameof(cashAmount));
            if (amount < 0m || amount > cashAmount.Value || amount != decimal.Truncate(amount) ||
                fundingReceiptId == Guid.Empty || (!fundingReceiptId.HasValue && amount != 0m))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Invalid reversal allocation or receipt linkage.");
            }
        }
        else
        {
            if (fundingReceiptId.HasValue)
                throw new ArgumentException("A linked reversal requires its original cash amount.", nameof(cashAmount));

            PolicyValidationGuard.ValidatePositiveWholeVnd(amount, nameof(amount));
        }
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));
        var normalizedProvider = Required(provider, nameof(provider), 64);
        var normalizedEventId = Required(providerEventId, nameof(providerEventId), 150);
        var normalizedEventType = Required(eventType, nameof(eventType), 100);

        return new SurveyFundingReversal
        {
            Id = id,
            SurveyPaymentId = surveyPaymentId,
            SurveyOrderId = surveyOrderId,
            DepositObligationId = depositObligationId,
            Provider = normalizedProvider,
            ProviderEventId = normalizedEventId,
            EventType = normalizedEventType,
            FundingReceiptId = fundingReceiptId,
            CashAmount = cashAmount,
            Amount = amount,
            Currency = validatedCurrency,
            OccurredAt = occurredAt,
            CreatedAt = createdAt
        };
    }

    private static string Required(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.", name);
        }

        return normalized;
    }
}
