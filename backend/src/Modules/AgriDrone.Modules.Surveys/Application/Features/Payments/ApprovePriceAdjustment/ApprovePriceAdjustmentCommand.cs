using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ApprovePriceAdjustment;

public sealed record ApprovePriceAdjustmentCommand(
    Guid SurveyOrderId,
    Guid PriceAdjustmentId,
    string Reason,
    string EvidenceReference,
    uint ExpectedOrderVersion,
    uint ExpectedAdjustmentVersion)
    : IRequest<Result<ApprovePriceAdjustmentResponse>>;
