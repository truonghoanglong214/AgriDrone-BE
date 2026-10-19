using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ReviewExtraFlight;

public sealed record ReviewExtraFlightCommand(
    Guid SurveyOrderId,
    Guid RequestId,
    bool Approved,
    uint ExpectedVersion)
    : IRequest<Result<ExtraFlightRequestResponse>>;
