using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.IntegrationContracts.Missions.Validation;
using AgriDrone.IntegrationContracts.Surveys;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class BaselineLifecycleSyncContractTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BaselineMissionStartedValidatorAcceptsValidPayload()
    {
        var payload = new BaselineMissionStartedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now);

        var errors = BaselineMissionStartedV1Validator.Validate(payload);

        Assert.Empty(errors);
    }

    [Fact]
    public void BaselineMissionStartedValidatorRejectsEmptyIdsAndNonUtcTimestamp()
    {
        var payload = new BaselineMissionStartedV1(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.FromHours(7)));

        var errors = BaselineMissionStartedV1Validator.Validate(payload);

        Assert.Contains("SurveyOrderId is required.", errors);
        Assert.Contains("MissionId is required.", errors);
        Assert.Contains("TenantId is required.", errors);
        Assert.Contains("FarmId is required.", errors);
        Assert.Contains("StartedAt must use the UTC offset.", errors);
    }

    [Fact]
    public void BaselineOrthophotoCompletedValidatorAcceptsValidPayload()
    {
        var payload = new BaselineOrthophotoCompletedV1(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "https://minio.agridrone.internal/orthophoto.tif",
            Now);

        var errors = BaselineOrthophotoCompletedV1Validator.Validate(payload);

        Assert.Empty(errors);
    }

    [Fact]
    public void BaselineOrthophotoCompletedValidatorRejectsEmptyIds()
    {
        var payload = new BaselineOrthophotoCompletedV1(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            null,
            default);

        var errors = BaselineOrthophotoCompletedV1Validator.Validate(payload);

        Assert.Contains("SurveyOrderId is required.", errors);
        Assert.Contains("MissionId is required.", errors);
        Assert.Contains("TenantId is required.", errors);
        Assert.Contains("FarmId is required.", errors);
        Assert.Contains("CompletedAt is required.", errors);
    }

    [Fact]
    public void OperationalContextV3PreservesCandidateSurveyEnvelope()
    {
        var context = new SurveyOrderOperationalContextV3(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BASELINE_MAPPING",
            "BASELINE_MAPPING",
            "BaselineMapping",
            "BaselineReady",
            1,
            null,
            null,
            true,
            null,
            null,
            null,
            "VND",
            null,
            Guid.NewGuid(),
            "BaselineMapping",
            Now,
            Now.AddHours(2),
            "Confirmed",
            Guid.NewGuid(),
            "Confirmed",
            Guid.NewGuid(),
            null,
            true,
            [],
            Now,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            true,
            true,
            "Confirmed",
            500_000m,
            null,
            Guid.NewGuid(),
            Now,
            CandidateCenterLongitude: 106.7001,
            CandidateCenterLatitude: 10.8001,
            ApproximateAreaHa: 2.5m,
            SurveyRadiusMeters: 107.1);

        Assert.Equal(106.7001, context.CandidateCenterLongitude);
        Assert.Equal(10.8001, context.CandidateCenterLatitude);
        Assert.Equal(2.5m, context.ApproximateAreaHa);
        Assert.Equal(107.1, context.SurveyRadiusMeters);
    }

    [Fact]
    public void DescriptorsAndConsumerNamesAreProperlyConfigured()
    {
        Assert.Equal("mission.baseline-started.v1", IntegrationEventDescriptors.BaselineMissionStartedV1.EventType);
        Assert.Equal("mission.baseline-orthophoto-completed.v1", IntegrationEventDescriptors.BaselineOrthophotoCompletedV1.EventType);
        Assert.Equal("be1-baseline-mission-started-v1", IntegrationConsumerNames.Be1BaselineMissionStartedV1);
        Assert.Equal("be1-baseline-orthophoto-completed-v1", IntegrationConsumerNames.Be1BaselineOrthophotoCompletedV1);
    }
}
