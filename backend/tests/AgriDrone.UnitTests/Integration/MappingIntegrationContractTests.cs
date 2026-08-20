using System.Text;
using AgriDrone.IntegrationContracts.Contracts;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Mapping.Validation;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.UnitTests.Integration;

public sealed class MappingIntegrationContractTests
{
    private static readonly Guid MessageId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CorrelationId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ActorId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ApprovalId =
        Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid MissionId =
        Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid FarmId =
        Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ZoneId =
        Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ObservationId =
        Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid PlantId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MapVersionId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 20, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    public void MappingCandidatesApprovedRoundTripsThroughProductionSerializer()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<
            IIntegrationMessageSerializer>();
        var envelope = CreateCandidatesApprovedEnvelope();

        var body = serializer.Serialize(envelope);
        var actual = serializer.Deserialize<MappingCandidatesApprovedV1>(body);

        Assert.NotNull(actual);
        Assert.Equal(envelope.MessageId, actual.MessageId);
        Assert.Equal(envelope.CorrelationId, actual.CorrelationId);
        Assert.Equal(envelope.TenantId, actual.TenantId);
        Assert.Equal(envelope.ActorId, actual.ActorId);
        Assert.Equal(envelope.OccurredAt, actual.OccurredAt);
        Assert.Equal(envelope.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(envelope.EventType, actual.EventType);
        Assert.Equal(ApprovalId, actual.Payload.ApprovalId);
        Assert.Equal(MissionId, actual.Payload.MissionId);
        Assert.Equal(
            MappingCandidateDecisions.CreateNew,
            Assert.Single(actual.Payload.Candidates).Decision);
    }

