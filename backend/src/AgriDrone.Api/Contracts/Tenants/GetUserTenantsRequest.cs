namespace AgriDrone.Api.Contracts.Tenants;

public sealed record GetUserTenantsRequest
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
