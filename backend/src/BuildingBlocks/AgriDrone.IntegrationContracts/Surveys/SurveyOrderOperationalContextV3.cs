namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyOrderOperationalContextV3(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    string ServiceCode,
    string ServiceType,
    string MissionPurpose,
    string OrderStatus,
    uint OrderVersion,
    Guid? CurrentFarmBoundaryVersionId,
    Guid? CurrentFarmBaseMapVersionId,
    int? EffectiveConfirmedPoleCount,
    DateTimeOffset? AppointmentStartAt,
    DateTimeOffset? AppointmentEndAt,
    Guid? PrimarySystemManagerId,
    CaptureAuthorizationStateV3 WrittenDataUseConsent,
    CaptureAuthorizationStateV3 FlightPermission,
    bool HasPendingFinancialSettlement,
    bool HasUnapprovedExtraFlight,
    bool IsReadyForCapture,
    IReadOnlyList<string> ReadinessFailures,
    DateTimeOffset EvaluatedAt);

public sealed record CaptureAuthorizationStateV3(
    string Status,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt,
    uint Version);

public interface ISurveyOrderOperationalContextV3Query
{
    Task<SurveyOrderOperationalContextV3?> GetAsync(
        Guid surveyOrderId,
        Guid missionId,
        string missionPurpose,
        CancellationToken cancellationToken = default);
}
