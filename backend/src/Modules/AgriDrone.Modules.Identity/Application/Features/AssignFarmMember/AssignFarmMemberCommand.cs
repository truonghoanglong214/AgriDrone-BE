using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;

public sealed record AssignFarmMemberCommand(
    Guid FarmId,
    Guid UserId,
    FarmMemberRole Role,
    FarmAccessScope AccessScope,
    IReadOnlyCollection<Guid> ZoneIds,
    long? ExpectedVersion,
    string? Reason)
    : IRequest<Result<AssignFarmMemberResponse>>;
