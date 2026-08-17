namespace AgriDrone.Modules.Identity.Application.Contracts.PasswordReset;

public sealed record PasswordResetTokenResult(
    string PlainTextToken,
    string TokenHash);
