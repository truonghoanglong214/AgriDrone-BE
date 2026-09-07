using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;

public sealed record GetFarmMemberAssignmentResponse(
    Guid FarmMembershipId,
    Guid TenantId,
    Guid FarmId,
    Guid UserId,
    FarmMemberRole Role,
    FarmAccessScope AccessScope,
    IReadOnlyCollection<Guid> ZoneIds,
    GeneralStatus Status,
    long Version,
    DateTimeOffset JoinedAt);
