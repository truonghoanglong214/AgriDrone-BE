using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrderPoleCountSnapshot : Entity
{
    private readonly List<SurveyOrderPoleCountSnapshotItem> _items = [];

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

    public IReadOnlyCollection<SurveyOrderPoleCountSnapshotItem> Items => _items.AsReadOnly();

    public static string ComputeEvidenceFingerprint(
        Guid surveyOrderId,
        Guid farmId,
        Guid? missionId,
        Guid? captureAttemptId,
        Guid farmBaseMapVersionId,
        Guid farmBoundaryVersionId,
        Guid? mappingCandidateBatchId,
        string spatialPolicyFingerprint,
        IEnumerable<SurveyOrderPoleCountSnapshotItem> items)
    {
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(farmBaseMapVersionId, nameof(farmBaseMapVersionId));
        DomainGuard.NotEmpty(farmBoundaryVersionId, nameof(farmBoundaryVersionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(spatialPolicyFingerprint, nameof(spatialPolicyFingerprint));
        ArgumentNullException.ThrowIfNull(items, nameof(items));

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{surveyOrderId:D}|{farmId:D}|");
        sb.Append(CultureInfo.InvariantCulture, $"{missionId?.ToString("D") ?? "NO_MISSION"}|");
        sb.Append(CultureInfo.InvariantCulture, $"{captureAttemptId?.ToString("D") ?? "NO_ATTEMPT"}|");
        sb.Append(CultureInfo.InvariantCulture, $"{farmBaseMapVersionId:D}|{farmBoundaryVersionId:D}|");
        sb.Append(CultureInfo.InvariantCulture, $"{mappingCandidateBatchId?.ToString("D") ?? "NO_BATCH"}|");
        sb.Append(CultureInfo.InvariantCulture, $"{spatialPolicyFingerprint.Trim()};");

        var sortedItems = items.OrderBy(x => x.MappingCandidateId).ToList();
        foreach (var item in sortedItems)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"{item.MappingCandidateId:D}:{item.ReviewDecision}:{item.EffectivePosition.X:F7},{item.EffectivePosition.Y:F7}:" +
                $"{(item.PlantProfileId.HasValue ? item.PlantProfileId.Value.ToString("D") : "NO_PROFILE")}:" +
                $"{item.IsAccepted}:{item.IsStrictlyInBoundary}:{item.ExclusionReason ?? "NONE"};");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

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
        string evidenceFingerprint,
        IReadOnlyList<SurveyOrderPoleCountSnapshotItem> items,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceFingerprint, nameof(evidenceFingerprint));
        ArgumentNullException.ThrowIfNull(items, nameof(items));
        DomainGuard.NotEmpty(verifiedBy, nameof(verifiedBy));
        DomainGuard.Utc(verifiedAt, nameof(verifiedAt));

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision number must be >= 1.");
        }

        foreach (var item in items)
        {
            if (item.TenantId != tenantId || item.FarmId != farmId)
            {
                throw new InvalidOperationException("Snapshot item tenant or farm does not match snapshot header.");
            }
        }

        var verifiedBillablePoleCount = items
            .Where(x => x.IsAccepted && x.IsStrictlyInBoundary && x.PlantProfileId.HasValue)
            .Select(x => x.PlantProfileId!.Value)
            .Distinct()
            .Count();

        var snapshot = new SurveyOrderPoleCountSnapshot
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
            EvidenceFingerprint = evidenceFingerprint.Trim().ToLowerInvariant(),
            RevisionNumber = revisionNumber,
            PredecessorSnapshotId = predecessorSnapshotId
        };

        snapshot._items.AddRange(items);
        return snapshot;
    }
}
