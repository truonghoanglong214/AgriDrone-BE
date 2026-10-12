using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;

public sealed record ApplyPriceAdjustmentCommand(
    Guid SurveyOrderId,
    Guid PriceAdjustmentId,
    uint ExpectedOrderVersion,
    uint ExpectedAdjustmentVersion,
    uint? ExpectedPaymentVersion)
    : IRequest<Result<ApplyPriceAdjustmentResponse>>;
