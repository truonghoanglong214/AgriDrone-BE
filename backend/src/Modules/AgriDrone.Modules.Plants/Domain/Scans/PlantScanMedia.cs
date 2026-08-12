namespace AgriDrone.Modules.Plants.Domain.Scans;

public sealed class PlantScanMedia
{
    private PlantScanMedia()
    {
    }

    public Guid PlantScanId { get; private set; }

    public Guid MediaId { get; private set; }

    public ScanMediaRole MediaRole { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
