using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;

public sealed record ConfirmSurveyOrderPoleCountCommand(
    Guid SurveyOrderId,
    Guid ExpectedFarmBaseMapVersionId,
    string EvidenceReference,
    uint ExpectedOrderVersion)
    : IRequest<Result<ConfirmSurveyOrderPoleCountResponse>>;
