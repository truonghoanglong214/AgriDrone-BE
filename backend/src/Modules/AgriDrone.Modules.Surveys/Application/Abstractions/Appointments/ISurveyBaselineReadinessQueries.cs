using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;

public interface ISurveyBaselineReadinessQueries
{
    Task<Result<SurveyBaselineReadinessState>> GetAsync(
        Guid surveyOrderId,
        Guid missionId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}

public sealed record SurveyBaselineReadinessState(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    uint OrderVersion,
    Guid? AppointmentId,
    Guid? SystemManagerProfileId,
    SurveyOrderReadinessSnapshot Snapshot);
