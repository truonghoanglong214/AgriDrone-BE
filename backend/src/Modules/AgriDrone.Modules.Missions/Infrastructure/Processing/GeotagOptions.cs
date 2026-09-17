namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

public sealed class GeotagOptions
{
    public const string SectionName = "Geotag";

    public string PythonPath { get; set; } = "python";

    public string ScriptPath { get; set; } = Path.Combine("scripts", "geotag", "geotag.py");

    public int TimeoutSeconds { get; set; } = 300;

    public double DefaultMaxDiffSeconds { get; set; } = 3.0;
}
