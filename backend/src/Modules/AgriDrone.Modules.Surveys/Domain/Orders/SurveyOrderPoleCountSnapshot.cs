using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrderPoleCountSnapshot : Entity
{
    private SurveyOrderPoleCountSnapshot() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid? MissionId { get; private set; }
    public Guid? CaptureAttemptId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid? MappingCandidateBatchId { get; private set; }
    public Guid SpatialPolicyVersionId { get; private set; }
    public int VerifiedBillablePoleCount { get; private set; }
    public Guid VerifiedBy { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }
    public string EvidenceFingerprint { get; private set; } = null!;
    public int RevisionNumber { get; private set; }
    public Guid? PredecessorSnapshotId { get; private set; }

    public static SurveyOrderPoleCountSnapshot Create(
        Guid id,
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        Guid? missionId,
        Guid? captureAttemptId,
        Guid farmBaseMapVersionId,
        Guid farmBoundaryVersionId,
        Guid? mappingCandidateBatchId,
        Guid spatialPolicyVersionId,
        int verifiedBillablePoleCount,
        Guid verifiedBy,
        DateTimeOffset verifiedAt,
        int revisionNumber = 1,
        Guid? predecessorSnapshotId = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(farmBaseMapVersionId, nameof(farmBaseMapVersionId));
        DomainGuard.NotEmpty(farmBoundaryVersionId, nameof(farmBoundaryVersionId));
        DomainGuard.NotEmpty(spatialPolicyVersionId, nameof(spatialPolicyVersionId));
        DomainGuard.NotEmpty(verifiedBy, nameof(verifiedBy));
        DomainGuard.Utc(verifiedAt, nameof(verifiedAt));

        if (verifiedBillablePoleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(verifiedBillablePoleCount), "Billable pole count cannot be negative.");
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision number must be >= 1.");
        }

        var fingerprintRaw = string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
            surveyOrderId,
            farmId,
            farmBaseMapVersionId,
            farmBoundaryVersionId,
            mappingCandidateBatchId?.ToString() ?? "NO_BATCH",
            spatialPolicyVersionId,
            verifiedBillablePoleCount,
            revisionNumber,
            verifiedAt.ToUnixTimeMilliseconds());

        var evidenceFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintRaw))).ToLowerInvariant();

        return new SurveyOrderPoleCountSnapshot
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            FarmId = farmId,
            MissionId = missionId,
            CaptureAttemptId = captureAttemptId,
            FarmBaseMapVersionId = farmBaseMapVersionId,
            FarmBoundaryVersionId = farmBoundaryVersionId,
            MappingCandidateBatchId = mappingCandidateBatchId,
            SpatialPolicyVersionId = spatialPolicyVersionId,
            VerifiedBillablePoleCount = verifiedBillablePoleCount,
            VerifiedBy = verifiedBy,
            VerifiedAt = verifiedAt,
            EvidenceFingerprint = evidenceFingerprint,
            RevisionNumber = revisionNumber,
            PredecessorSnapshotId = predecessorSnapshotId
        };
    }
}
