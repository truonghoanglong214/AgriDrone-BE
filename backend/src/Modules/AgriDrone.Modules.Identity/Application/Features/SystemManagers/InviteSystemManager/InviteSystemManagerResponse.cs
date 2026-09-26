namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record InviteSystemManagerResponse(
    Guid InvitationId,
    string Email,
    DateTimeOffset ExpiresAt,
    bool EmailSent);
