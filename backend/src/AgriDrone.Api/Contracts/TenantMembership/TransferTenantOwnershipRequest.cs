namespace AgriDrone.Api.Contracts.TenantMembership
{
    public sealed record TransferTenantOwnershipRequest
    {
        public Guid NewOwnerUserId { get; init; }
    }
}
