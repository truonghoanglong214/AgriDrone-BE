using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

public sealed class SurveyApprovalStep3Tests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 1, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true, SurveyOrderStatus.AwaitingBaselineAppointment)]
    [InlineData(false, SurveyOrderStatus.PendingBoundaryVerification)]
    public void InitialOrderStateDependsOnAuthoritativeBaselineRequirement(
        bool requiresBaselineMapping,
        SurveyOrderStatus expectedStatus)
    {
        var order = CreateOrder(requiresBaselineMapping);

        Assert.Equal(expectedStatus, order.Status);
        Assert.Equal(
            SurveyApprovalOrderCompatibilityPolicy.Version,
            order.PreviousOrderCompatibilityPolicyVersion);
        Assert.Equal(
            SurveyApprovalOrderCompatibilityPolicy.NoPreviousOrder,
            order.PreviousOrderCompatibilityReason);
    }

    [Fact]
    public void PreviousOrderIsCompatibleOnlyWhenPlantIdentityAndSemanticsMatch()
    {
        var mapId = Guid.NewGuid();
        var candidate = Candidate(
            mapId,
            SurveyApprovalOrderCompatibilityPolicy.LabelDatasetVersion,
            SurveyApprovalOrderCompatibilityPolicy.AnnotationRuleVersion);

        var decision = SurveyApprovalOrderCompatibilityPolicy.Evaluate(
            Baseline(mapId),
            candidate);

        Assert.Equal(candidate.SurveyOrderId, decision.PreviousCompatibleOrderId);
        Assert.Equal(SurveyApprovalOrderCompatibilityPolicy.Version,
            decision.PolicyVersion);
        Assert.Equal(SurveyApprovalOrderCompatibilityPolicy.Compatible,
            decision.Reason);
    }

    [Theory]
    [InlineData(false, true, true, "PLANT_IDENTITY_MISMATCH")]
    [InlineData(true, false, true, "CLASS_SEMANTICS_MISMATCH")]
    [InlineData(true, true, false, "ANNOTATION_SEMANTICS_MISMATCH")]
    public void IncompatibleHistoryIsNotLinked(
        bool sameMap,
        bool sameDataset,
        bool sameAnnotation,
        string expectedReason)
    {
        var currentMapId = Guid.NewGuid();
        var candidate = Candidate(
            sameMap ? currentMapId : Guid.NewGuid(),
            sameDataset
                ? SurveyApprovalOrderCompatibilityPolicy.LabelDatasetVersion
                : "OTHER_DATASET",
            sameAnnotation
                ? SurveyApprovalOrderCompatibilityPolicy.AnnotationRuleVersion
                : "OTHER_RULES");

        var decision = SurveyApprovalOrderCompatibilityPolicy.Evaluate(
            Baseline(currentMapId),
            candidate);

        Assert.Null(decision.PreviousCompatibleOrderId);
        Assert.Equal(expectedReason, decision.Reason);
    }

    [Fact]
    public void MissingSemanticProvenanceFailsClosed()
    {
        var mapId = Guid.NewGuid();

        var decision = SurveyApprovalOrderCompatibilityPolicy.Evaluate(
            Baseline(mapId),
            Candidate(mapId, datasetVersion: null, annotationRuleVersion: null));

        Assert.Null(decision.PreviousCompatibleOrderId);
        Assert.Equal(
            SurveyApprovalOrderCompatibilityPolicy.ClassSemanticsUnavailable,
            decision.Reason);
    }

    [Fact]
    public void MissingCurrentInventoryCannotReusePreviousPlantIdentity()
    {
        var mapId = Guid.NewGuid();
        var baseline = new SurveyApprovalFarmBaselineReference(
            Guid.NewGuid(),
            Guid.NewGuid(),
            mapId,
            ConfirmedActivePoleCount: null);

        var decision = SurveyApprovalOrderCompatibilityPolicy.Evaluate(
            baseline,
            Candidate(
                mapId,
                SurveyApprovalOrderCompatibilityPolicy.LabelDatasetVersion,
                SurveyApprovalOrderCompatibilityPolicy.AnnotationRuleVersion));

        Assert.Null(decision.PreviousCompatibleOrderId);
        Assert.Equal(
            SurveyApprovalOrderCompatibilityPolicy
                .CurrentPlantInventoryUnavailable,
            decision.Reason);
    }

    private static SurveyOrder CreateOrder(bool requiresBaselineMapping) =>
        SurveyOrder.Create(
            $"SO-{Guid.NewGuid():N}"[..20],
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            requiresBaselineMapping,
            previousCompatibleOrderId: null,
            SurveyApprovalOrderCompatibilityPolicy.Version,
            SurveyApprovalOrderCompatibilityPolicy.NoPreviousOrder,
            Now);

    private static SurveyApprovalFarmBaselineReference Baseline(Guid mapId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), mapId, ConfirmedActivePoleCount: 10);

    private static SurveyApprovalPreviousOrderReference Candidate(
        Guid mapId,
        string? datasetVersion,
        string? annotationRuleVersion) =>
        new(
            Guid.NewGuid(),
            Now.AddDays(-1),
            mapId,
            datasetVersion,
            annotationRuleVersion);
}
