using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetAssignedFarmOperationalContext;

public sealed record GetAssignedFarmOperationalContextQuery(Guid FarmId)
    : IRequest<Result<FarmOperationalContextResponse>>;
