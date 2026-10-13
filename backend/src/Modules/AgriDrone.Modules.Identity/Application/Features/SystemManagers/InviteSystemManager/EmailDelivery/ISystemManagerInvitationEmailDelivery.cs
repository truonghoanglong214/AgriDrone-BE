namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.EmailDelivery;

internal interface ISystemManagerInvitationEmailDelivery
{
    Task DeliverAsync(
        string email,
        string plainTextToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}
