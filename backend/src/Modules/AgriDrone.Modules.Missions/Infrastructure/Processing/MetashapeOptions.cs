namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

public sealed class MetashapeOptions
{
    public const string SectionName = "Metashape";

    public string Endpoint { get; set; } = "http://localhost:8000";

    public string CameraType { get; set; } = "equidistant_fisheye";

    public string AlignAccuracy { get; set; } = "medium";

    public bool GenericPreselection { get; set; } = true;

    public int TimeoutMinutes { get; set; } = 180;
}
