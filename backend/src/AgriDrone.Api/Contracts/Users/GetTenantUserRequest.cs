namespace AgriDrone.Api.Contracts.Users
{
    public sealed class GetTenantUserRequest
    {
        public Guid TenantId { get; init; }
        public int PageNumber { get; init; } = 1;

        public int PageSize { get; init; } = 20;
    }
}
