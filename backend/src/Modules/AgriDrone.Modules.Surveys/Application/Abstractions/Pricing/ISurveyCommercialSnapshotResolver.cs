using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;

public interface ISurveyCommercialSnapshotResolver
{
    Task<Result<ResolvedSurveyCommercialSnapshot>> ResolveForApprovalAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}
