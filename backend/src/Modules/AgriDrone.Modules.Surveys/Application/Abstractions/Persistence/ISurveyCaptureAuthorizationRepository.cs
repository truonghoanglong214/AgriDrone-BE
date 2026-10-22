using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;

public interface ISurveyCaptureAuthorizationRepository
{
    Task<SurveyCaptureAuthorization?> GetAsync(
        Guid surveyOrderId,
        SurveyCaptureAuthorizationType type,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyCaptureAuthorization>> ListByOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}
