using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Queries;
using AgriDrone.SharedInfrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class V3OrderPlanningContractTests
{
    [Fact]
    public async Task BaselineCanBePreparedBeforePaidServiceIsReady()
    {
        var orderId = Guid.NewGuid();
        var source = new FakeQuery(orderId);
        var planning = new SurveyOrderMissionPlanningQuery(source);

        var prepared = await planning.GetAsync(orderId);
        var paid = await planning.GetForPurposeAsync(orderId, MissionPurpose.PlantHealth);

        Assert.NotNull(prepared);
        Assert.True(prepared.IsReadyForOperations);
        Assert.True(prepared.RequiresBaselineMapping);
        Assert.Equal(source.BaselineStart, prepared.AppointmentStartAt);
        Assert.NotNull(paid);
        Assert.False(paid.IsReadyForOperations);
        Assert.Equal("PaymentNotConfirmed", paid.ReadinessFailureCode);
    }

    [Fact]
    public async Task PlanningPreservesApprovedBoundaryAndSelectedZones()
    {
        var orderId = Guid.NewGuid();
        var boundaryId = Guid.NewGuid();
        var zones = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var appointment = new DateTimeOffset(2026, 10, 10, 2, 0, 0, TimeSpan.Zero);
        var source = new SurveyOrderOperationalContextV3(
            orderId, Guid.NewGuid(), Guid.NewGuid(), "PLANT_HEALTH", "PLANT_HEALTH",
            nameof(MissionPurpose.PlantHealth), "ReadyForPaidService", 1,
            boundaryId, Guid.NewGuid(), false, 100, Guid.NewGuid(), 10m,
            "VND", 1000m, Guid.NewGuid(), "PaidService", appointment,
            appointment.AddHours(1), "Confirmed", Guid.NewGuid(), "Confirmed",
            Guid.NewGuid(), null, true, [], appointment, zones);

        var planning = new SurveyOrderMissionPlanningQuery(new SingleContextQuery(source));
        var context = await planning.GetAsync(orderId);

        Assert.NotNull(context);
        Assert.Equal(boundaryId, context.FarmBoundaryVersionId);
        Assert.Equal(zones, context.ScopeZoneIds);
    }

    [Fact]
    public async Task UnpaidRepeatOrderCannotBePlannedOrScheduled()
    {
        var orderId = Guid.NewGuid();
        var appointment = new DateTimeOffset(2026, 10, 10, 2, 0, 0, TimeSpan.Zero);
        var source = new SurveyOrderOperationalContextV3(
            orderId, Guid.NewGuid(), Guid.NewGuid(), "PLANT_HEALTH", "PLANT_HEALTH",
            nameof(MissionPurpose.PlantHealth), "AwaitingPayment", 1,
            Guid.NewGuid(), Guid.NewGuid(), false, 100, Guid.NewGuid(), 10m,
            "VND", 1000m, Guid.NewGuid(), "PaidService", appointment,
            appointment.AddHours(1), "Confirmed", Guid.NewGuid(), "Pending",
            Guid.NewGuid(), null, false,
            ["OrderNotEligible", "PaymentNotConfirmed"], appointment);

        var planning = new SurveyOrderMissionPlanningQuery(new SingleContextQuery(source));
        var context = await planning.GetAsync(orderId);

        Assert.NotNull(context);
        Assert.False(context.IsEligibleForPlanning);
        Assert.False(context.IsReadyForOperations);
        Assert.False(context.IsReadyToSchedule);
        Assert.False(context.RequiresBaselineMapping);
    }

    [Fact]
    public async Task UnverifiedBoundaryCannotBeUsedForMissionPlanning()
    {
        var orderId = Guid.NewGuid();
        var source = new SurveyOrderOperationalContextV3(
            orderId, Guid.NewGuid(), Guid.NewGuid(), "PLANT_HEALTH", "PLANT_HEALTH",
            nameof(MissionPurpose.PlantHealth), "PendingBoundaryVerification", 1,
            null, null, true, null, null, null, null, null,
            null, null, null, null, null, null, null,
            Guid.NewGuid(), null, false,
            ["OrderNotEligible", "BoundaryNotApproved", "ScopeNotConfirmed"],
            DateTimeOffset.UtcNow);

        var planning = new SurveyOrderMissionPlanningQuery(new SingleContextQuery(source));
        var context = await planning.GetAsync(orderId);

        Assert.NotNull(context);
        Assert.False(context.IsEligibleForPlanning);
        Assert.False(context.IsReadyToSchedule);
    }

    [Fact]
    public void V3RoutingHasIndependentVersionAndConsumer()
    {
        Assert.Equal(3, IntegrationEventDescriptors.FarmBaseMapPublishedV3.SchemaVersion);
        Assert.Equal("mapping.farm-base-map-published.v3",
            IntegrationEventDescriptors.FarmBaseMapPublishedV3.EventType);
        Assert.NotEqual(IntegrationConsumerNames.Be2FarmBaseMapPublishedV2,
            IntegrationConsumerNames.Be2FarmBaseMapPublishedV3);
        Assert.Equal(2, IntegrationEventDescriptors.FarmBaseMapPublishedV2.SchemaVersion);
    }

    [Fact]
    public void PublishedMapV3HasStableWireJsonAndValidatesAfterRoundTrip()
    {
        var ids = Enumerable.Range(1, 12)
            .Select(index => Guid.Parse($"{index:D8}-0000-0000-0000-000000000000"))
            .ToArray();
        var publishedAt = new DateTimeOffset(2026, 10, 9, 1, 2, 3, TimeSpan.Zero);
        var payload = new FarmBaseMapPublishedV3(
            ids[4], ids[5], ids[6], ids[7], ids[8], ids[9], ids[10],
            2, 1, ids[3], publishedAt,
            [new PublishedZoneMapV3(ids[11], ids[0], 1,
                [new PublishedPlantMappingV3(ids[1], ids[2],
                    new GeoJsonPointV3("Point", [106.5, 10.5]), 1, 1,
                    PublishedPlantLifecycleStatusesV3.Active, null)])], []);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.FarmBaseMapPublishedV3,
            ids[0], ids[1], ids[2], ids[3], publishedAt, payload,
            causationId: ids[4], sourceSystem: "BE1");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=agridrone-contract-tests;Username=test;Password=test",
                ["RabbitMq:Enabled"] = "false",
                ["Messaging:Outbox:Enabled"] = "false"
            }).Build();
        var services = new ServiceCollection();
        services.AddIntegrationMessagingFoundation(configuration);
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IIntegrationMessageSerializer>();

        var json = Encoding.UTF8.GetString(serializer.Serialize(envelope));
        const string expected =
            "{\"messageId\":\"00000001-0000-0000-0000-000000000000\",\"correlationId\":\"00000002-0000-0000-0000-000000000000\",\"tenantId\":\"00000003-0000-0000-0000-000000000000\",\"actorId\":\"00000004-0000-0000-0000-000000000000\",\"occurredAt\":\"2026-10-09T01:02:03+00:00\",\"schemaVersion\":3,\"eventType\":\"mapping.farm-base-map-published.v3\",\"payload\":{\"causationId\":\"00000005-0000-0000-0000-000000000000\",\"publicationId\":\"00000006-0000-0000-0000-000000000000\",\"surveyOrderId\":\"00000007-0000-0000-0000-000000000000\",\"sourceMissionId\":\"00000008-0000-0000-0000-000000000000\",\"farmId\":\"00000009-0000-0000-0000-000000000000\",\"farmBoundaryVersionId\":\"00000010-0000-0000-0000-000000000000\",\"farmBaseMapVersionId\":\"00000011-0000-0000-0000-000000000000\",\"versionNumber\":2,\"confirmedSurveyPoleCount\":1,\"confirmedBy\":\"00000004-0000-0000-0000-000000000000\",\"publishedAt\":\"2026-10-09T01:02:03+00:00\",\"zones\":[{\"zoneId\":\"00000012-0000-0000-0000-000000000000\",\"zoneMapVersionId\":\"00000001-0000-0000-0000-000000000000\",\"versionNumber\":1,\"plantMappings\":[{\"plantId\":\"00000002-0000-0000-0000-000000000000\",\"sourceCandidateId\":\"00000003-0000-0000-0000-000000000000\",\"position\":{\"type\":\"Point\",\"coordinates\":[106.5,10.5]},\"rowIndex\":1,\"columnIndex\":1,\"lifecycleStatus\":\"ACTIVE\",\"appliedPlantChangeReportId\":null}]}],\"appliedPlantChangeReportIds\":[]},\"causationId\":\"00000005-0000-0000-0000-000000000000\",\"sourceSystem\":\"BE1\"}";
        Assert.Equal(expected, json);
        var copy = serializer.Deserialize<FarmBaseMapPublishedV3>(serializer.Serialize(envelope));
        Assert.NotNull(copy);
        Assert.Empty(V3BusinessContractValidators.Validate(copy.Payload));
    }

    private sealed class FakeQuery(Guid orderId) : ISurveyOrderOperationalContextV3Query
    {
        public DateTimeOffset BaselineStart { get; } =
            new(2026, 10, 10, 2, 0, 0, TimeSpan.Zero);

        public Task<SurveyOrderOperationalContextV3?> GetAsync(
            Guid surveyOrderId, string requestedMissionPurpose,
            CancellationToken cancellationToken = default)
        {
            if (surveyOrderId != orderId)
                return Task.FromResult<SurveyOrderOperationalContextV3?>(null);

            var baseline = requestedMissionPurpose == nameof(MissionPurpose.BaselineMapping);
            SurveyOrderOperationalContextV3 context = new(
                orderId, Guid.NewGuid(), Guid.NewGuid(), "PLANT_HEALTH", "PLANT_HEALTH",
                requestedMissionPurpose, "BaselineReady", 1,
                Guid.NewGuid(), null, true, null, null, null, null, null,
                baseline ? Guid.NewGuid() : null,
                baseline ? "BaselineMapping" : null,
                baseline ? BaselineStart : null,
                baseline ? BaselineStart.AddHours(1) : null,
                baseline ? "Confirmed" : null,
                null, null, Guid.NewGuid(), null, baseline,
                baseline ? [] : ["PaymentNotConfirmed"], BaselineStart);
            return Task.FromResult<SurveyOrderOperationalContextV3?>(context);
        }
    }

    private sealed class SingleContextQuery(SurveyOrderOperationalContextV3 context)
        : ISurveyOrderOperationalContextV3Query
    {
        public Task<SurveyOrderOperationalContextV3?> GetAsync(
            Guid surveyOrderId, string requestedMissionPurpose,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyOrderOperationalContextV3?>(
                surveyOrderId == context.SurveyOrderId ? context : null);
    }
}
