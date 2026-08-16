namespace AgriDrone.Api.Contracts.TenantInvitations;

public sealed record AcceptTenantInvitationRequest(
    string Token,
    string? Password,
    string? FullName,
    string? Phone);
