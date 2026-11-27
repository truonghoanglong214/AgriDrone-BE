using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Farms;

public sealed class BaselineMappingBatchValidatorTests
{
    private readonly BaselineMappingBatchValidator _validator = new();

    [Fact]
    public void ValidateWhenCandidatesArePendingReturnsInvalidWithPendingError()
    {
        // Arrange
        var batch = CreateSampleBatch();
        Guid? zoneId = null;

        var candidate = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: new Point(106.0, 10.0) { SRID = 4326 },
            proposedRowIndex: 1,
            proposedColumnIndex: 1,
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: DateTimeOffset.UtcNow);

        batch.AddCandidate(candidate);

        // Act
        var result = _validator.Validate(batch, boundary: null);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "PendingReviewRemaining" && i.Severity == "Error");
    }

    [Fact]
    public void ValidateWhenCandidateHasNeedsReviewClassificationAndPendingFlagsNeedsReviewError()
    {
        // Arrange
        var batch = CreateSampleBatch();
        Guid? zoneId = null;

        var candidate = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: new Point(106.0, 10.0) { SRID = 4326 },
            proposedRowIndex: 1,
            proposedColumnIndex: 1,
            positionConfidence: 0.70m,
            boundaryClassification: "NEEDS_REVIEW",
            measuredBoundaryDistanceM: 0.1m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: DateTimeOffset.UtcNow);

        batch.AddCandidate(candidate);

        // Act
        var result = _validator.Validate(batch, boundary: null);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "NeedsReviewPending" && i.Severity == "Error");
    }

    [Fact]
    public void ValidateWhenCandidatesAreTooCloseDetectsDuplicateViaSpatialBucketing()
    {
        // Arrange
        var batch = CreateSampleBatch();
        Guid? zoneId = null;
        var reviewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Two points ~0.03 meters apart (< 0.1m duplicate threshold)
        var p1 = new Point(106.0, 10.0) { SRID = 4326 };
        var p2 = new Point(106.0000002, 10.0000002) { SRID = 4326 };

        var c1 = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: p1,
            proposedRowIndex: 1,
            proposedColumnIndex: 1,
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now);
        c1.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, reviewerId, "OK", now);

        var c2 = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: p2,
            proposedRowIndex: 1,
            proposedColumnIndex: 2,
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now);
        c2.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, reviewerId, "OK", now);

        batch.AddCandidate(c1);
        batch.AddCandidate(c2);

        // Act
        var result = _validator.Validate(batch, boundary: null);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "DuplicateCandidatePosition");
    }

    [Fact]
    public void ValidateWhenDuplicateGridPositionInFarmFlagsDuplicateGridError()
    {
        // Arrange
        var batch = CreateSampleBatch();
        Guid? zoneId = null;
        var reviewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var p1 = new Point(106.0, 10.0) { SRID = 4326 };
        var p2 = new Point(106.01, 10.01) { SRID = 4326 }; // Far enough to not trigger distance error

        var c1 = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: p1,
            proposedRowIndex: 2,
            proposedColumnIndex: 3,
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now);
        c1.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, reviewerId, "OK", now);

        var c2 = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: p2,
            proposedRowIndex: 2,
            proposedColumnIndex: 3, // Duplicate Row 2, Col 3
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now);
        c2.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, reviewerId, "OK", now);

        batch.AddCandidate(c1);
        batch.AddCandidate(c2);

        // Act
        var result = _validator.Validate(batch, boundary: null);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "DuplicateGridPositionInFarmMap");
    }

    [Fact]
    public void ValidateWhenCandidatesProperlyReviewedAndNonOverlappingReturnsValid()
    {
        // Arrange
        var batch = CreateSampleBatch();
        Guid? zoneId = null;
        var reviewerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Two adjacent columns 2.5m apart along GridBearingDeg = 45 deg
        var p1 = new Point(106.0, 10.0) { SRID = 4326 };
        var p2 = new Point(106.0000161, 10.0000159) { SRID = 4326 };

        var c1 = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: p1,
            proposedRowIndex: 1,
            proposedColumnIndex: 1,
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now);
        c1.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, reviewerId, "OK", now);

        var c2 = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: zoneId,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: p2,
            proposedRowIndex: 1,
            proposedColumnIndex: 2,
            positionConfidence: 0.95m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5.0m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now);
        c2.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, reviewerId, "OK", now);

        batch.AddCandidate(c1);
        batch.AddCandidate(c2);

        // Act
        var result = _validator.Validate(batch, boundary: null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal(2, result.ConfirmedPublishablePoleCount);
        Assert.Equal(2, result.AcceptedCount);
    }

    [Fact]
    public void ValidateStaggeredRowsUsesPerpendicularSeparation()
    {
        var batch = CreateSampleBatch(gridBearingDeg: 90);
        AddReviewedPoint(batch, row: 1, col: 1, east: 0, north: 0);
        AddReviewedPoint(batch, row: 2, col: 1, east: 2.5, north: 2.5);

        var result = _validator.Validate(batch, boundary: null);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateSpacingAcrossSkippedColumnsReportsBothPointsAndScaledTolerance()
    {
        var batch = CreateSampleBatch(gridBearingDeg: 90);
        AddReviewedPoint(batch, row: 1, col: 1, east: 0, north: 0);
        AddReviewedPoint(batch, row: 1, col: 3, east: 7, north: 0);

        var result = _validator.Validate(batch, boundary: null);

        var issue = Assert.Single(result.Issues, issue => issue.Code == "PlantSpacingToleranceExceeded");
        Assert.Contains("tree_r1_c1 and tree_r1_c3", issue.Message, StringComparison.Ordinal);
        Assert.Contains($"tolerance +/-{1.0:F2}m", issue.Message, StringComparison.Ordinal);
    }

    private static void AddReviewedPoint(MappingCandidateBatch batch, int row, int col, double east, double north)
    {
        const double degreesPerMeter = 180.0 / (Math.PI * 6_371_000.0);
        var now = DateTimeOffset.UtcNow;
        var point = new Point(east * degreesPerMeter, north * degreesPerMeter) { SRID = 4326 };
        var candidate = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: null,
            candidateId: Guid.NewGuid(),
            sourceObservationId: null,
            evidenceMediaAssetIds: [],
            originalPosition: point,
            proposedRowIndex: row,
            proposedColumnIndex: col,
            positionConfidence: 1,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 5,
            boundaryThresholdM: 1,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: now,
            treeId: $"tree_r{row}_c{col}");
        candidate.ApplyReview(MappingCandidateReviewDecision.CreateNew, null, null, null, null, Guid.NewGuid(), null, now);
        batch.AddCandidate(candidate);
    }

    private static MappingCandidateBatch CreateSampleBatch(double gridBearingDeg = 45.0)
    {
        var now = DateTimeOffset.UtcNow;
        var batchId = Guid.NewGuid();
        var batch = MappingCandidateBatch.Create(
            id: batchId,
            mappingKind: MappingKind.FirstBaseline,
            handoffId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            farmId: Guid.NewGuid(),
            surveyOrderId: Guid.NewGuid(),
            missionId: Guid.NewGuid(),
            captureAttemptId: Guid.NewGuid(),
            farmBoundaryVersionId: Guid.NewGuid(),
            farmBaseMapVersionId: Guid.NewGuid(),
            expectedCurrentFarmBaseMapVersionId: null,
            gridBearingDeg: gridBearingDeg,
            rowSpacingM: 2.5,
            plantSpacingM: 2.5,
            jobId: Guid.NewGuid(),
            payloadFingerprint: new string('0', 64),
            modelVersionId: Guid.NewGuid(),
            modelVersion: "v1.0",
            algorithmVersion: "grid-v1",
            boundaryPolicyVersion: "poly-v1",
            producedAt: now,
            spatialPolicyVersionId: Guid.NewGuid(),
            spatialPolicyFingerprint: "test-policy-fingerprint",
            boundaryToleranceM: 2.0,
            matchingDistanceThresholdM: 1.5,
            rowBearingToleranceDeg: 15.0,
            gridSpacingToleranceM: 0.5,
            createdAt: now);

        return batch;
    }
}
