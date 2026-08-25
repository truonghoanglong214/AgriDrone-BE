namespace AgriDrone.Modules.Identity.Application.Options;

public sealed class SystemAdminBootstrapOptions
{
    public const string SectionName = "Identity:SystemAdminBootstrap";

    public bool Enabled { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = "System Administrator";
}
