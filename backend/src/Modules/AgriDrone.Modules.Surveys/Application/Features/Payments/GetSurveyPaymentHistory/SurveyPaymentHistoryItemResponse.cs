namespace AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;

public sealed record SurveyPaymentHistoryItemResponse(
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
    IReadOnlyList<SurveyPaymentHistoryEventResponse> Events);
