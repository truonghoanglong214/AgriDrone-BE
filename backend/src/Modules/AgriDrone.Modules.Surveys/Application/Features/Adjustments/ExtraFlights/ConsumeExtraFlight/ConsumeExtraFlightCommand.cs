using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ConsumeExtraFlight;

public sealed record ConsumeExtraFlightCommand(
    Guid SurveyOrderId,
    Guid RequestId,
    Guid ExtraMissionId,
    uint ExpectedVersion)
    : IRequest<Result<ExtraFlightRequestResponse>>;
