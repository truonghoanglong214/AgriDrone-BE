using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.PublishFarmBaseMap;

public sealed record PublishFarmBaseMapCommand(
    Guid FarmId,
    Guid DraftId) : IRequest<Result<FarmBaseMapPublishedResult>>;
