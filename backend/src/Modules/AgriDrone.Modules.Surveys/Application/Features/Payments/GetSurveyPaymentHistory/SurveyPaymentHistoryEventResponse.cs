namespace AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;

public sealed record SurveyPaymentHistoryEventResponse(
    string EventType,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt);
