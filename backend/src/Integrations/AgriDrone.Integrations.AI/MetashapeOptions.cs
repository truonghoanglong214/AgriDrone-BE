namespace AgriDrone.Integrations.AI;

public sealed class MetashapeOptions
{
    public const string SectionName = "Metashape";

    public string Endpoint { get; set; } = "http://localhost:8000";

    public int TimeoutMinutes { get; set; } = 180;
}

