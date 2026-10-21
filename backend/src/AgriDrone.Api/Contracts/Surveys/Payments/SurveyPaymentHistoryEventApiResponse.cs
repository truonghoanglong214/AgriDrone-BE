namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record SurveyPaymentHistoryEventApiResponse(
    string EventType,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt);
