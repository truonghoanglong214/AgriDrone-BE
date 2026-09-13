using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;

public sealed record FarmMemberListItemResponse(
    Guid FarmMembershipId,
    Guid FarmId,
    Guid UserId,
    string Email,
    string FullName,
    TenantMemberRole TenantRole,
    FarmMemberRole Role,
    FarmAccessScope AccessScope,
    IReadOnlyCollection<Guid> ZoneIds,
    GeneralStatus Status,
    long Version,
    DateTimeOffset JoinedAt);
