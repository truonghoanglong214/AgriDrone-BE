namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

public sealed class NodeOdmOptions
{
    public const string SectionName = "NodeODM";

    public string Endpoint { get; set; } = "http://localhost:3000";

    public string CameraLens { get; set; } = "fisheye";

    public string FeatureQuality { get; set; } = "medium";

    public int ResizeTo { get; set; } = 2048;

    public string PcQuality { get; set; } = "lowest";

    public int MaxConcurrency { get; set; } = 4;

    public bool FastOrthophoto { get; set; }

    public int TimeoutMinutes { get; set; } = 120;
}
