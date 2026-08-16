namespace AgriDrone.Modules.Identity.Application.Contracts.Invitations;

public sealed record InvitationTokenResult(
    string PlainTextToken,
    string TokenHash);
