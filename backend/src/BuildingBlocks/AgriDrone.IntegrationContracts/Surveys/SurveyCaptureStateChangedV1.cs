namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyCaptureStateChangedV1(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    string OrderStatus,
    string ChangeType,
    Guid? CaptureAttemptId,
    Guid? MissionId,
    Guid? CancellationDecisionId,
    Guid? RefundObligationId,
    DateTimeOffset ChangedAt);
