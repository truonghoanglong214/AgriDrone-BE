using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDraft;

public sealed record GetFarmBaseMapDraftQuery(Guid FarmId, Guid DraftId)
    : IRequest<Result<FarmBaseMapDraftResponse>>;
