using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Verifications;

public sealed class ConditionDetectionReview : Entity
{
    private ConditionDetectionReview()
    {
    }

    public Guid ScanVerificationId { get; private set; }

    public Guid PlantScanId { get; private set; }

    public Guid ConditionDetectionId { get; private set; }

    public ConditionReviewDecision Decision { get; private set; }

    public Guid? CorrectedConditionId { get; private set; }

    public Guid? CorrectedSeverityLevelId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
