using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Features.AssignFarmMember;

public sealed record AssignFarmMemberResponse(
    Guid FarmMembershipId,
    Guid TenantId,
    Guid FarmId,
    Guid UserId,
    FarmMemberRole Role,
    FarmAccessScope AccessScope,
    GeneralStatus Status,
    long Version,
    DateTimeOffset JoinedAt);
