using System.Text;
using System.Text.Json.Nodes;
using AgriDrone.IntegrationContracts.HarvestReadiness;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class CrossServiceGoldenEventFixtureTests
{
    private static readonly string FixtureDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "contracts",
        "events");

    public static TheoryData<string> Be1InboundFixtures =>
        new()
        {
            "mapping.candidates-approved.v1.event.json",
            "mapping.baseline-candidates-approved.v2.event.json",
            "health.observations-ready.v1.event.json",
            "health.observations-ready.v2.event.json",
            "harvest-readiness.assessments-ready.v2.event.json"
        };

    public static TheoryData<string> Be2InboundFixtures =>
        new()
        {
            "mapping.zone-map-published.v1.event.json",
            "mapping.farm-base-map-published.v2.event.json",
            "health.review-state-changed.v1.event.json",
            "survey.result-review-state-changed.v2.event.json"
        };

    [Theory]
    [MemberData(nameof(Be1InboundFixtures))]
    public void Be1ConsumesCanonicalCrossServiceFixture(string fixtureName) =>
        AssertFixtureRoundTrips(fixtureName);

    [Theory]
    [MemberData(nameof(Be2InboundFixtures))]
    public void Be2ConsumesCanonicalCrossServiceFixture(string fixtureName) =>
        AssertFixtureRoundTrips(fixtureName);

    [Theory]
    [InlineData("identity.tenant-invitation-email-requested.v1.event.json")]
    [InlineData("notification.email-requested.v1.event.json")]
    public void Be1NotificationPipelineConsumesCanonicalFixture(
        string fixtureName) =>
        AssertFixtureRoundTrips(fixtureName);

    [Fact]
    public void FixtureSetCoversEveryRegisteredIntegrationEvent()
    {
        var fixtureEventTypes = Directory
            .EnumerateFiles(FixtureDirectory, "*.event.json")
            .Select(File.ReadAllText)
            .Select(json => JsonNode.Parse(json))
            .Select(node => node!["eventType"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        var registeredEventTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            IntegrationEventDescriptors.MappingCandidatesApprovedV1.EventType,
            IntegrationEventDescriptors.ZoneMapPublishedV1.EventType,
            IntegrationEventDescriptors.TenantInvitationEmailRequestedV1.EventType,
            IntegrationEventDescriptors.EmailNotificationRequestedV1.EventType,
            IntegrationEventDescriptors.HealthObservationsReadyV1.EventType,
            IntegrationEventDescriptors.HealthReviewStateChangedV1.EventType,
            IntegrationEventDescriptors.BaselineMappingCandidatesApprovedV2.EventType,
            IntegrationEventDescriptors.FarmBaseMapPublishedV2.EventType,
            IntegrationEventDescriptors.HealthObservationsReadyV2.EventType,
            IntegrationEventDescriptors.HarvestReadinessAssessmentsReadyV2.EventType,
            IntegrationEventDescriptors.SurveyResultReviewStateChangedV2.EventType
        };

        Assert.True(
            registeredEventTypes.SetEquals(fixtureEventTypes),
            "Golden fixture event types do not match the registered integration event descriptors.");
    }

    private static void AssertFixtureRoundTrips(string fixtureName)
    {
        var json = File.ReadAllText(Path.Combine(FixtureDirectory, fixtureName));
        var eventType = JsonNode.Parse(json)?["eventType"]?.GetValue<string>();

        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<IIntegrationMessageSerializer>();

        var serialized = eventType switch
        {
            IntegrationEventTypes.MappingCandidatesApprovedV1 =>
                RoundTrip<MappingCandidatesApprovedV1>(serializer, json),
            IntegrationEventTypes.ZoneMapPublishedV1 =>
                RoundTrip<ZoneMapPublishedV1>(serializer, json),
            IntegrationEventTypes.TenantInvitationEmailRequestedV1 =>
                RoundTrip<TenantInvitationEmailRequestedV1>(serializer, json),
            IntegrationEventTypes.EmailNotificationRequestedV1 =>
                RoundTrip<EmailNotificationRequestedV1>(serializer, json),
            IntegrationEventTypes.HealthObservationsReadyV1 =>
                RoundTrip<HealthObservationsReadyV1>(serializer, json),
            IntegrationEventTypes.HealthReviewStateChangedV1 =>
                RoundTrip<HealthReviewStateChangedV1>(serializer, json),
            IntegrationEventTypes.BaselineMappingCandidatesApprovedV2 =>
                RoundTrip<BaselineMappingCandidatesApprovedV2>(serializer, json),
            IntegrationEventTypes.FarmBaseMapPublishedV2 =>
                RoundTrip<FarmBaseMapPublishedV2>(serializer, json),
            IntegrationEventTypes.HealthObservationsReadyV2 =>
                RoundTrip<HealthObservationsReadyV2>(serializer, json),
            IntegrationEventTypes.HarvestReadinessAssessmentsReadyV2 =>
                RoundTrip<HarvestReadinessAssessmentsReadyV2>(serializer, json),
            IntegrationEventTypes.SurveyResultReviewStateChangedV2 =>
                RoundTrip<SurveyResultReviewStateChangedV2>(serializer, json),
            _ => throw new InvalidOperationException(
                $"Fixture '{fixtureName}' has an unknown event type '{eventType}'.")
        };

        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(serialized)),
            $"Fixture '{fixtureName}' does not match the production C# serializer.");
    }

    private static string RoundTrip<TPayload>(
        IIntegrationMessageSerializer serializer,
        string json)
    {
        var envelope = serializer.Deserialize<TPayload>(Encoding.UTF8.GetBytes(json));
        Assert.NotNull(envelope);
        return Encoding.UTF8.GetString(serializer.Serialize(envelope));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=agridrone-golden-contract-tests;Username=test;Password=test",
                ["RabbitMq:Enabled"] = "false",
                ["Messaging:Outbox:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddIntegrationMessagingFoundation(configuration);
        return services.BuildServiceProvider();
    }
}
