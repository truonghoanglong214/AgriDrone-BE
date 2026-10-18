using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ApplyScopeAdjustment;

public sealed record ApplyScopeAdjustmentCommand(
    Guid SurveyOrderId,
    Guid ScopeAdjustmentId,
    uint ExpectedAdjustmentVersion)
    : IRequest<Result<ScopeAdjustmentResponse>>;
