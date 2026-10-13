using System.Text;
using AgriDrone.IntegrationContracts.HarvestReadiness;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class V2BusinessIntegrationContractTests
{
    private static readonly Guid MessageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CorrelationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CausationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OrderId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid MissionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid FarmId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 9, 26, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public void V1DescriptorsRemainImmutableWhenV2IsAdded()
    {
        Assert.Equal(1, IntegrationEventDescriptors.MappingCandidatesApprovedV1.SchemaVersion);
        Assert.Equal("mapping.candidates-approved.v1", IntegrationEventDescriptors.MappingCandidatesApprovedV1.EventType);
        Assert.Equal(1, IntegrationEventDescriptors.HealthObservationsReadyV1.SchemaVersion);
        Assert.Equal("health.observations-ready.v1", IntegrationEventDescriptors.HealthObservationsReadyV1.EventType);
    }

    [Fact]
    public void V2EnvelopeRoundTripsOrderFarmAndCausationContext()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<IIntegrationMessageSerializer>();
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.FarmBaseMapPublishedV2,
            MessageId,
            CorrelationId,
            TenantId,
            ActorId,
            OccurredAt,
            new FarmBaseMapPublishedV2(
                CausationId,
                Guid.NewGuid(),
                OrderId,
                MissionId,
                FarmId,
                Guid.NewGuid(),
                1,
                OccurredAt,
                [new PublishedZoneMapV2(Guid.NewGuid(), Guid.NewGuid(), 1, [])]));

        var actual = serializer.Deserialize<FarmBaseMapPublishedV2>(
            serializer.Serialize(envelope));

        Assert.NotNull(actual);
        Assert.Equal(IntegrationSchemaVersions.V2, actual.SchemaVersion);
        Assert.Equal(OrderId, actual.Payload.SurveyOrderId);
        Assert.Equal(FarmId, actual.Payload.FarmId);
        Assert.Equal(CausationId, actual.Payload.CausationId);
    }

    [Fact]
    public void V2ReviewStateWireJsonMatchesGoldenContract()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<IIntegrationMessageSerializer>();
        var envelope = CreateReviewStateEnvelope();

        var json = Encoding.UTF8.GetString(serializer.Serialize(envelope));

        const string expected =
            "{\"messageId\":\"11111111-1111-1111-1111-111111111111\",\"correlationId\":\"22222222-2222-2222-2222-222222222222\",\"tenantId\":\"33333333-3333-3333-3333-333333333333\",\"actorId\":null,\"occurredAt\":\"2026-09-26T01:02:03+00:00\",\"schemaVersion\":2,\"eventType\":\"survey.result-review-state-changed.v2\",\"payload\":{\"causationId\":\"55555555-5555-5555-5555-555555555555\",\"handoffId\":\"99999999-9999-9999-9999-999999999999\",\"surveyOrderId\":\"66666666-6666-6666-6666-666666666666\",\"missionId\":\"77777777-7777-7777-7777-777777777777\",\"farmId\":\"88888888-8888-8888-8888-888888888888\",\"surveyResultId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"serviceType\":\"PLANT_HEALTH\",\"reviewVersion\":3,\"state\":\"PUBLISHED\",\"totalItems\":10,\"pendingItems\":0,\"correctedItems\":2,\"changedAt\":\"2026-09-26T01:02:03+00:00\"}}";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void V2ReaderUsesIndependentDescriptorAndValidation()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<IIntegrationMessageSerializer>();
        var reader = provider.GetRequiredService<IIntegrationMessageReader>();

        var result = reader.Read(
            serializer.Serialize(CreateReviewStateEnvelope()),
            IntegrationEventDescriptors.SurveyResultReviewStateChangedV2,
            V2BusinessContractValidators.Validate);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void EveryV2HandoffRequiresOrderFarmMissionAndCausation()
    {
        var healthErrors = V2BusinessContractValidators.Validate(
            new HealthObservationsReadyV2(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "model-v2",
                null,
                null,
                []));
        var readinessErrors = V2BusinessContractValidators.Validate(
            new HarvestReadinessAssessmentsReadyV2(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "model-v2",
                "criteria-v1",
                "FARM",
                []));

        foreach (var errors in new[] { healthErrors, readinessErrors })
        {
            Assert.Contains("CausationId is required.", errors);
            Assert.Contains("SurveyOrderId is required.", errors);
            Assert.Contains("MissionId is required.", errors);
            Assert.Contains("FarmId is required.", errors);
        }
    }

    [Fact]
    public void MappingV2ValidatorsAcceptCompleteOrderBoundPayloads()
    {
        var baseline = new BaselineMappingCandidatesApprovedV2(
            CausationId,
            Guid.NewGuid(),
            OrderId,
            MissionId,
            FarmId,
            ExpectedCurrentFarmBaseMapVersionId: null,
            "grid-v2",
            new Dictionary<string, string>(),
            [
                new ZoneMappingCandidatesV2(
                    Guid.NewGuid(),
                    ExpectedCurrentZoneMapVersionId: null,
                    12.5,
                    3.0,
                    1.5,
                    [new MappingCandidateV1(Guid.NewGuid(), null, 10.5, 106.5, 1, 1, 0.2, 0.99, "create-new")])
            ]);
        var published = new FarmBaseMapPublishedV2(
            CausationId,
            Guid.NewGuid(),
            OrderId,
            MissionId,
            FarmId,
            Guid.NewGuid(),
            1,
            OccurredAt,
            [new PublishedZoneMapV2(Guid.NewGuid(), Guid.NewGuid(), 1, [])]);

        Assert.Empty(V2BusinessContractValidators.Validate(baseline));
        Assert.Empty(V2BusinessContractValidators.Validate(published));
    }

    [Fact]
    public void V2ContractsAssemblyStaysIndependentFromDomainAndEf()
    {
        var references = typeof(SurveyOrderOperationalContextV2)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, name =>
            name.StartsWith("AgriDrone.Modules.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name =>
            name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    private static IntegrationEventEnvelope<SurveyResultReviewStateChangedV2>
        CreateReviewStateEnvelope() =>
        IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyResultReviewStateChangedV2,
            MessageId,
            CorrelationId,
            TenantId,
            actorId: null,
            OccurredAt,
            new SurveyResultReviewStateChangedV2(
                CausationId,
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                OrderId,
                MissionId,
                FarmId,
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "PLANT_HEALTH",
                3,
                "PUBLISHED",
                10,
                0,
                2,
                OccurredAt));

    private static ServiceProvider CreateServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=agridrone-contract-tests;Username=test;Password=test",
                ["RabbitMq:Enabled"] = "false",
                ["Messaging:Outbox:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddIntegrationMessagingFoundation(configuration);
        return services.BuildServiceProvider();
    }
}
