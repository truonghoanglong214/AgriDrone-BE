using System.Text.Json;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain;

public sealed class Phase6DomainSeamsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MissionFactoryCarriesSurveyOrderContextWithoutStartingFlight()
    {
        using var parameters = JsonDocument.Parse("{}");
        var context = new SurveyMissionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MissionPurpose.BaselineMapping);

        var mission = DroneMission.CreateForSurvey(
            context,
            Guid.NewGuid(),
            Guid.NewGuid(),
            pilotUserId: null,
            "survey-map-01",
            sourceMapVersionId: null,
            parameters,
            notes: null,
            Guid.NewGuid(),
            Now);

        Assert.Equal(context.SurveyOrderId, mission.SurveyOrderId);
        Assert.Equal(MissionPurpose.BaselineMapping, mission.MissionPurpose);
        Assert.Equal(MissionType.Mapping, mission.MissionType);
        Assert.Equal(MissionStatus.Draft, mission.Status);
    }

    [Fact]
    public void FarmBaseMapServicePreparesOrderScopedDraft()
    {
        var context = new FarmBaseMapPublicationContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            versionNumber: 2,
            sourceMissionGroupId: Guid.NewGuid());
        var service = new FarmBaseMapPublicationService();

        var map = service.PrepareDraft(context, Now);

        Assert.Equal(context.SourceSurveyOrderId, map.SourceSurveyOrderId);
        Assert.Equal(context.FarmId, map.FarmId);
        Assert.Equal(2, map.VersionNumber);
        Assert.Equal(FarmBaseMapStatus.Draft, map.Status);
        Assert.Null(map.PublishedAt);
    }

    [Fact]
    public void PlantScanCanOnlyAttachToOneSurveyResult()
    {
        var scan = (PlantScan)Activator.CreateInstance(
            typeof(PlantScan),
            nonPublic: true)!;
        var orderId = Guid.NewGuid();
        var resultId = Guid.NewGuid();

        scan.AttachToSurveyResult(orderId, resultId);
        scan.AttachToSurveyResult(orderId, resultId);

        Assert.Equal(orderId, scan.SurveyOrderId);
        Assert.Equal(resultId, scan.SurveyResultId);
        Assert.Throws<InvalidOperationException>(() =>
            scan.AttachToSurveyResult(orderId, Guid.NewGuid()));
    }

    [Fact]
    public void HumanReviewLifecycleBelongsToSurveyResult()
    {
        using var provenance = JsonDocument.Parse("{\"model\":\"v1\"}");
        var reviewerId = Guid.NewGuid();
        var result = SurveyResult.CreateForManagerReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SurveyServiceType.PlantHealth,
            provenance,
            Now);

        result.Approve(reviewerId, Now.AddMinutes(5));

        Assert.Equal(SurveyResultStatus.Approved, result.Status);
        Assert.Equal(reviewerId, result.ReviewedBy);
        Assert.Equal(1u, result.Version);
    }
}
