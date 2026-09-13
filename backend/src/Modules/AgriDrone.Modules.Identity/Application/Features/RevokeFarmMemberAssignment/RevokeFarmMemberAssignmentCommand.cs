using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.RevokeFarmMemberAssignment;

public sealed record RevokeFarmMemberAssignmentCommand(
    Guid FarmId,
    Guid UserId,
    long ExpectedVersion,
    string? Reason) : IRequest<Result>;
