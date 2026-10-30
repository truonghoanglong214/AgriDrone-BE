using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDraft;

public sealed record GetFarmBaseMapDraftQuery(
    Guid FarmId,
    Guid? DraftId = null,
    Guid? SurveyOrderId = null)
    : IRequest<Result<FarmBaseMapDraftResponse>>;
