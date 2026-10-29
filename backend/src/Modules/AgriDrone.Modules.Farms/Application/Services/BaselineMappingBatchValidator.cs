using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Services;

public sealed class BaselineMappingBatchValidator : IBaselineMappingBatchValidator
{
    private const double DuplicateDistanceThresholdMeters = 0.3;
    private const double GridCellSizeMeters = 0.5;
    private const double EarthRadiusMeters = 6371000.0;
    private const double MetersPerDegreeLat = 111139.0;

    public BaselineMappingBatchValidationResult Validate(
        MappingCandidateBatch batch,
        FarmBoundary? boundary)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var issues = new List<MappingBatchValidationIssue>();
        var candidates = batch.Candidates.ToList();
        var boundaryPoly = boundary?.Geometry;

        // 1. Candidate validation (Boundary, Decision, Target Plant, Pending checks)
        foreach (var c in candidates)
        {
            var pos = c.CorrectedPosition ?? c.OriginalPosition;

            // Flag Pending candidates as blocking errors for publication
            if (c.ReviewDecision == MappingCandidateReviewDecision.Pending)
            {
                issues.Add(new MappingBatchValidationIssue(
                    c.CandidateId,
                    "PendingReviewRemaining",
                    $"Candidate {c.CandidateId} is still Pending and requires an explicit review decision before publication.",
                    "Error"));
            }

            // Flag NEEDS_REVIEW AI classification
            if (c.BoundaryClassification == "NEEDS_REVIEW" && c.ReviewDecision == MappingCandidateReviewDecision.Pending)
            {
                issues.Add(new MappingBatchValidationIssue(
                    c.CandidateId,
                    "NeedsReviewPending",
                    $"Candidate {c.CandidateId} is flagged as NEEDS_REVIEW by AI model and requires manual decision.",
                    "Error"));
            }

            if (c.ReviewDecision != MappingCandidateReviewDecision.Reject)
            {
                // Boundary check
                if (boundaryPoly is not null)
                {
                    var isInside = boundaryPoly.Covers(pos) || boundaryPoly.Intersects(pos);
                    if (!isInside)
                    {
                        issues.Add(new MappingBatchValidationIssue(
                            c.CandidateId,
                            "OutsideBoundaryInvalidDecision",
                            $"Candidate {c.CandidateId} position remains outside the farm boundary despite '{c.ReviewDecision}' decision.",
                            "Error"));
                    }
                }

                // Relocate check: requires corrected position and target plant
                if (c.ReviewDecision == MappingCandidateReviewDecision.Relocate)
                {
                    if (c.CorrectedPosition is null)
                    {
                        issues.Add(new MappingBatchValidationIssue(
                            c.CandidateId,
                            "RelocateMissingPosition",
                            $"Candidate {c.CandidateId} has Relocate decision but lacks corrected GPS coordinates.",
                            "Error"));
                    }

                    if (!c.CorrectedMatchedPlantId.HasValue && !c.ProposedMatchedPlantId.HasValue)
                    {
                        issues.Add(new MappingBatchValidationIssue(
                            c.CandidateId,
                            "RelocateMissingTargetPlant",
                            $"Candidate {c.CandidateId} has Relocate decision but lacks target plant ID to update.",
                            "Error"));
                    }
                }

                // MatchExisting check: requires target plant
                if (c.ReviewDecision == MappingCandidateReviewDecision.MatchExisting &&
                    !c.CorrectedMatchedPlantId.HasValue && !c.ProposedMatchedPlantId.HasValue)
                {
                    issues.Add(new MappingBatchValidationIssue(
                        c.CandidateId,
                        "MatchExistingMissingTargetPlant",
                        $"Candidate {c.CandidateId} has MatchExisting decision but lacks target plant ID.",
                        "Error"));
                }

                // Replace check: requires target plant
                if (c.ReviewDecision == MappingCandidateReviewDecision.Replace &&
                    !c.CorrectedMatchedPlantId.HasValue && !c.ProposedMatchedPlantId.HasValue)
                {
                    issues.Add(new MappingBatchValidationIssue(
                        c.CandidateId,
                        "ReplaceMissingTargetPlant",
                        $"Candidate {c.CandidateId} has Replace decision but lacks an existing target plant ID to replace.",
                        "Error"));
                }
            }
        }

