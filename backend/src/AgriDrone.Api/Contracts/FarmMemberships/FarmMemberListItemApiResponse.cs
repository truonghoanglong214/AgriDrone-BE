namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record FarmMemberListItemApiResponse(
    Guid FarmMembershipId,
    Guid FarmId,
    Guid UserId,
    string Email,
    string FullName,
    string TenantRole,
    FarmMemberRoleValue Role,
    FarmAccessScopeValue AccessScope,
    IReadOnlyCollection<Guid> ZoneIds,
    string Status,
    long Version,
    DateTimeOffset JoinedAt);
