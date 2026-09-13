using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.FarmMemberships;

[JsonConverter(typeof(JsonStringEnumConverter<FarmMemberRoleValue>))]
public enum FarmMemberRoleValue
{
    [JsonStringEnumMemberName("MANAGER")]
    Manager,

    [JsonStringEnumMemberName("WORKER")]
    Worker
}

[JsonConverter(typeof(JsonStringEnumConverter<FarmAccessScopeValue>))]
public enum FarmAccessScopeValue
{
    [JsonStringEnumMemberName("ALL_ZONES")]
    AllZones,

    [JsonStringEnumMemberName("SELECTED_ZONES")]
    SelectedZones
}

[JsonConverter(typeof(JsonStringEnumConverter<FarmMembershipStatusValue>))]
public enum FarmMembershipStatusValue
{
    [JsonStringEnumMemberName("ACTIVE")]
    Active,

    [JsonStringEnumMemberName("INACTIVE")]
    Inactive
}
