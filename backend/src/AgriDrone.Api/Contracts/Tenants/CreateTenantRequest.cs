namespace AgriDrone.Api.Contracts.Tenants
{
    public sealed class CreateTenantRequest
    {
        public string TenantCode { get; init; } = null!;
        public string TenantName { get; init; } = null!;
    }
}
