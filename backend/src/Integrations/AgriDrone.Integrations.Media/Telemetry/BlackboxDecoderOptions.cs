namespace AgriDrone.Integrations.Media.Telemetry;

public sealed class BlackboxDecoderOptions
{
    public const string SectionName = "Telemetry:BlackboxDecoder";

    public string ExecutablePath { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public long MaximumFileBytes { get; set; } = 20 * 1024 * 1024;
}