using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ReviewScopeAdjustment;

public sealed record ReviewScopeAdjustmentCommand(
    Guid SurveyOrderId,
    Guid ScopeAdjustmentId,
    bool Approved,
    string ReviewReason,
    uint ExpectedAdjustmentVersion)
    : IRequest<Result<ScopeAdjustmentResponse>>;
