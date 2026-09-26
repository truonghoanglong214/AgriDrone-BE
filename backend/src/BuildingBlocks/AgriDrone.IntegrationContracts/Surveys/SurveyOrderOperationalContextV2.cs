namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyOrderOperationalContextV2(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    string ServiceCode,
    string ServiceType,
    string OrderStatus,
    uint OrderVersion,
    decimal? ConfirmedAreaHa,
    DateTimeOffset? AppointmentStartAt,
    DateTimeOffset? AppointmentEndAt,
    string? AppointmentStatus,
    string? PaymentStatus,
    Guid? PrimarySystemManagerId,
    bool RequiresBaselineMapping,
    Guid? PreviousCompatibleOrderId,
    bool IsReadyForOperations,
    IReadOnlyList<string> ReadinessFailures,
    Guid? CurrentFarmBaseMapVersionId,
    DateTimeOffset EvaluatedAt);

public interface ISurveyOrderOperationalContextQuery
{
    Task<SurveyOrderOperationalContextV2?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}
