namespace AgriDrone.Modules.Identity.Application.Options;

public sealed class RefreshSessionOptions
{
    public const string SectionName = "RefreshSession";

    public string CookieName { get; set; } = "agridrone_refresh";

    public int IdleLifetimeDays { get; set; } = 7;

    public int AbsoluteLifetimeDays { get; set; } = 30;

    public string[] AllowedOrigins { get; set; } = [];
}