    [Fact]
    public void ZoneMapPublishedRoundTripsThroughProductionSerializer()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<
            IIntegrationMessageSerializer>();
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.ZoneMapPublishedV1,
            MessageId,
            CorrelationId,
            TenantId,
            ActorId,
            OccurredAt,
            new ZoneMapPublishedV1(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                ApprovalId,
                MissionId,
                FarmId,
                ZoneId,
                MapVersionId,
                3,
                OccurredAt,
                [new PlantMappingV1(ObservationId, PlantId, true)]));

        var body = serializer.Serialize(envelope);
        var actual = serializer.Deserialize<ZoneMapPublishedV1>(body);

        Assert.NotNull(actual);
        Assert.Equal(ApprovalId, actual.Payload.ApprovalId);
        Assert.Equal(MapVersionId, actual.Payload.MapVersionId);
        var mapping = Assert.Single(actual.Payload.PlantMappings);
        Assert.Equal(ObservationId, mapping.ObservationId);
        Assert.Equal(PlantId, mapping.PlantId);
        Assert.True(mapping.WasCreated);
    }

    [Fact]
    public void MappingCandidatesApprovedWireJsonMatchesGoldenContract()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<
            IIntegrationMessageSerializer>();

        var json = Encoding.UTF8.GetString(
            serializer.Serialize(CreateCandidatesApprovedEnvelope()));

        const string expected =
            "{\"messageId\":\"11111111-1111-1111-1111-111111111111\",\"correlationId\":\"22222222-2222-2222-2222-222222222222\",\"tenantId\":\"33333333-3333-3333-3333-333333333333\",\"actorId\":\"44444444-4444-4444-4444-444444444444\",\"occurredAt\":\"2026-08-20T01:02:03+00:00\",\"schemaVersion\":1,\"eventType\":\"mapping.candidates-approved.v1\",\"payload\":{\"approvalId\":\"55555555-5555-5555-5555-555555555555\",\"missionId\":\"66666666-6666-6666-6666-666666666666\",\"farmId\":\"77777777-7777-7777-7777-777777777777\",\"zoneId\":\"88888888-8888-8888-8888-888888888888\",\"expectedCurrentMapVersionId\":null,\"algorithmVersion\":\"grid-v1\",\"gridBearingDeg\":12.5,\"rowSpacingM\":3.25,\"plantSpacingM\":1.5,\"parameters\":{\"model\":\"mapping-v4\"},\"candidates\":[{\"observationId\":\"99999999-9999-9999-9999-999999999999\",\"resolvedPlantId\":null,\"latitude\":10.75,\"longitude\":106.67,\"rowIndex\":1,\"columnIndex\":2,\"locationAccuracyM\":0.25,\"positionConfidence\":0.98,\"decision\":\"create-new\"}]}}";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void ReaderRejectsUnsupportedSchemaVersion()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<
            IIntegrationMessageSerializer>();
        var reader = provider.GetRequiredService<IIntegrationMessageReader>();
        var valid = CreateCandidatesApprovedEnvelope();
        var unsupported = valid with
        {
            SchemaVersion = IntegrationSchemaVersions.V1 + 1
        };

        var result = reader.Read(
            serializer.Serialize(unsupported),
            IntegrationEventDescriptors.MappingCandidatesApprovedV1,
            MappingCandidatesApprovedV1Validator.Validate);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Errors,
            error =>
                error.Code == IntegrationMessageErrorCodes.EnvelopeInvalid &&
                error.Message.Contains(
                    "Unsupported SchemaVersion",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void SerializerIgnoresUnknownAdditiveField()
    {
        using var provider = CreateServiceProvider();
        var serializer = provider.GetRequiredService<
            IIntegrationMessageSerializer>();
        var json = Encoding.UTF8.GetString(
            serializer.Serialize(CreateCandidatesApprovedEnvelope()));
        var withUnknownField = json.Replace(
            "\"payload\":",
            "\"futureEnvelopeField\":\"ignored\",\"payload\":",
            StringComparison.Ordinal);

        var actual = serializer.Deserialize<MappingCandidatesApprovedV1>(
            Encoding.UTF8.GetBytes(withUnknownField));

        Assert.NotNull(actual);
        Assert.Equal(MessageId, actual.MessageId);
        Assert.Equal(ApprovalId, actual.Payload.ApprovalId);
    }

    [Fact]
    public void MappingCandidatesApprovedRequiresBusinessApprovalId()
    {
        var invalid = CreateCandidatesApprovedEnvelope().Payload with
        {
            ApprovalId = Guid.Empty
        };

        var errors = MappingCandidatesApprovedV1Validator.Validate(invalid);

        Assert.Contains("ApprovalId is required.", errors);
    }

    [Fact]
    public void EnvelopeValidatorAllowsOldEventsForReplay()
    {
        var envelope = CreateCandidatesApprovedEnvelope() with
        {
            OccurredAt = OccurredAt.AddYears(-5)
        };

        var error = IntegrationEnvelopeValidator.Validate(
            envelope,
            IntegrationEventTypes.MappingCandidatesApprovedV1,
            IntegrationSchemaVersions.V1,
            OccurredAt);

        Assert.Null(error);
    }

    [Fact]
    public void ContractsAssemblyDoesNotReferenceEfOrDomainModules()
    {
        var references = typeof(MappingCandidatesApprovedV1)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(
            references,
            name => name.StartsWith(
                "Microsoft.EntityFrameworkCore",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            name => name.StartsWith(
                "AgriDrone.Modules.",
                StringComparison.Ordinal));
    }

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

    private static IntegrationEventEnvelope<MappingCandidatesApprovedV1>
        CreateCandidatesApprovedEnvelope() =>
        IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.MappingCandidatesApprovedV1,
            MessageId,
            CorrelationId,
            TenantId,
            ActorId,
            OccurredAt,
            new MappingCandidatesApprovedV1(
                ApprovalId,
                MissionId,
                FarmId,
                ZoneId,
                ExpectedCurrentMapVersionId: null,
                AlgorithmVersion: "grid-v1",
                GridBearingDeg: 12.5,
                RowSpacingM: 3.25,
                PlantSpacingM: 1.5,
                new Dictionary<string, string>
                {
                    ["model"] = "mapping-v4"
                },
                [
                    new MappingCandidateV1(
                        ObservationId,
                        ResolvedPlantId: null,
                        Latitude: 10.75,
                        Longitude: 106.67,
                        RowIndex: 1,
                        ColumnIndex: 2,
                        LocationAccuracyM: 0.25,
                        PositionConfidence: 0.98,
                        MappingCandidateDecisions.CreateNew)
                ]));
}
