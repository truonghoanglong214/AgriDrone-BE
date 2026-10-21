namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record SurveyPaymentHistoryItemApiResponse(
    Guid SurveyPaymentId,
    string Purpose,
    decimal Amount,
    string Currency,
    string Provider,
    string? ProviderReference,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ConfirmedAt,
    uint PaymentVersion,
    IReadOnlyList<SurveyPaymentHistoryEventApiResponse> Events);
