namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record MyFarmAssignmentListItemApiResponse(
    Guid FarmMembershipId,
    AssignedFarmSummaryApiResponse Farm,
    FarmMemberRoleValue Role,
    FarmAccessScopeValue AccessScope,
    IReadOnlyCollection<AssignedZoneSummaryApiResponse> Zones,
    string Status,
    long Version,
    DateTimeOffset JoinedAt);

public sealed record AssignedFarmSummaryApiResponse(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    decimal? AreaHectares);

public sealed record AssignedZoneSummaryApiResponse(
    Guid Id,
    string Code,
    string Name,
    decimal? AreaHectares);
