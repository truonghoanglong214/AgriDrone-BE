namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record AssignFarmMemberApiResponse(
    Guid FarmMembershipId,
    Guid TenantId,
    Guid FarmId,
    Guid UserId,
    AssignFarmMemberRoleValue Role,
    AssignFarmAccessScopeValue AccessScope,
    IReadOnlyCollection<Guid> ZoneIds,
    string Status,
    long Version,
    DateTimeOffset JoinedAt);
