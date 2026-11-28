using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class ComboSnapReviewTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid AttemptId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();
    private static readonly Guid EvidenceId = Guid.NewGuid();
    private static readonly string Hex64 = new('a', 64);

    private static ComboAnalysisReadyV1 CreatePayload(params ComboFindingV1[] findings) =>
        new(
            CausationId: Guid.NewGuid(),
            HandoffId: Guid.NewGuid(),
            SurveyOrderId: OrderId,
            FarmId: FarmId,
            CaptureAttemptId: AttemptId,
            MissionId: Guid.NewGuid(),
            JobId: Guid.NewGuid(),
            SourceRevision: 1,
            ProducedAt: DateTimeOffset.UtcNow,
            SupersedesHandoffId: null,
            BusinessPolicyVersionId: Guid.NewGuid(),
            InputApprovalId: null,
            InputManifestId: null,
            InputManifestHash: null,
            MappingKind: MappingKind.FirstBaseline,
            MappingCandidateBatchId: Guid.NewGuid(),
            FarmBaseMapVersionId: Guid.NewGuid(),
            BaselineMapVersionId: Guid.NewGuid(),
            FarmBoundaryVersionId: Guid.NewGuid(),
            ResultNature: ComboAnalysisV1Values.ResultNature,
            LimitationNotice: "Preliminary",
            Provenance: new ComboAnalysisProvenanceV1(
                "dragonfruit", "1.0.0", "RULES_V1", Guid.NewGuid(), "1.0.0", "combo-v2", "STRICT_V1"),
            Findings: findings,
            BoundaryExceptions: [],
            DiseaseZones: []);

    private static ComboFindingV1 CreateFinding(
        Guid findingId,
        string classCode,
        double lon,
        double lat,
        Guid? plantProfileId = null) =>
        new(
            FindingId: findingId,
            FindingVersion: 1,
            PlantProfileId: plantProfileId,
            MappingCandidateId: plantProfileId.HasValue ? Guid.NewGuid() : null,
            Position: new GeoJsonPointV3("Point", [lon, lat]),
            ObservedAt: DateTimeOffset.UtcNow,
            SuspectedClassCode: classCode,
            ClassificationNature: ComboAnalysisV1Values.ClassificationNature,
            Confidence: 0.90m,
            Uncertainty: plantProfileId.HasValue ? "NONE" : "REQUIRES_REVIEW",
            Limitations: ["DRONE_AERIAL_VIEW_ONLY"],
            EvidenceMedia: [new EvidenceMediaReferenceV4(EvidenceId, "source_image")]);

    [Fact]
    public void ValidateWhenDecisionIsExcludeAndReviewedProfileProvidedFailsWithSnapRequiresAccept()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [new ComboFindingReviewDecisionRecord(findingId, "Exclude", "Not disease", [EvidenceId], profileId)],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapRequiresAccept", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenReasonMissingFailsWithSnapReasonRequired()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "   ", [EvidenceId], profileId)],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapReasonRequired", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenEvidenceMissingFailsWithSnapEvidenceRequired()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Row 14 Col 1", [], profileId)],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapEvidenceRequired", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenEvidenceNotInBatchFailsWithForeignEvidence()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var foreignMedia = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Row 14 Col 1", [foreignMedia], profileId)],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.ForeignEvidence", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenTargetProfileNotInSnapTargetsFailsWithSnapTargetNotFound()
    {
        var findingId = Guid.NewGuid();
        var unknownProfileId = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Row 14 Col 1", [EvidenceId], unknownProfileId)],
            new Dictionary<Guid, ComboSnapTargetPoint>(),
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapTargetNotFound", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenFindingOutsideBoundaryFailsWithSnapOutsideBoundary()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid> { findingId },
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Row 14 Col 1", [EvidenceId], profileId)],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapOutsideBoundary", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenDistanceExceedsThresholdFailsWithSnapTooFar()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(CreateFinding(findingId, "BROWN_SPOT", 106.66000, 10.75000));

        // ~11.1m north (> 5m max snap threshold)
        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Too far", [EvidenceId], profileId)],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75010, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapTooFar", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenTwoSameClassFindingsClaimSamePoleWithSnapFailsWithSnapConflict()
    {
        var existingFindingId = Guid.NewGuid();
        var snappedFindingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(
            CreateFinding(existingFindingId, "BROWN_SPOT", 106.66000, 10.75001, profileId),
            CreateFinding(snappedFindingId, "BROWN_SPOT", 106.66000, 10.75003));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [
                new ComboFindingReviewDecisionRecord(existingFindingId, "Accept", null, []),
                new ComboFindingReviewDecisionRecord(snappedFindingId, "Accept", "Snap to same pole", [EvidenceId], profileId)
            ],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsFailure);
        Assert.Equal("ComboReview.SnapConflict", result.Error.Code);
    }

    [Fact]
    public void ValidateWhenDifferentClassesSnapToSamePoleOrExistingWasExcludedSucceeds()
    {
        var excludedSameClassFindingId = Guid.NewGuid();
        var snappedBrownSpotId = Guid.NewGuid();
        var snappedAnthracnoseId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var payload = CreatePayload(
            CreateFinding(excludedSameClassFindingId, "BROWN_SPOT", 106.66000, 10.75001, profileId),
            CreateFinding(snappedBrownSpotId, "BROWN_SPOT", 106.66000, 10.75003),
            CreateFinding(snappedAnthracnoseId, "ANTHRACNOSE", 106.66000, 10.75003));

        var result = ComboSnapReviewValidator.Validate(
            payload,
            new HashSet<Guid>(),
            [
                new ComboFindingReviewDecisionRecord(excludedSameClassFindingId, "Exclude", "False positive", [EvidenceId]),
                new ComboFindingReviewDecisionRecord(snappedBrownSpotId, "Accept", "Belongs to pole", [EvidenceId], profileId),
                new ComboFindingReviewDecisionRecord(snappedAnthracnoseId, "Accept", "Second class on pole", [EvidenceId], profileId)
            ],
            new Dictionary<Guid, ComboSnapTargetPoint> { [profileId] = new(profileId, 10.75001, 106.66000) },
            new HashSet<Guid> { EvidenceId });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void IsIdentityBlockerResolvedBySnapReturnsTrueForCorrectedIdentityWithSnappedFinding()
    {
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var decision = new ComboFindingReviewDecisionRecord(findingId, "Accept", "Snapped", [EvidenceId], profileId);
        var blocker = new ComboBlockerResolutionRecord(
            "FINDING_IDENTITY",
            findingId.ToString("D"),
            "UnresolvedIdentity",
            "Corrected",
            "Attached to pole",
            []);

        var resolved = ComboSnapReviewValidator.IsIdentityBlockerResolvedBySnap(
            blocker,
            new Dictionary<Guid, ComboFindingReviewDecisionRecord> { [findingId] = decision });

        Assert.True(resolved);
    }

    [Fact]
    public void ComputeDecisionFingerprintChangesOnlyWhenReviewedPlantProfileIdIsSet()
    {
        var reviewedAt = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var findingId = Guid.NewGuid();
        var profileId = Guid.NewGuid();

        var baseline = new ComboReviewDecisionsSnapshotRecord(
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Confirmed", [EvidenceId])],
            [],
            []);
        var explicitNull = new ComboReviewDecisionsSnapshotRecord(
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Confirmed", [EvidenceId], null)],
            [],
            []);
        var snapped = new ComboReviewDecisionsSnapshotRecord(
            [new ComboFindingReviewDecisionRecord(findingId, "Accept", "Confirmed", [EvidenceId], profileId)],
            [],
            []);

        var fpBaseline = SurveyComboReviewRevision.ComputeDecisionFingerprint(
            TenantId, FarmId, OrderId, AttemptId, BatchId, 1, Hex64, 1, null,
            "Approved", "Review", false, null, ReviewerId, reviewedAt, baseline);
        var fpExplicitNull = SurveyComboReviewRevision.ComputeDecisionFingerprint(
            TenantId, FarmId, OrderId, AttemptId, BatchId, 1, Hex64, 1, null,
            "Approved", "Review", false, null, ReviewerId, reviewedAt, explicitNull);
        var fpSnapped = SurveyComboReviewRevision.ComputeDecisionFingerprint(
            TenantId, FarmId, OrderId, AttemptId, BatchId, 1, Hex64, 1, null,
            "Approved", "Review", false, null, ReviewerId, reviewedAt, snapped);

        Assert.Equal(fpBaseline, fpExplicitNull);
        Assert.NotEqual(fpBaseline, fpSnapped);
    }
}
