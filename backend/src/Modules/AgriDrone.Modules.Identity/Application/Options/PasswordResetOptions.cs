namespace AgriDrone.Modules.Identity.Application.Options;

public sealed class PasswordResetOptions
{
    public const string SectionName = "Identity:PasswordReset";

    public string ResetUrl { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; } = 30;
}
