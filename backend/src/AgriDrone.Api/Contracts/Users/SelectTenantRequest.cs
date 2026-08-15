namespace AgriDrone.Api.Contracts.Users;

public sealed record SelectTenantRequest(
    string SelectionToken,
    Guid TenantId);
