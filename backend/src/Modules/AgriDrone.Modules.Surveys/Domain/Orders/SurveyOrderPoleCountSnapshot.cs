using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrderPoleCountSnapshot : Entity
{
    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);
    private readonly List<SurveyOrderPoleCountSnapshotItem> _items = [];

    private SurveyOrderPoleCountSnapshot() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid CaptureAttemptId { get; private set; }
    public Guid FarmBaseMapVersionId { get; private set; }
    public Guid FarmBoundaryVersionId { get; private set; }
    public Guid MappingCandidateBatchId { get; private set; }
    public Guid SpatialPolicyVersionId { get; private set; }
    public int VerifiedBillablePoleCount { get; private set; }
    public Guid VerifiedBy { get; private set; }
    public DateTimeOffset VerifiedAt { get; private set; }
    public string EvidenceFingerprint { get; private set; } = null!;
    public int RevisionNumber { get; private set; }
    public Guid? PredecessorSnapshotId { get; private set; }

    public IReadOnlyCollection<SurveyOrderPoleCountSnapshotItem> Items => _items.AsReadOnly();

    public static string ComputeEvidenceFingerprint(
        Guid tenantId,
        Guid surveyOrderId,
        Guid farmId,
        Guid missionId,
        Guid captureAttemptId,
        Guid farmBaseMapVersionId,
        Guid farmBoundaryVersionId,
        Guid mappingCandidateBatchId,
        string spatialPolicyFingerprint,
        IEnumerable<SurveyOrderPoleCountSnapshotItem> items)
    {
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(missionId, nameof(missionId));
        DomainGuard.NotEmpty(captureAttemptId, nameof(captureAttemptId));
        DomainGuard.NotEmpty(farmBaseMapVersionId, nameof(farmBaseMapVersionId));
        DomainGuard.NotEmpty(farmBoundaryVersionId, nameof(farmBoundaryVersionId));
        DomainGuard.NotEmpty(mappingCandidateBatchId, nameof(mappingCandidateBatchId));
        ArgumentException.ThrowIfNullOrWhiteSpace(spatialPolicyFingerprint, nameof(spatialPolicyFingerprint));
        ArgumentNullException.ThrowIfNull(items, nameof(items));

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{tenantId:D}|{surveyOrderId:D}|{farmId:D}|");
        sb.Append(CultureInfo.InvariantCulture, $"{missionId:D}|{captureAttemptId:D}|");
        sb.Append(CultureInfo.InvariantCulture, $"{farmBaseMapVersionId:D}|{farmBoundaryVersionId:D}|");
        sb.Append(CultureInfo.InvariantCulture, $"{mappingCandidateBatchId:D}|");
        sb.Append(CultureInfo.InvariantCulture, $"{spatialPolicyFingerprint.Trim()};");

        var sortedItems = items.OrderBy(x => x.SourceCandidateId).ToList();
        foreach (var item in sortedItems)
        {
            var decision = item.ReviewDecision.Trim();
            if (!item.IsAccepted)
            {
                var normReason = (item.ExclusionReason?.Trim() ?? "REJECTED").ToUpperInvariant();
                sb.Append(CultureInfo.InvariantCulture, $"EXCLUDED|{item.SourceCandidateId:D}|{normReason};");
            }
            else if (decision == "CreateNew")
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"NEW|{item.SourceCandidateId:D}|{item.EffectivePosition.X:F7},{item.EffectivePosition.Y:F7}|{item.EffectiveRowIndex}|{item.EffectiveColumnIndex};");
            }
            else if (decision == "MatchExisting")
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"MATCH|{item.SourceCandidateId:D}|{item.TargetPlantId:D}|{item.TargetPlantProfileId:D}|{item.EffectivePosition.X:F7},{item.EffectivePosition.Y:F7};");
            }
            else if (decision == "Relocate")
            {
                var prev = item.PreviousPosition ?? item.EffectivePosition;
                sb.Append(CultureInfo.InvariantCulture,
                    $"RELOCATE|{item.SourceCandidateId:D}|{item.TargetPlantId:D}|{item.TargetPlantProfileId:D}|{prev.X:F7},{prev.Y:F7}|{item.EffectivePosition.X:F7},{item.EffectivePosition.Y:F7}|{item.EffectiveRowIndex}|{item.EffectiveColumnIndex};");
            }
            else if (decision == "Replace")
            {
                var prev = item.PreviousPosition ?? item.EffectivePosition;
                sb.Append(CultureInfo.InvariantCulture,
                    $"REPLACE|{item.SourceCandidateId:D}|{item.TargetPlantId:D}|{item.TargetPlantProfileId:D}|{prev.X:F7},{prev.Y:F7}|{item.EffectivePosition.X:F7},{item.EffectivePosition.Y:F7}|{item.EffectiveRowIndex}|{item.EffectiveColumnIndex};");
            }
            else
            {
                var normReason = (item.ExclusionReason?.Trim() ?? decision).ToUpperInvariant();
                sb.Append(CultureInfo.InvariantCulture, $"EXCLUDED|{item.SourceCandidateId:D}|{normReason};");
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    public static SurveyOrderPoleCountSnapshot Create(
        Guid id,
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid captureAttemptId,
        Guid farmBaseMapVersionId,
        Guid farmBoundaryVersionId,
        Guid mappingCandidateBatchId,
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
        DomainGuard.NotEmpty(missionId, nameof(missionId));
        DomainGuard.NotEmpty(captureAttemptId, nameof(captureAttemptId));
        DomainGuard.NotEmpty(farmBaseMapVersionId, nameof(farmBaseMapVersionId));
        DomainGuard.NotEmpty(farmBoundaryVersionId, nameof(farmBoundaryVersionId));
        DomainGuard.NotEmpty(mappingCandidateBatchId, nameof(mappingCandidateBatchId));
        DomainGuard.NotEmpty(spatialPolicyVersionId, nameof(spatialPolicyVersionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceFingerprint, nameof(evidenceFingerprint));
        ArgumentNullException.ThrowIfNull(items, nameof(items));
        DomainGuard.NotEmpty(verifiedBy, nameof(verifiedBy));
        DomainGuard.Utc(verifiedAt, nameof(verifiedAt));

        var cleanFingerprint = evidenceFingerprint.Trim().ToLowerInvariant();
        if (!Hex64Regex.IsMatch(cleanFingerprint))
        {
            throw new ArgumentException("Evidence fingerprint must be a 64-character lowercase hex string.", nameof(evidenceFingerprint));
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision number must be >= 1.");
        }

        foreach (var item in items)
        {
            if (item.SnapshotId != id)
            {
                throw new InvalidOperationException($"Snapshot item {item.Id} SnapshotId must match snapshot Id.");
            }

            if (item.TenantId != tenantId || item.FarmId != farmId)
            {
                throw new InvalidOperationException($"Snapshot item {item.Id} tenant or farm does not match snapshot header.");
            }

            if (item.MappingCandidateBatchId != mappingCandidateBatchId)
            {
                throw new InvalidOperationException($"Snapshot item {item.Id} batch does not match snapshot MappingCandidateBatchId.");
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
            EvidenceFingerprint = cleanFingerprint,
            RevisionNumber = revisionNumber,
            PredecessorSnapshotId = predecessorSnapshotId
        };

        snapshot._items.AddRange(items);
        return snapshot;
    }
}
