using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;

public sealed record RejectPriceAdjustmentCommand(
    Guid SurveyOrderId,
    Guid PriceAdjustmentId,
    string Reason,
    string EvidenceReference,
    uint ExpectedOrderVersion,
    uint ExpectedAdjustmentVersion)
    : IRequest<Result<RejectPriceAdjustmentResponse>>;
