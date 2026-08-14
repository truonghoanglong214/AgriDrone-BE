using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Verifications;

public sealed class ScanVerification : Entity
{
    private ScanVerification()
    {
    }

    public Guid PlantScanId { get; private set; }

    public Guid UserId { get; private set; }

    public VerificationDecision Decision { get; private set; }

    public Guid? CorrectedHealthLevelId { get; private set; }

    public string? Note { get; private set; }

    public int RevisionNumber { get; private set; }

    public Guid? SupersedesVerificationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
