using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.UpdatePlantCondition;

public sealed record UpdatePlantConditionCommand(
    Guid ConditionId,
    string Name,
    string? ScientificName,
    string? Description,
    long ExpectedVersion) : IRequest<Result<PlantConditionResponse>>;
