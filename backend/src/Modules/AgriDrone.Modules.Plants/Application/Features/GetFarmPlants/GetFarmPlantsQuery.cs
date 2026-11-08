using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.GetFarmPlants;

public sealed record GetFarmPlantsQuery(Guid FarmId)
    : IRequest<Result<IReadOnlyList<FarmPlantItem>>>;
