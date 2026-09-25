namespace AgriDrone.Api.Contracts.SystemManagerInvitations;

public sealed record AcceptSystemManagerInvitationRequest(
    string Token,
    string? Password,
    string? FullName,
    string? Phone);