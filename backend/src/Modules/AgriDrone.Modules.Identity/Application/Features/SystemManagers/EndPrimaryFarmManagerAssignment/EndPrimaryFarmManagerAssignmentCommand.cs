using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record EndPrimaryFarmManagerAssignmentCommand(
    Guid FarmId,
    string Reason,
    long ExpectedVersion) : IRequest<Result>;
