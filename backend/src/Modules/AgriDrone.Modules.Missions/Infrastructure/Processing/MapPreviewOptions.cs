namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

public sealed class MapPreviewOptions
{
    public const string SectionName = "MapPreview";

    public string PythonPath { get; set; } = "python";

    public string ScriptPath { get; set; } = "scripts/map/export_map_preview.py";

    public int TimeoutSeconds { get; set; } = 120;
}
