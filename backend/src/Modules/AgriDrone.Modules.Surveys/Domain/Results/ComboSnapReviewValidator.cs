using AgriDrone.IntegrationContracts.Health;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed record ComboSnapTargetPoint(
    Guid PlantProfileId,
    double Latitude,
    double Longitude);

public static class ComboSnapReviewValidator
{
    public const double MaxSnapDistanceMeters = 5.0;

    public static Result Validate(
        ComboAnalysisReadyV1 payload,
        IReadOnlySet<Guid> outsideBoundaryFindingIds,
        IReadOnlyList<ComboFindingReviewDecisionRecord> findingDecisions,
        IReadOnlyDictionary<Guid, ComboSnapTargetPoint> snapTargetsByProfile,
        IReadOnlySet<Guid> allowedMedia,
        double maxSnapDistanceMeters = MaxSnapDistanceMeters)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(outsideBoundaryFindingIds);
        ArgumentNullException.ThrowIfNull(findingDecisions);
        ArgumentNullException.ThrowIfNull(snapTargetsByProfile);
        ArgumentNullException.ThrowIfNull(allowedMedia);

        var findingsById = payload.Findings.ToDictionary(f => f.FindingId);
        var decisionByFinding = new Dictionary<Guid, ComboFindingReviewDecisionRecord>();
        foreach (var fd in findingDecisions)
        {
            decisionByFinding.TryAdd(fd.FindingId, fd);
        }

        // 1. Validate individual snapped findings against all SNAP prerequisites
        foreach (var fd in findingDecisions)
        {
            if (!fd.ReviewedPlantProfileId.HasValue)
            {
                continue;
            }

            if (!string.Equals(
                    SurveyComboReviewRevision.CanonicalizeFindingDecision(fd.Decision),
                    SurveyComboReviewRevision.FindingDecisionAccept,
                    StringComparison.Ordinal))
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.SnapRequiresAccept",
                    $"Finding '{fd.FindingId}' must have Decision 'Accept' when ReviewedPlantProfileId is provided."));
            }

            if (string.IsNullOrWhiteSpace(fd.Reason))
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.SnapReasonRequired",
                    $"Snapping finding '{fd.FindingId}' to a pole requires a non-empty reason."));
            }

            if (fd.EvidenceMediaIds is null || fd.EvidenceMediaIds.Count == 0)
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.SnapEvidenceRequired",
                    $"Snapping finding '{fd.FindingId}' to a pole requires supporting evidence media."));
            }

            if (fd.EvidenceMediaIds.Count > 128 ||
                fd.EvidenceMediaIds.Contains(Guid.Empty) ||
                fd.EvidenceMediaIds.Distinct().Count() != fd.EvidenceMediaIds.Count)
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.InvalidEvidence",
                    "EvidenceMediaIds must contain unique non-empty GUIDs (max 128)."));
            }

            if (fd.EvidenceMediaIds.Any(id => !allowedMedia.Contains(id)))
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.ForeignEvidence",
                    "Every evidence media reference must belong to the source Mission/Tenant/Farm."));
            }

            var targetProfileId = fd.ReviewedPlantProfileId.Value;
            if (targetProfileId == Guid.Empty || !snapTargetsByProfile.TryGetValue(targetProfileId, out var target))
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.SnapTargetNotFound",
                    $"Target plant profile '{targetProfileId}' is not an active verified pole in this order's base map version."));
            }

            if (!findingsById.TryGetValue(fd.FindingId, out var finding))
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.UnknownFinding",
                    $"Finding decision references '{fd.FindingId}', which does not belong to this COMBO batch."));
            }

            if (outsideBoundaryFindingIds.Contains(finding.FindingId))
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.SnapOutsideBoundary",
                    $"Out-of-boundary finding '{finding.FindingId}' cannot be snapped to a pole."));
            }

            var distanceMeters = CalculateHaversineMeters(
                finding.Position.Coordinates[1],
                finding.Position.Coordinates[0],
                target.Latitude,
                target.Longitude);

            if (distanceMeters > maxSnapDistanceMeters)
            {
                return Result.Failure(AppError.Validation(
                    "ComboReview.SnapTooFar",
                    $"Finding '{finding.FindingId}' is {distanceMeters:F2}m away from pole '{targetProfileId}', exceeding the {maxSnapDistanceMeters:F1}m snap threshold."));
            }
        }

        // 2. Enforce uniqueness of (effectiveProfileId, suspectedClassCode) whenever a SNAP participates
        var claimsByProfileAndClass = new Dictionary<(Guid ProfileId, string ClassCode), (Guid FindingId, bool IsSnapped)>();
        foreach (var fd in findingDecisions)
        {
            if (!string.Equals(
                    SurveyComboReviewRevision.CanonicalizeFindingDecision(fd.Decision),
                    SurveyComboReviewRevision.FindingDecisionAccept,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!findingsById.TryGetValue(fd.FindingId, out var finding))
            {
                continue;
            }

            var effectiveProfileId = fd.ReviewedPlantProfileId ?? finding.PlantProfileId;
            if (!effectiveProfileId.HasValue || effectiveProfileId.Value == Guid.Empty)
            {
                continue;
            }

            var normalizedClass = ComboAnalysisV1Values.NormalizeSuspectedClassCode(finding.SuspectedClassCode);
            var key = (effectiveProfileId.Value, normalizedClass);
            var isSnapped = fd.ReviewedPlantProfileId.HasValue;

            if (claimsByProfileAndClass.TryGetValue(key, out var existing))
            {
                if (isSnapped || existing.IsSnapped)
                {
                    return Result.Failure(AppError.Validation(
                        "ComboReview.SnapConflict",
                        $"Multiple accepted '{normalizedClass}' findings ('{existing.FindingId}' and '{fd.FindingId}') cannot be assigned to the same pole '{effectiveProfileId.Value}'."));
                }
            }
            else
            {
                claimsByProfileAndClass[key] = (fd.FindingId, isSnapped);
            }
        }

        return Result.Success();
    }

    public static bool IsIdentityBlockerResolvedBySnap(
        ComboBlockerResolutionRecord blocker,
        IReadOnlyDictionary<Guid, ComboFindingReviewDecisionRecord> findingDecisionById)
    {
        if (!string.Equals(blocker.SourceType.Trim(), "FINDING_IDENTITY", StringComparison.Ordinal) ||
            !string.Equals(
                SurveyComboReviewRevision.CanonicalizeBlockerResolution(blocker.Resolution),
                SurveyComboReviewRevision.BlockerResolutionCorrected,
                StringComparison.Ordinal))
        {
            return false;
        }

        return Guid.TryParse(blocker.SourceId.Trim(), out var findingId) &&
            findingDecisionById.TryGetValue(findingId, out var fd) &&
            string.Equals(
                SurveyComboReviewRevision.CanonicalizeFindingDecision(fd.Decision),
                SurveyComboReviewRevision.FindingDecisionAccept,
                StringComparison.Ordinal) &&
            fd.ReviewedPlantProfileId.HasValue;
    }

    public static double CalculateHaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return r * c;
    }
}
