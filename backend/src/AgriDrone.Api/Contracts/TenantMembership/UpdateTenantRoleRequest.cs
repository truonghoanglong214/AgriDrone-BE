using System.Text.Json.Serialization;

namespace AgriDrone.Api.Contracts.TenantMembership
{
    public sealed record UpdateTenantRoleRequest
    {
        public UpdateTenantRoleValue Role { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<UpdateTenantRoleValue>))]
    public enum UpdateTenantRoleValue
    {
        [JsonStringEnumMemberName("MEMBER")]
        Member,

        [JsonStringEnumMemberName("TENANT_ADMIN")]
        TenantAdmin
    }
}
