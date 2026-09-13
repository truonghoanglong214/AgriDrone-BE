namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record AssignFarmMemberRequest(
    [property: System.Text.Json.Serialization.JsonRequired]
    FarmMemberRoleValue Role,
    [property: System.Text.Json.Serialization.JsonRequired]
    FarmAccessScopeValue AccessScope,
    IReadOnlyCollection<Guid>? ZoneIds,
    long? ExpectedVersion,
    string? Reason);
