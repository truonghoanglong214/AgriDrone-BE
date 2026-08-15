using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Verifications;
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

    public Plant Plant { get; private set; } = null!;

    public HealthLevel OverallHealthLevel { get; private set; } = null!;

    public PlantScan? VerificationOfScan { get; private set; }

    public ICollection<PlantScan> VerificationScans { get; private set; } = [];

    public ICollection<PlantScanMedia> Media { get; private set; } = [];

    public ICollection<ConditionDetection> ConditionDetections { get; private set; } = [];

    public ICollection<ScanVerification> Verifications { get; private set; } = [];
}
