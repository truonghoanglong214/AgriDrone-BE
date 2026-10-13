namespace AgriDrone.Api.Legacy;

public sealed class LegacyFeaturesOptions
{
    public const string SectionName = "LegacyFeatures";

    /// <summary>
    /// Emergency rollback switch. It must remain false in production-like
    /// environments; enabling it reopens every endpoint marked as legacy.
    /// </summary>
    public bool EnableDeprecatedEndpoints { get; init; }
}
