using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record FarmManagerAssignmentResponse(
    Guid AssignmentId,
    Guid TenantId,
    Guid FarmId,
    Guid SystemManagerProfileId,
    Guid ManagerUserId,
    DateTimeOffset AssignedAt,
    long Version);

public sealed record AssignPrimaryFarmManagerCommand(
    Guid FarmId,
    Guid SystemManagerProfileId,
    string Reason,
    long? ExpectedCurrentAssignmentVersion)
    : IRequest<Result<FarmManagerAssignmentResponse>>;
