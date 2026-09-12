namespace AgriDrone.Modules.Identity.Application.Options;

public sealed class TenantRegistrationOptions
{
    public const string SectionName = "Identity:TenantRegistration";

    public string LoginUrl { get; init; } = string.Empty;
}