        var activeCandidates = candidates
            .Where(c => c.ReviewDecision != MappingCandidateReviewDecision.Reject)
            .ToList();

        // 2. Grid Position Unique Check within the farm base map
        var gridPositions = new HashSet<(int Row, int Col)>();
        foreach (var c in activeCandidates)
        {
            var row = c.CorrectedRowIndex ?? c.ProposedRowIndex;
            var col = c.CorrectedColumnIndex ?? c.ProposedColumnIndex;
            if (row.HasValue && col.HasValue)
            {
                if (!gridPositions.Add((row.Value, col.Value)))
                {
                    issues.Add(new MappingBatchValidationIssue(
                        c.CandidateId,
                        "DuplicateGridPositionInFarmMap",
                        $"Candidate {c.CandidateId} has duplicate farm grid position (Row: {row.Value}, Col: {col.Value}).",
                        "Error"));
                }
            }
        }

        // 3. Minimum distance duplicate check using Spatial Grid Bucketing: O(N)
        ValidateMinimumDistancesSpatialBucketing(activeCandidates, issues);

        var total = candidates.Count;
        var accepted = candidates.Count(c => c.ReviewDecision is MappingCandidateReviewDecision.CreateNew or MappingCandidateReviewDecision.MatchExisting);
        var rejected = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Reject);
        var relocated = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Relocate);
        var replaced = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Replace);
        var pending = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending);

        // Confirmed poles that will be published (strictly terminal accepted decisions)
        var confirmedPublishable = candidates.Count(c =>
            c.ReviewDecision is MappingCandidateReviewDecision.CreateNew
                or MappingCandidateReviewDecision.MatchExisting
                or MappingCandidateReviewDecision.Relocate
                or MappingCandidateReviewDecision.Replace);

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

    private static void ValidateMinimumDistancesSpatialBucketing(
        List<MappingCandidate> activeCandidates,
        List<MappingBatchValidationIssue> issues)
    {
        if (activeCandidates.Count <= 1) return;

        // Reference latitude for longitude distance projection
        var avgLat = activeCandidates.Average(c => (c.CorrectedPosition ?? c.OriginalPosition).Y);
        var radLat = avgLat * Math.PI / 180.0;
        var metersPerDegreeLon = EarthRadiusMeters * Math.Cos(radLat) * (Math.PI / 180.0);

        var buckets = new Dictionary<(int X, int Y), List<(Guid CandidateId, Point Pos)>>();

        foreach (var c in activeCandidates)
        {
            var pos = c.CorrectedPosition ?? c.OriginalPosition;
            var cellX = (int)Math.Floor((pos.X * metersPerDegreeLon) / GridCellSizeMeters);
            var cellY = (int)Math.Floor((pos.Y * MetersPerDegreeLat) / GridCellSizeMeters);

            var key = (cellX, cellY);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = [];
                buckets[key] = list;
            }

            // Check neighbor cells (-1, 0, 1 in both dimensions)
            var reported = false;
            for (var dx = -1; dx <= 1 && !reported; dx++)
            {
                for (var dy = -1; dy <= 1 && !reported; dy++)
                {
                    if (buckets.TryGetValue((cellX + dx, cellY + dy), out var neighborList))
                    {
                        foreach (var other in neighborList)
                        {
                            var dist = DistanceMeters(pos, other.Pos);
                            if (dist < DuplicateDistanceThresholdMeters)
                            {
                                issues.Add(new MappingBatchValidationIssue(
                                    c.CandidateId,
                                    "DuplicateCandidatePosition",
                                    $"Candidates {c.CandidateId} and {other.CandidateId} are too close ({dist:F2}m < {DuplicateDistanceThresholdMeters}m).",
                                    "Error"));
                                reported = true;
                                break;
                            }
                        }
                    }
                }
            }

            list.Add((c.CandidateId, pos));
        }
    }

    public static double DistanceMeters(Point p1, Point p2)
    {
        var lat1 = p1.Y * Math.PI / 180.0;
        var lat2 = p2.Y * Math.PI / 180.0;
        var dLat = (p2.Y - p1.Y) * Math.PI / 180.0;
        var dLon = (p2.X - p1.X) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }
}
