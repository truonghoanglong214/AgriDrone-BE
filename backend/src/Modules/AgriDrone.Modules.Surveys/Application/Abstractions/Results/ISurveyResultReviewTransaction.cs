using AgriDrone.Modules.Surveys.Application.Features.Results.FreezeSurveyInternalResult;
using AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;
using AgriDrone.Modules.Surveys.Application.Features.Results.RecordSurveyComboReview;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Results;

public interface ISurveyResultReviewTransaction
{
    Task<Result<SurveyResultReviewResponse>> GetReviewAsync(
        Guid orderId,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<RecordSurveyComboReviewResponse>> RecordComboReviewAsync(
        RecordSurveyComboReviewCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    Task<Result<FreezeSurveyInternalResultResponse>> FreezeInternalResultAsync(
        FreezeSurveyInternalResultCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
