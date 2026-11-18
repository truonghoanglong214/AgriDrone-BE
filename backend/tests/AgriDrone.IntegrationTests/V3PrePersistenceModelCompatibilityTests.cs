using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.IntegrationTests;

// Real Npgsql models and SQL translation, without a database connection.
// PostgreSQL migration/constraint acceptance is covered separately with a live fixture.
public sealed class V3PersistenceModelCompatibilityTests
{
    [Fact]
    public void ExistingContextsMapExpandedV3OrderAndHistoryProperties()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=127.0.0.1;Port=1;Database=v3_model_test;Username=test;Password=test;Timeout=1"
            })
            .Build();
        var services = new ServiceCollection()
            .AddAgriDroneDatabase(configuration)
            .AddMappingPublicationPersistence(configuration)
            .AddSurveyResultPublicationPersistence(configuration)
            .AddSurveyApprovalPersistence(configuration)
            .AddSurveyBoundaryVerificationPersistence(configuration)
            .AddSurveyAppointmentSchedulingPersistence(configuration)
            .AddSurveyPricingConfirmationPersistence(configuration)
            .AddSurveyPaymentProcessingPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var contexts = new DbContext[]
        {
            scope.ServiceProvider.GetRequiredService<AgriDroneSchemaDbContext>(),
            scope.ServiceProvider.GetRequiredService<MappingPublicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyResultPublicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyApprovalDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyBoundaryVerificationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyAppointmentSchedulingDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyPricingConfirmationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyPaymentProcessingDbContext>()
        };
        var checkedOrderModels = 0;
        var checkedHistoryModels = 0;

        foreach (var context in contexts)
        {
            var order = context.Model.FindEntityType(typeof(SurveyOrder));
            if (order is not null)
            {
                checkedOrderModels++;
                var property = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyProperty>(
                    order.FindProperty(nameof(SurveyOrder.DepositObligationId)));
                Assert.Equal("deposit_obligation_id", property.GetColumnName());
                var sql = context.Set<SurveyOrder>().AsNoTracking().ToQueryString();
                Assert.Contains("deposit_obligation_id", sql, StringComparison.OrdinalIgnoreCase);
            }

            var history = context.Model.FindEntityType(typeof(OrderStateHistory));
            if (history is not null)
            {
                checkedHistoryModels++;
                var property = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyProperty>(
                    history.FindProperty(nameof(OrderStateHistory.TransitionSequence)));
                Assert.Equal("transition_sequence", property.GetColumnName());
                var sql = context.Set<OrderStateHistory>().AsNoTracking().ToQueryString();
                Assert.Contains("transition_sequence", sql, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Equal(contexts.Length, checkedOrderModels);
        Assert.True(checkedHistoryModels > 0);
    }

    [Fact]
    public void CanonicalSchemaModelMapsCaptureAttemptEvidenceAndMissionBinding()
    {
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=v3_model_test;Username=test;Password=test;Timeout=1",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new AgriDroneSchemaDbContext(options);
        var attempt = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            context.Model.FindEntityType(typeof(CaptureAttempt)));
        var history = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            context.Model.FindEntityType(typeof(CaptureAttemptHistory)));
        var manifest = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            context.Model.FindEntityType(typeof(CaptureInputManifest)));
        var approval = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            context.Model.FindEntityType(typeof(CaptureInputApproval)));
        var mission = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            context.Model.FindEntityType(typeof(AgriDrone.Modules.Missions.Domain.Missions.DroneMission)));

        Assert.Equal("capture_attempts", attempt.GetTableName());
        Assert.Equal("capture_attempt_history", history.GetTableName());
        Assert.Equal("capture_input_manifests", manifest.GetTableName());
        Assert.Equal("capture_input_approvals", approval.GetTableName());
        Assert.Equal("capture_attempt_id", mission.FindProperty("CaptureAttemptId")!.GetColumnName());
        Assert.Null(attempt.FindProperty(nameof(CaptureAttempt.MissionId)));

        Assert.Contains(attempt.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(CaptureAttempt.OrderId), nameof(CaptureAttempt.AttemptIndex) }));
        Assert.Contains(mission.GetIndexes(), index =>
            index.IsUnique && index.Properties.Count == 1 &&
            index.Properties[0].Name == "CaptureAttemptId");
        Assert.Contains(manifest.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CaptureAttempt));
        Assert.Contains(approval.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CaptureInputManifest));
    }

    [Fact]
    public void CanonicalSchemaModelMapsFundingAndCancellationFacts()
    {
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=v3_model_test;Username=test;Password=test;Timeout=1",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;

        using var context = new AgriDroneSchemaDbContext(options);
        var deposit = context.Model.FindEntityType(typeof(SurveyDepositObligation))!;
        var receipt = context.Model.FindEntityType(typeof(SurveyFundingReceipt))!;
        var allocation = context.Model.FindEntityType(typeof(SurveyFundingAllocation))!;
        var decision = context.Model.FindEntityType(typeof(SurveyCancellationDecision))!;
        var refund = context.Model.FindEntityType(typeof(SurveyRefundObligation))!;
        var retention = context.Model.FindEntityType(typeof(CancellationRetention))!;

        Assert.Equal("survey_deposit_obligations", deposit.GetTableName());
        Assert.Equal("survey_funding_receipts", receipt.GetTableName());
        Assert.Equal("survey_funding_allocations", allocation.GetTableName());
        Assert.Equal("survey_cancellation_decisions", decision.GetTableName());
        Assert.Equal("survey_refund_obligations", refund.GetTableName());
        Assert.Equal("cancellation_retentions", retention.GetTableName());

        Assert.Contains(deposit.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(SurveyDepositObligation.SurveyOrderId) }));
        Assert.Contains(receipt.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SurveyFundingReceipt.Provider),
                    nameof(SurveyFundingReceipt.ProviderTransactionId)
                }));
        Assert.Contains(refund.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SurveyRefundObligation.SurveyOrderId),
                    nameof(SurveyRefundObligation.OriginKind),
                    nameof(SurveyRefundObligation.OriginDecisionId)
                }));
        Assert.Contains(decision.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SurveyCancellationDecision.SurveyOrderId),
                    nameof(SurveyCancellationDecision.OperationId)
                }));
        Assert.Contains(retention.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(SurveyCancellationDecision));
    }
}
