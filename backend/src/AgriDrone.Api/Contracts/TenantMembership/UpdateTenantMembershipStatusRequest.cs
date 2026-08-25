using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.TenantMembership;

public sealed record UpdateTenantMembershipStatusRequest
{
    public UpdateTenantMembershipStatusValue Status { get; init; }
}

[JsonConverter(
    typeof(JsonStringEnumConverter<UpdateTenantMembershipStatusValue>))]
public enum UpdateTenantMembershipStatusValue
{
    [JsonStringEnumMemberName("ACTIVE")]
    Active,

    [JsonStringEnumMemberName("INACTIVE")]
    Inactive
}
