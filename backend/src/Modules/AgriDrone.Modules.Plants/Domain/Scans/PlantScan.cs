using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Scans;

public sealed class PlantScan : Entity
{
    private PlantScan()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid PlantId { get; private set; }

    public Guid? MissionId { get; private set; }

    public Guid? AiJobId { get; private set; }

    public Guid? VerificationOfScanId { get; private set; }

    public Guid? SourceTaskId { get; private set; }

    public Guid? ClientOperationId { get; private set; }

    public DateTimeOffset ObservedAt { get; private set; }

    public ScanSource Source { get; private set; }

    public Guid OverallHealthLevelId { get; private set; }

    public decimal? OverallConfidence { get; private set; }

    public string? Notes { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? DeviceCreatedAt { get; private set; }

    public DateTimeOffset ServerReceivedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
