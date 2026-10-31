using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing.GetFarmPlantHealthMap;

public sealed record GetFarmPlantHealthMapQuery(
    Guid FarmId,
    Guid? TenantId = null) : IRequest<Result<FarmPlantHealthMapResult>>;

