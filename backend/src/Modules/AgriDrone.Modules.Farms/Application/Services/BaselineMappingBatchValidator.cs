using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Services;

public sealed record MappingBatchValidationIssue(
    Guid? CandidateId,
    string Code,
    string Message,
    string Severity); // "Error" | "Warning"

public sealed record BaselineMappingBatchValidationResult(
    bool IsValid,
    int TotalCandidates,
    int AcceptedCount,
    int RejectedCount,
    int RelocatedCount,
    int ReplacedCount,
    int PendingCount,
    int ConfirmedPublishablePoleCount,
    IReadOnlyList<MappingBatchValidationIssue> Issues);

public interface IBaselineMappingBatchValidator
{
    BaselineMappingBatchValidationResult Validate(
        MappingCandidateBatch batch,
        FarmBoundary? boundary);
}

public sealed class BaselineMappingBatchValidator : IBaselineMappingBatchValidator
{
    public BaselineMappingBatchValidationResult Validate(
        MappingCandidateBatch batch,
        FarmBoundary? boundary)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var issues = new List<MappingBatchValidationIssue>();

        var candidates = batch.Candidates.ToList();
        var activeCandidates = candidates
            .Where(c => c.ReviewDecision != MappingCandidateReviewDecision.Reject)
            .ToList();

        var boundaryPoly = boundary?.Geometry;

        // 1. Boundary classification and coverage
        foreach (var c in activeCandidates)
        {
            var pos = c.CorrectedPosition ?? c.OriginalPosition;

            if (boundaryPoly is not null)
            {
                var isInside = boundaryPoly.Covers(pos) || boundaryPoly.Intersects(pos);
                if (!isInside)
                {
                    if (c.ReviewDecision == MappingCandidateReviewDecision.Pending)
                    {
                        issues.Add(new MappingBatchValidationIssue(
                            c.CandidateId,
                            "OutsideBoundaryUnresolved",
                            $"Candidate {c.CandidateId} lies outside the farm boundary and must be reviewed (Rejected or Relocated).",
                            "Error"));
                    }
                    else
                    {
                        issues.Add(new MappingBatchValidationIssue(
                            c.CandidateId,
                            "OutsideBoundaryInvalidDecision",
                            $"Candidate {c.CandidateId} position remains outside the farm boundary despite '{c.ReviewDecision}' decision.",
                            "Error"));
                    }
                }
            }

            if (c.ReviewDecision == MappingCandidateReviewDecision.Replace &&
                !c.CorrectedMatchedPlantId.HasValue && !c.ProposedMatchedPlantId.HasValue)
            {
                issues.Add(new MappingBatchValidationIssue(
                    c.CandidateId,
                    "ReplaceMissingTargetPlant",
                    $"Candidate {c.CandidateId} has Replace decision but lacks an existing target plant ID.",
                    "Error"));
            }

            if (c.BoundaryClassification == "NEEDS_REVIEW" && c.ReviewDecision == MappingCandidateReviewDecision.Pending)
            {
                issues.Add(new MappingBatchValidationIssue(
                    c.CandidateId,
                    "NeedsReviewPending",
                    $"Candidate {c.CandidateId} is flagged as NEEDS_REVIEW by AI model and requires manual decision.",
                    "Warning"));
            }
        }

        // 2. Minimum distance check (detect duplicates < 0.3m)
        for (var i = 0; i < activeCandidates.Count; i++)
        {
            var p1 = activeCandidates[i].CorrectedPosition ?? activeCandidates[i].OriginalPosition;
            for (var j = i + 1; j < activeCandidates.Count; j++)
            {
                var p2 = activeCandidates[j].CorrectedPosition ?? activeCandidates[j].OriginalPosition;
                var distance = DistanceMeters(p1, p2);
                if (distance < 0.3)
                {
                    issues.Add(new MappingBatchValidationIssue(
                        activeCandidates[i].CandidateId,
                        "DuplicateCandidatePosition",
                        $"Candidates {activeCandidates[i].CandidateId} and {activeCandidates[j].CandidateId} are too close ({distance:F2}m < 0.3m).",
                        "Error"));
                }
            }
        }

        var total = candidates.Count;
        var accepted = candidates.Count(c => c.ReviewDecision is MappingCandidateReviewDecision.Accept or MappingCandidateReviewDecision.CreateNew);
        var rejected = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Reject);
        var relocated = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Relocate);
        var replaced = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Replace);
        var pending = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending);

        // Confirmed poles that will be published:
        var confirmedPublishable = candidates.Count(c =>
            c.ReviewDecision is MappingCandidateReviewDecision.Accept
                or MappingCandidateReviewDecision.Relocate
                or MappingCandidateReviewDecision.Replace
            || (c.ReviewDecision == MappingCandidateReviewDecision.Pending && c.BoundaryClassification == "IN_BOUNDARY"));

        var isValid = issues.All(i => i.Severity != "Error");

        return new BaselineMappingBatchValidationResult(
            IsValid: isValid,
            TotalCandidates: total,
            AcceptedCount: accepted,
            RejectedCount: rejected,
            RelocatedCount: relocated,
            ReplacedCount: replaced,
            PendingCount: pending,
            ConfirmedPublishablePoleCount: confirmedPublishable,
            Issues: issues);
    }

    private static double DistanceMeters(Point p1, Point p2)
    {
        const double r = 6371000;
        var lat1 = p1.Y * Math.PI / 180.0;
        var lat2 = p2.Y * Math.PI / 180.0;
        var dLat = (p2.Y - p1.Y) * Math.PI / 180.0;
        var dLon = (p2.X - p1.X) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }
}
