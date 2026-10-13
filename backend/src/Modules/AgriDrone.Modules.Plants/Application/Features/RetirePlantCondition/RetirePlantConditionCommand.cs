using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.RetirePlantCondition;

public sealed record RetirePlantConditionCommand(
    Guid ConditionId,
    long ExpectedVersion) : IRequest<Result>;
