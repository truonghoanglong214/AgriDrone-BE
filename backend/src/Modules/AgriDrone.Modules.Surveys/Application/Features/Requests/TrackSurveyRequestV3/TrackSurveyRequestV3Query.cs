using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;

public sealed record TrackSurveyRequestV3Query(string TrackingToken)
    : IRequest<Result<TrackSurveyRequestV3Response>>;
