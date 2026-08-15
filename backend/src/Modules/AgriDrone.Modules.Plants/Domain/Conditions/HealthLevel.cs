using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Conditions;

public sealed class HealthLevel : Entity
{
    private HealthLevel()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public int? Rank { get; private set; }

    public bool IsHealthy { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<Plant> CurrentPlants { get; private set; } = [];

    public ICollection<PlantScan> PlantScans { get; private set; } = [];

    public ICollection<ConditionDetection> ConditionDetections { get; private set; } = [];

    public ICollection<ScanVerification> CorrectedScanVerifications { get; private set; } = [];

    public ICollection<ConditionDetectionReview> CorrectedDetectionReviews { get; private set; } = [];
}
