namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers;

public sealed record InviteSystemManagerResponse(
    Guid UserId,
    Guid ProfileId,
    string Email,
    DateTimeOffset ExpiresAt,
    bool EmailSent);