using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Plants.Domain.Changes;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Plants;

/// <summary>
/// Unit tests for F-05 (Follow-up matching, boundary billability, NeedsReview)
/// and F-06 (PlantProfile observations, version concurrency BVA, unverified retire/replace protection):
/// - TC-F05-U01: Follow-up candidate matches active PlantProfile and preserves PlantProfileId.
/// - TC-F05-U02: Candidates inside vs outside boundary (OUT_OF_BOUNDARY excluded from billable pole count).
/// - TC-F05-U03: Ambiguous/NeedsReview candidates do not auto-create or retire PlantProfile.
/// - TC-F06-U01: Apply approved Follow-up observation preserves PlantProfileId with date &amp; evidence.
/// - TC-F06-U02: Optimistic concurrency version BVA (current succeeds, current - 1 fails without overwrite).
/// - TC-F06-U03: Proposing retire/replace without Manager verification &amp; Follow-up evidence leaves profile Active.
/// </summary>
public sealed class FollowUpMatchingAndProfileLifecycleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    // ─── TC-F05-U01 & TC-F06-U01 ───────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F05-U01"), Trait("TC", "TC-F06-U01")]
    public void FollowUpMatchedCandidatePreservesPlantProfileIdAndRecordsDatedObservation()
    {
        var baselineMapId = Guid.NewGuid();
        var followUpMapId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();

        var baselinePoint = new Point(106.700000, 10.770000) { SRID = 4326 };
        var followUpPoint = new Point(106.700005, 10.770005) { SRID = 4326 }; // ~0.78m drift

        var profile = PlantProfile.Create(
            Guid.NewGuid(), TenantId, FarmId, "POLE-001",
            baselinePoint, baselineMapId, T0);
        var originalProfileId = profile.Id;

        var candidate = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batchId,
            zoneId: null,
            candidateId: candidateId,
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [Guid.NewGuid()],
            originalPosition: followUpPoint,
            proposedRowIndex: 1,
            proposedColumnIndex: 1,
            positionConfidence: 0.96m,
            boundaryClassification: "IN_BOUNDARY",
            measuredBoundaryDistanceM: 4.2m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: originalProfileId,
            matchConfidence: 0.94m,
            createdAt: T1);

        candidate.ApplyReview(
            MappingCandidateReviewDecision.MatchExisting,
            correctedPosition: null,
            correctedRowIndex: null,
            correctedColumnIndex: null,
            correctedMatchedPlantId: originalProfileId,
            reviewerId: ReviewerId,
            reason: "Matched baseline pole in row 1 col 1",
            reviewedAt: T1);

        var distanceM = profile.Relocate(followUpPoint, followUpMapId, matchingDistanceThresholdM: 3.0, T1);
        var observation = PlantProfileObservation.Create(
            Guid.NewGuid(), profile.Id, TenantId, FarmId, orderId, followUpMapId,
            batchId, candidate.CandidateId, followUpPoint, distanceM, T1, ReviewerId);

        Assert.Equal(originalProfileId, profile.Id);
        Assert.Equal(PlantProfileStatus.Active, profile.Status);
        Assert.Equal(followUpMapId, profile.CurrentMapVersionId);
        Assert.Equal(originalProfileId, observation.PlantProfileId);
        Assert.Equal(candidateId, observation.CandidateId);
        Assert.Equal(batchId, observation.MappingCandidateBatchId);
        Assert.Equal(T1, observation.ObservedAt);
        Assert.True(observation.ObservedDistanceM < 3.0);
    }

    // ─── TC-F05-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F05-U02")]
    public void OutsideBoundaryPoleIsExcludedFromVerifiedBillablePoleCount()
    {
        var snapshotId = Guid.NewGuid();
        var batchId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var spatialPolicyId = Guid.NewGuid();

        var insideProfileId = Guid.NewGuid();
        var insidePos = new Point(106.7000, 10.7700) { SRID = 4326 };
        var outsidePos = new Point(106.7500, 10.8000) { SRID = 4326 };

        var items = new List<SurveyOrderPoleCountSnapshotItem>
        {
            SurveyOrderPoleCountSnapshotItem.Create(
                id: Guid.NewGuid(),
                snapshotId: snapshotId,
                tenantId: TenantId,
                farmId: FarmId,
                mappingCandidateBatchId: batchId,
                mappingCandidateId: Guid.NewGuid(),
                sourceCandidateId: Guid.NewGuid(),
                plantProfileId: insideProfileId,
                effectivePosition: insidePos,
                reviewDecision: "CreateNew",
                isAccepted: true,
                isStrictlyInBoundary: true,
                effectiveRowIndex: 1,
                effectiveColumnIndex: 1),
            SurveyOrderPoleCountSnapshotItem.Create(
                id: Guid.NewGuid(),
                snapshotId: snapshotId,
                tenantId: TenantId,
                farmId: FarmId,
                mappingCandidateBatchId: batchId,
                mappingCandidateId: Guid.NewGuid(),
                sourceCandidateId: Guid.NewGuid(),
                plantProfileId: null,
                effectivePosition: outsidePos,
                reviewDecision: "Reject",
                isAccepted: false,
                isStrictlyInBoundary: false,
                exclusionReason: "OUT_OF_BOUNDARY")
        };

        var fp = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
            TenantId, orderId, FarmId, missionId, attemptId, mapId, boundaryId, batchId,
            "spatial-policy-fp", items);

        var snapshot = SurveyOrderPoleCountSnapshot.Create(
            snapshotId, orderId, TenantId, FarmId, missionId, attemptId,
            mapId, boundaryId, batchId, spatialPolicyId, fp, items, ReviewerId, T1);

        Assert.Equal(2, snapshot.Items.Count);
        Assert.Equal(1, snapshot.VerifiedBillablePoleCount);
    }

    // ─── TC-F05-U03 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F05-U03")]
    public void AmbiguousNeedsReviewCandidateBlocksApprovalWithoutMutatingProfile()
    {
        var batch = MappingCandidateBatch.Create(
            id: Guid.NewGuid(),
            mappingKind: MappingKind.FollowUpSnapshot,
            handoffId: Guid.NewGuid(),
            tenantId: TenantId,
            farmId: FarmId,
            surveyOrderId: Guid.NewGuid(),
            missionId: Guid.NewGuid(),
            captureAttemptId: Guid.NewGuid(),
            farmBoundaryVersionId: Guid.NewGuid(),
            farmBaseMapVersionId: Guid.NewGuid(),
            expectedCurrentFarmBaseMapVersionId: Guid.NewGuid(),
            gridBearingDeg: 0,
            rowSpacingM: 3.0,
            plantSpacingM: 2.5,
            jobId: Guid.NewGuid(),
            payloadFingerprint: new string('a', 64),
            modelVersionId: Guid.NewGuid(),
            modelVersion: "v3",
            algorithmVersion: "grid-v3",
            boundaryPolicyVersion: "bp-v1",
            producedAt: T1,
            spatialPolicyVersionId: Guid.NewGuid(),
            spatialPolicyFingerprint: "sp-fp",
            boundaryToleranceM: 1.0,
            matchingDistanceThresholdM: 3.0,
            rowBearingToleranceDeg: 5.0,
            gridSpacingToleranceM: 0.5,
            createdAt: T1);

        var ambiguousCandidate = MappingCandidate.Create(
            id: Guid.NewGuid(),
            batchId: batch.Id,
            zoneId: null,
            candidateId: Guid.NewGuid(),
            sourceObservationId: Guid.NewGuid(),
            evidenceMediaAssetIds: [],
            originalPosition: new Point(106.7, 10.77) { SRID = 4326 },
            proposedRowIndex: null,
            proposedColumnIndex: null,
            positionConfidence: 0.55m,
            boundaryClassification: "NEEDS_REVIEW",
            measuredBoundaryDistanceM: 0.2m,
            boundaryThresholdM: 1.0m,
            proposedMatchedPlantId: null,
            matchConfidence: null,
            createdAt: T1);

        batch.AddCandidate(ambiguousCandidate);

        var validator = new BaselineMappingBatchValidator();
        var validation = validator.Validate(batch, boundary: null);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, i => i.Code == "NeedsReviewPending");
        Assert.Equal(MappingCandidateReviewDecision.Pending, ambiguousCandidate.ReviewDecision);
    }

    // ─── TC-F06-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F06-U02")]
    public void ConcurrencyVersionBvaAllowsCurrentAndRejectsStaleVersionMinusOne()
    {
        var attempt = CaptureAttempt.CreateInitial(
            Guid.NewGuid(), Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(),
            ReviewerId, "Manager", "Init", Guid.NewGuid(), operationId: null, payloadFingerprint: null, createdAt: T0);

        // Version is 1 -> current passes
        attempt.EnsureVersion(attempt.Version);

        // Version - 1 -> throws SurveyDomainException without changing state
        var ex = Assert.Throws<SurveyDomainException>(() => attempt.EnsureVersion(attempt.Version - 1));
        Assert.Equal(CaptureAttemptDomainErrorCodes.VersionConflict, ex.Code);
        Assert.Equal(1u, attempt.Version);
        Assert.Equal(CaptureAttemptStatus.Planned, attempt.Status);
    }

    // ─── TC-F06-U03 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F06-U03")]
    public void ProposingRetireOrReplaceWithoutVerificationCannotApplyAndKeepsProfileActive()
    {
        var baselineMapId = Guid.NewGuid();
        var profile = PlantProfile.Create(
            Guid.NewGuid(), TenantId, FarmId, "POLE-002",
            new Point(106.7, 10.77) { SRID = 4326 }, baselineMapId, T0);

        using var evidence = JsonDocument.Parse("{\"note\":\"owner report\"}");
        var report = PlantInventoryChangeReport.Create(
            TenantId, FarmId, Guid.NewGuid(),
            PlantInventoryChangeKind.Removed,
            profile.Id,
            new Point(106.7, 10.77) { SRID = 4326 },
            callerScope: "tenant-owner",
            idempotencyKey: "retire-without-review",
            reason: "Owner thinks pole was removed",
            evidence: evidence,
            submittedAt: T1);

        // Attempting to apply directly without Manager verification + Follow-up survey evidence is rejected
        Assert.Throws<PlantInventoryChangeDomainException>(() =>
            report.MarkApplied(null, Guid.NewGuid(), baselineMapId, ReviewerId, T1.AddHours(1), "Bypass attempt", evidence));

        Assert.Equal(PlantInventoryChangeStatus.Submitted, report.Status);
        Assert.Equal(PlantProfileStatus.Active, profile.Status);
    }
}
