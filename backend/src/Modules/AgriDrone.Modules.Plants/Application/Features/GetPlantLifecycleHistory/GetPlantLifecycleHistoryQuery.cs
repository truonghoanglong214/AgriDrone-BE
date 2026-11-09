using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.GetPlantLifecycleHistory;

public sealed record GetPlantLifecycleHistoryQuery(
    Guid FarmId,
    Guid PlantId)
    : IRequest<Result<PlantLifecycleHistoryResponse>>;

