using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Orders;

public interface ISurveyOrderReadinessService
{
    Task<Result<SurveyOrderReadinessEvaluation>> EvaluateAsync(
        Guid surveyOrderId,
        Guid missionId,
        SurveyOperationPurpose purpose,
        CancellationToken cancellationToken = default);
}

public interface ISurveyOrderReadinessStateQueries
{
    Task<Result<SurveyOrderReadinessState>> GetCurrentAsync(
        Guid surveyOrderId,
        Guid missionId,
        SurveyOperationPurpose purpose,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}

public sealed record SurveyOrderReadinessState(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid TenantId,
    Guid FarmId,
    uint OrderVersion,
    Guid? AppointmentId,
    Guid? SystemManagerProfileId,
    Guid? SystemManagerUserId,
    SurveyOrderReadinessSnapshot Snapshot);

public sealed record SurveyOrderReadinessEvaluation(
    Guid SurveyOrderId,
    Guid MissionId,
    Guid TenantId,
    Guid FarmId,
    SurveyOperationPurpose Purpose,
    uint OrderVersion,
    Guid? AppointmentId,
    Guid? SystemManagerProfileId,
    Guid? SystemManagerUserId,
    bool IsReady,
    IReadOnlyList<string> FailureCodes,
    DateTimeOffset EvaluatedAt);
