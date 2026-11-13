using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Services;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;

internal sealed class TrackSurveyRequestV3QueryHandler(
    ISurveyRequestRepository requestRepository)
    : IRequestHandler<TrackSurveyRequestV3Query, Result<TrackSurveyRequestV3Response>>
{
    public async Task<Result<TrackSurveyRequestV3Response>> Handle(
        TrackSurveyRequestV3Query request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TrackingToken))
        {
            return Result.Failure<TrackSurveyRequestV3Response>(
                SurveyRequestError.TrackingTokenNotFound());
        }

        string tokenHash;
        try
        {
            tokenHash = TrackingTokenService.ComputeHash(request.TrackingToken);
        }
        catch (ArgumentException)
        {
            return Result.Failure<TrackSurveyRequestV3Response>(
                SurveyRequestError.TrackingTokenNotFound());
        }

        var tracking = await requestRepository.GetTrackingByHashAsync(
            tokenHash,
            cancellationToken);

        if (tracking is null)
        {
            return Result.Failure<TrackSurveyRequestV3Response>(
                SurveyRequestError.TrackingTokenNotFound());
        }

        var surveyRequest = await requestRepository.GetByIdAsync(
            tracking.SurveyRequestId,
            cancellationToken);

        if (surveyRequest is null)
        {
            return Result.Failure<TrackSurveyRequestV3Response>(
                SurveyRequestError.TrackingTokenNotFound());
        }

        var quote = await requestRepository.GetQuoteSnapshotByRequestIdAsync(
            tracking.SurveyRequestId,
            cancellationToken);

        return Result.Success(new TrackSurveyRequestV3Response(
            surveyRequest.RequestNumber,
            surveyRequest.Status,
            surveyRequest.SurveyService?.Name ?? "Survey Service",
            quote?.EstimatedPoleCount,
            quote?.IndicativeTotalAmount,
            quote?.DepositAmount ?? 0m,
            quote?.Currency ?? "VND",
            quote?.EvaluatedAt ?? surveyRequest.CreatedAt,
            surveyRequest.CreatedAt));
    }
}
