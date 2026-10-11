using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

public interface ISurveyApprovalIdempotencyRepository
{
    Task<SurveyApprovalIdempotencyRecord?> GetByActorAndKeyAsync(
        Guid actorId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<SurveyApprovalIdempotencyRecord?> GetByRequestIdAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default);

    Task AddClaimAsync(
        SurveyApprovalIdempotencyRecord record,
        CancellationToken cancellationToken = default);
}
