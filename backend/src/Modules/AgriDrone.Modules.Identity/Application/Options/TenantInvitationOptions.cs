namespace AgriDrone.Modules.Identity.Application.Options;

public sealed class TenantInvitationOptions
{
    public const string SectionName = "Identity:TenantInvitations";

    public string AcceptUrl { get; init; } = string.Empty;

    public int ExpirationHours { get; init; } = 24;
}
