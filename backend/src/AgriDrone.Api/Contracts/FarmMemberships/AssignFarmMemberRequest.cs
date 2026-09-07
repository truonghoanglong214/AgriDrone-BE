using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.FarmMemberships;

public sealed record AssignFarmMemberRequest(
    [property: JsonRequired] AssignFarmMemberRoleValue Role,
    [property: JsonRequired] AssignFarmAccessScopeValue AccessScope,
    IReadOnlyCollection<Guid>? ZoneIds,
    long? ExpectedVersion,
    string? Reason);

[JsonConverter(typeof(JsonStringEnumConverter<AssignFarmMemberRoleValue>))]
public enum AssignFarmMemberRoleValue
{
    [JsonStringEnumMemberName("MANAGER")]
    Manager,

    [JsonStringEnumMemberName("WORKER")]
    Worker
}

[JsonConverter(typeof(JsonStringEnumConverter<AssignFarmAccessScopeValue>))]
public enum AssignFarmAccessScopeValue
{
    [JsonStringEnumMemberName("ALL_ZONES")]
    AllZones,

    [JsonStringEnumMemberName("SELECTED_ZONES")]
    SelectedZones
}
