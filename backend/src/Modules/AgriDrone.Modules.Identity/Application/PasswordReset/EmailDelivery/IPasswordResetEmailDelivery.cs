namespace AgriDrone.Modules.Identity.Application.PasswordReset.EmailDelivery;

internal interface IPasswordResetEmailDelivery
{
    Task DeliverAsync(
        string email,
        string fullName,
        string plainTextToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}
