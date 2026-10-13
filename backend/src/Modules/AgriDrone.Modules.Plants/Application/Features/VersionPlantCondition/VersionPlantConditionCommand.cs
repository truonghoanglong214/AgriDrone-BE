using AgriDrone.Modules.Plants.Application.Features.CreatePlantCondition;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.VersionPlantCondition;

public sealed record VersionPlantConditionCommand(
    Guid ConditionId,
    string Name,
    string? ScientificName,
    string? Description,
    long ExpectedVersion) : IRequest<Result<PlantConditionResponse>>;
