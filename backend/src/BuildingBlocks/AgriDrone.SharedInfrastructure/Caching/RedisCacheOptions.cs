namespace AgriDrone.SharedInfrastructure.Caching;

public sealed class RedisCacheOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;

    public string InstancePrefix { get; set; } = "agridrone";

    public int PlantReferenceTtlSeconds { get; set; } = 300;

    public int InvalidationEpochTtlSeconds { get; set; } = 2_592_000;
}
