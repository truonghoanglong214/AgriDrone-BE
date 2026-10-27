namespace AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;

public sealed record SurveyPaymentHistoryResponse(
    Guid SurveyOrderId,
    IReadOnlyList<SurveyPaymentHistoryItemResponse> Payments);
