using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.SharedKernel.Domain.Spatial;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Application.Services;

public sealed class BaselineMappingBatchValidator : IBaselineMappingBatchValidator
{
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

        // Tolerance validation: fail if any tolerance is <= 0
        if (batch.BoundaryToleranceM <= 0 ||
            batch.MatchingDistanceThresholdM <= 0 ||
            batch.RowBearingToleranceDeg <= 0 ||
            batch.GridSpacingToleranceM <= 0)
        {
            issues.Add(new MappingBatchValidationIssue(
                batch.Id,
                "InvalidSpatialPolicyTolerances",
                "Spatial policy tolerances must all be strictly positive.",
                "Error"));
        }

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

        // 3. Grid structure checks (Plant spacing, Row spacing, Row bearing)
        ValidateGridGeometry(batch, activeCandidates, issues);

        // 4. Duplicate position check (identical GPS coordinates within 0.1m)
        ValidateMinimumDistancesSpatialBucketing(activeCandidates, 0.1, issues);

        var total = candidates.Count;
        var accepted = candidates.Count(c => c.ReviewDecision is MappingCandidateReviewDecision.CreateNew or MappingCandidateReviewDecision.MatchExisting);
        var rejected = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Reject);
        var relocated = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Relocate);
        var replaced = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Replace);
        var pending = candidates.Count(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending);

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

    private static void ValidateGridGeometry(
        MappingCandidateBatch batch,
        List<MappingCandidate> activeCandidates,
        List<MappingBatchValidationIssue> issues)
    {
        var candidatesWithGrid = activeCandidates
            .Select(c => new
            {
                Candidate = c,
                Row = c.CorrectedRowIndex ?? c.ProposedRowIndex,
                Col = c.CorrectedColumnIndex ?? c.ProposedColumnIndex,
                Pos = c.CorrectedPosition ?? c.OriginalPosition
            })
            .Where(x => x.Row.HasValue && x.Col.HasValue)
            .ToList();

        var pairCount = 0;

        // Group by Row -> validate PlantSpacing and RowBearing along adjacent columns
        var byRow = candidatesWithGrid.GroupBy(x => x.Row!.Value);
        foreach (var rowGroup in byRow)
        {
            var orderedCols = rowGroup.OrderBy(x => x.Col!.Value).ToList();
            for (var i = 0; i < orderedCols.Count - 1; i++)
            {
                var cur = orderedCols[i];
                var next = orderedCols[i + 1];
                var colDelta = next.Col!.Value - cur.Col!.Value;
                if (colDelta <= 0) continue;

                pairCount++;
                var measuredDist = GeodesicSpatialCalculator.CalculateDistanceMeters(
                    cur.Pos.Y, cur.Pos.X, next.Pos.Y, next.Pos.X);
                var expectedDist = colDelta * batch.PlantSpacingM;
                var spacingTolerance = batch.GridSpacingToleranceM * colDelta;
                var points = $"{PointLabel(cur.Candidate)} and {PointLabel(next.Candidate)}";

                if (Math.Abs(measuredDist - expectedDist) > spacingTolerance)
                {
                    issues.Add(new MappingBatchValidationIssue(
                        next.Candidate.CandidateId,
                        "PlantSpacingToleranceExceeded",
                        $"Distance between {points} in row {rowGroup.Key}, columns {cur.Col} and {next.Col}, is {measuredDist:F2}m (expected ~{expectedDist:F2}m, tolerance +/-{spacingTolerance:F2}m).",
                        "Warning"));
                }

                // Row bearing check between adjacent columns in the row
                var bearing = GeodesicSpatialCalculator.CalculateInitialBearingDegrees(
                    cur.Pos.Y, cur.Pos.X, next.Pos.Y, next.Pos.X);
                var diff = Math.Abs((bearing - batch.GridBearingDeg + 180) % 180);
                if (diff > 90) diff = 180 - diff;

                if (diff > batch.RowBearingToleranceDeg)
                {
                    issues.Add(new MappingBatchValidationIssue(
                        next.Candidate.CandidateId,
                        "RowBearingToleranceExceeded",
                        $"Row {rowGroup.Key} bearing between {points}, columns {cur.Col} and {next.Col}, is {bearing:F1} deg (expected ~{batch.GridBearingDeg:F1} deg, tolerance +/-{batch.RowBearingToleranceDeg:F1} deg).",
                        "Warning"));
                }
            }
        }

        // Group by Col -> validate RowSpacing along adjacent rows
        var byCol = candidatesWithGrid.GroupBy(x => x.Col!.Value);
        foreach (var colGroup in byCol)
        {
            var orderedRows = colGroup.OrderBy(x => x.Row!.Value).ToList();
            for (var i = 0; i < orderedRows.Count - 1; i++)
            {
                var cur = orderedRows[i];
                var next = orderedRows[i + 1];
                var rowDelta = next.Row!.Value - cur.Row!.Value;
                if (rowDelta <= 0) continue;

                pairCount++;
                var distance = GeodesicSpatialCalculator.CalculateDistanceMeters(
                    cur.Pos.Y, cur.Pos.X, next.Pos.Y, next.Pos.X);
                var bearing = GeodesicSpatialCalculator.CalculateInitialBearingDegrees(
                    cur.Pos.Y, cur.Pos.X, next.Pos.Y, next.Pos.X);
                var measuredDist = GeodesicSpatialCalculator.CalculateLateralDistanceMeters(
                    distance, bearing, batch.GridBearingDeg);
                var expectedDist = rowDelta * batch.RowSpacingM;
                var spacingTolerance = batch.GridSpacingToleranceM * rowDelta;

                if (Math.Abs(measuredDist - expectedDist) > spacingTolerance)
                {
                    issues.Add(new MappingBatchValidationIssue(
                        next.Candidate.CandidateId,
                        "RowSpacingToleranceExceeded",
                        $"Perpendicular distance between {PointLabel(cur.Candidate)} and {PointLabel(next.Candidate)}, rows {cur.Row} and {next.Row} in column {colGroup.Key}, is {measuredDist:F2}m (expected ~{expectedDist:F2}m, tolerance +/-{spacingTolerance:F2}m).",
                        "Warning"));
                }
            }
        }

        if (pairCount == 0 && activeCandidates.Count > 0)
        {
            issues.Add(new MappingBatchValidationIssue(
                batch.Id,
                "InsufficientGridEvidence",
                "Farm active candidate count or distribution provides no adjacent grid pairs to evaluate spacing and bearing.",
                "Info"));
        }
    }

    private static string PointLabel(MappingCandidate candidate) =>
        candidate.TreeId ?? candidate.CandidateId.ToString();

    private static void ValidateMinimumDistancesSpatialBucketing(
        List<MappingCandidate> activeCandidates,
        double duplicateThresholdMeters,
        List<MappingBatchValidationIssue> issues)
    {
        if (activeCandidates.Count <= 1) return;

        var cellSizeMeters = duplicateThresholdMeters;
        var avgLat = activeCandidates.Average(c => (c.CorrectedPosition ?? c.OriginalPosition).Y);
        var radLat = avgLat * Math.PI / 180.0;
        var metersPerDegreeLon = EarthRadiusMeters * Math.Cos(radLat) * (Math.PI / 180.0);

        var buckets = new Dictionary<(int X, int Y), List<(Guid CandidateId, Point Pos)>>();

        foreach (var c in activeCandidates)
        {
            var pos = c.CorrectedPosition ?? c.OriginalPosition;
            var cellX = (int)Math.Floor((pos.X * metersPerDegreeLon) / cellSizeMeters);
            var cellY = (int)Math.Floor((pos.Y * MetersPerDegreeLat) / cellSizeMeters);

            var key = (cellX, cellY);
            if (!buckets.TryGetValue(key, out var list))
            {
                list = [];
                buckets[key] = list;
            }

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
                            if (dist < duplicateThresholdMeters)
                            {
                                issues.Add(new MappingBatchValidationIssue(
                                    c.CandidateId,
                                    "DuplicateCandidatePosition",
                                    $"Candidates {c.CandidateId} and {other.CandidateId} are too close ({dist:F2}m < {duplicateThresholdMeters:F2}m).",
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
        return GeodesicSpatialCalculator.CalculateDistanceMeters(p1.Y, p1.X, p2.Y, p2.X);
    }
}
