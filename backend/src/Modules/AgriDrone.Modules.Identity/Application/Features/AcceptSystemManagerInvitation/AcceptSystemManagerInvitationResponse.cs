namespace AgriDrone.Modules.Identity.Application.Features.AcceptSystemManagerInvitation;

public sealed record AcceptSystemManagerInvitationResponse(
    Guid UserId,
    Guid ProfileId,
    bool AccountCreated);