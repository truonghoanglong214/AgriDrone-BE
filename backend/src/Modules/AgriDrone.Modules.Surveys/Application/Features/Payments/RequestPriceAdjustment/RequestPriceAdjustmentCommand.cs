using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;

public sealed record RequestPriceAdjustmentCommand(
    Guid SurveyOrderId,
    int NewConfirmedPoleCount,
    string Reason,
    string EvidenceReference,
    uint ExpectedOrderVersion)
    : IRequest<Result<RequestPriceAdjustmentResponse>>;
