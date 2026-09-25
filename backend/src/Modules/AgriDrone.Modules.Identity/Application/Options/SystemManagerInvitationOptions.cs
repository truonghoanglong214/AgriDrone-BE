namespace AgriDrone.Modules.Identity.Application.Options;

public sealed class SystemManagerInvitationOptions
{
    public const string SectionName =
        "Identity:SystemManagerInvitations";

    public string AcceptUrl { get; init; } = string.Empty;

    public int ExpirationHours { get; init; } = 24;
}