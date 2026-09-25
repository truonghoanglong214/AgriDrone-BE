namespace AgriDrone.Modules.Identity.Application.Features.PreviewSystemManagerInvitation;

public sealed record PreviewSystemManagerInvitationResponse(
    string MaskedEmail,
    string Role,
    DateTimeOffset ExpiresAt,
    bool RequiresAccountCreation);