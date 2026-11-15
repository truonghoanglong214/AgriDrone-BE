using System.Text.Json;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303H2CompatibilityActivationTests : V303H2IntegrationTestBase
{
    public V303H2CompatibilityActivationTests() : base("v303h2_compat")
    {
    }

    [Fact]
    public async Task Scenario03T12LegacyOrderIsStrictlyExcludedFromV3CaptureFlows()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        var orderVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);

        // Downgrade scope to Legacy
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $"""
                UPDATE survey.survey_orders
                SET scope = 'LEGACY'::system.contract_scope,
                    business_policy_version_id = NULL
                WHERE id = '{seeded.OrderId}';
                """);
        }

        var orderVerPostUpdate = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);

        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        // V3 coordinator must strictly reject legacy order
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPostUpdate,
            seeded.ManagerUserId,
            seeded.DroneId,
            "legacy-prep",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "legacy-fp"));

        Assert.False(prepRes.Success);
        Assert.Equal("ORDER_NOT_READY", prepRes.ErrorCode);
    }

    [Fact]
    public async Task Scenario03T13PolicyVersioningAndPublicFlagsGuard()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);

        // 1. Verify commercial snapshot preserves version 1 terms
        var snapshot = await context.Set<SurveyOrderCommercialSnapshot>()
            .AsNoTracking()
            .SingleAsync(s => s.SurveyOrderId == seeded.OrderId);

        Assert.Equal(1, snapshot.DepositVersionNumber);
        Assert.Equal(100000m, snapshot.DepositAmount);
        Assert.Equal(1, snapshot.CancellationPolicyVersionNumber);
        Assert.True(snapshot.RefundableBeforeCapture);
        Assert.True(snapshot.RetainDepositAfterCapture);

        // 2. Publish a new policy version 2 with different parameters (e.g. 200,000 VND deposit)
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            var newDepositPolicyId = Guid.NewGuid();
            await ExecuteAsync(conn, $"""
                INSERT INTO survey.v3_survey_deposit_policies (id, version_number, amount, currency, effective_from, created_by, created_at, reason_code)
                VALUES ('{newDepositPolicyId}', 2, 200000, 'VND', NOW(), '{seeded.AdminUserId}', NOW(), 'V2_INCREASE');
                """);
        }

        // 3. Verify Order snapshot is immutable and still reflects version 1 (100,000 VND)
        await using (var verifyCtx = CreateContext(DatabaseName))
        {
            var verifiedSnapshot = await verifyCtx.Set<SurveyOrderCommercialSnapshot>()
                .AsNoTracking()
                .SingleAsync(s => s.SurveyOrderId == seeded.OrderId);

            Assert.Equal(1, verifiedSnapshot.DepositVersionNumber);
            Assert.Equal(100000m, verifiedSnapshot.DepositAmount);
        }

        // 4. Verify public configuration flags in appsettings.json remain strictly OFF
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var surveyPaymentsEnabled = config.GetValue<bool>("SurveyPayments:Enabled");
        Assert.False(surveyPaymentsEnabled, "SurveyPayments:Enabled must remain false in production configuration!");
    }

    [Fact]
    public async Task Scenario03T13OutboxEventPersistedWithCorrectSchemaOnMutation()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerPrep = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var correlationId = Guid.NewGuid();
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-fp",
            correlationId,
            Guid.NewGuid(),
            "prep-fp"));

        Assert.True(prepRes.Success, prepRes.ErrorMessage);

        // Verify Outbox message was created in the same transaction
        await using var verifyCtx = CreateContext(DatabaseName);
        var outboxMessages = await verifyCtx.Set<OutboxMessage>()
            .Where(m => m.PartitionKey == seeded.OrderId.ToString("D") && m.EventType == IntegrationEventTypes.SurveyCaptureStateChangedV1)
            .ToListAsync();

        Assert.NotEmpty(outboxMessages);
        var message = outboxMessages.Last();
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        var payloadJson = System.Text.Encoding.UTF8.GetString(message.Body);
        Assert.Contains("PREPARED", payloadJson);
    }
}
