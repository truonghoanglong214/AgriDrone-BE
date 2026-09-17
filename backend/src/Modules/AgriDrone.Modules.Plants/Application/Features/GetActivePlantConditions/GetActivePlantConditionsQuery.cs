using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.GetActivePlantConditions;

public sealed record GetActivePlantConditionsQuery
    : IRequest<Result<IReadOnlyList<PlantConditionCatalogResponse>>>;
