namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record SurveyPaymentHistoryApiResponse(
    Guid SurveyOrderId,
    IReadOnlyList<SurveyPaymentHistoryItemApiResponse> Payments);
