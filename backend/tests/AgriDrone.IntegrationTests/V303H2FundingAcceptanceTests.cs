using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303H2FundingAcceptanceTests : V303H2IntegrationTestBase
{
    public V303H2FundingAcceptanceTests() : base("v303h2_fund")
    {
    }

    [Theory]
    [InlineData("First", "MAP")]
    [InlineData("First", "COMBO")]
    [InlineData("Follow-up", "MAP")]
    [InlineData("Follow-up", "COMBO")]
    public async Task Scenario03T01MatrixFundingEnforcesDepositGateForCapture(string intent, string serviceCode)
    {
        // 1. Order awaiting deposit without satisfied obligation
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            initialStatus: SurveyOrderStatus.AwaitingDeposit,
            isSatisfiedDeposit: false,
            hasConfirmedAppointment: false,
            serviceCode: serviceCode);

        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        // Attempting to prepare capture when deposit is unsatisfied must fail
        var orderVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVer,
            seeded.ManagerUserId,
            seeded.DroneId,
            $"prep-{intent}-{serviceCode}",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));

        Assert.False(prepRes.Success);
        Assert.Equal("ORDER_NOT_READY", prepRes.ErrorCode);

        // 2. Add partial funding allocation (50,000 VND < 100,000 VND)
        var receiptId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $"""
                INSERT INTO survey.survey_funding_receipts
                  (id, tenant_id, survey_order_id, deposit_obligation_id, provider,
                   provider_transaction_id, amount, currency, payment_reference,
                   payload_fingerprint, received_at, created_at)
                VALUES
                  ('{receiptId}', '{seeded.TenantId}', '{seeded.OrderId}', '{seeded.ObligationId}', 'SEPAY',
                   'TX-PARTIAL', 50000, 'VND', 'REF-PARTIAL', 'FP-PARTIAL',
                   NOW(), NOW());

                UPDATE survey.survey_deposit_obligations
                SET allocated_amount = 50000
                WHERE id = '{seeded.ObligationId}';

                INSERT INTO survey.survey_funding_allocations
                  (id, funding_receipt_id, survey_order_id, deposit_obligation_id,
                   allocated_amount, currency, allocated_at)
                VALUES
                  (gen_random_uuid(), '{receiptId}', '{seeded.OrderId}', '{seeded.ObligationId}',
                   50000, 'VND', NOW());
                """);
        }

        // Check obligation remains unsatisfied
        await using (var verifyCtx = CreateContext(DatabaseName))
        {
            var obligation = await verifyCtx.Set<SurveyDepositObligation>()
                .AsNoTracking()
                .SingleAsync(o => o.Id == seeded.ObligationId);
            Assert.False(obligation.IsSatisfied);
            Assert.Equal(50000m, obligation.AllocatedAmount);
        }

        // 3. Satisfy full remaining deposit (another 50,000 VND)
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            var secondReceiptId = Guid.NewGuid();
            await ExecuteAsync(conn, $"""
                INSERT INTO survey.survey_funding_receipts
                  (id, tenant_id, survey_order_id, deposit_obligation_id, provider,
                   provider_transaction_id, amount, currency, payment_reference,
                   payload_fingerprint, received_at, created_at)
                VALUES
                  ('{secondReceiptId}', '{seeded.TenantId}', '{seeded.OrderId}', '{seeded.ObligationId}', 'SEPAY',
                   'TX-FULL', 50000, 'VND', 'REF-FULL', 'FP-FULL',
                   NOW(), NOW());

                UPDATE survey.survey_deposit_obligations
                SET allocated_amount = 100000, status = 'SATISFIED'::system.survey_deposit_obligation_status,
                    satisfied_at = NOW()
                WHERE id = '{seeded.ObligationId}';

                UPDATE survey.survey_orders
                SET deposit_payment_status = 'PAID'::system.survey_payment_status,
                    status = 'READY_FOR_CAPTURE'::system.survey_order_status,
                    updated_at = NOW()
                WHERE id = '{seeded.OrderId}';

                UPDATE survey.survey_appointments
                SET status = 'CONFIRMED'::system.survey_appointment_status,
                    confirmed_by_tenant_owner_id = '{seeded.OwnerUserId}',
                    confirmed_at = NOW(),
                    updated_at = NOW()
                WHERE survey_order_id = '{seeded.OrderId}';
                """);
        }

        // Now prepare capture must succeed
        await using (var readyCtx = CreateContext(DatabaseName))
        {
            var readyCoordinator = CreateCoordinator(readyCtx);
            var readyOrderVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
            var prepSuccessRes = await readyCoordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
                seeded.OrderId,
                readyOrderVer,
                seeded.ManagerUserId,
                seeded.DroneId,
                $"prep-full-{intent}-{serviceCode}",
                Guid.NewGuid(),
                Guid.NewGuid(),
                "prep-full-fp"));

            Assert.True(prepSuccessRes.Success, prepSuccessRes.ErrorMessage);
        }
    }

    [Fact]
    public async Task Scenario03T02IdempotentDuplicateCallbackAndLatePaymentAfterCancellation()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName, initialStatus: SurveyOrderStatus.AwaitingDeposit, isSatisfiedDeposit: false);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        // Pre-create an initiated survey payment for the deposit obligation so the provider callback matches it
        var paymentId = Guid.CreateVersion7();
        var providerReference = $"REF-{Guid.NewGuid():N}";
        await using (var npgConn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await npgConn.OpenAsync();
            await ExecuteAsync(npgConn, $"""
                INSERT INTO survey.survey_payments
                  (id, survey_order_id, deposit_obligation_id, amount, currency, provider, provider_reference, status, purpose, created_at, updated_at)
                VALUES
                  ('{paymentId}', '{seeded.OrderId}', '{seeded.ObligationId}', 100000, 'VND', 'sepay', '{providerReference}', 'PROCESSING'::system.survey_payment_status, 'OrderDeposit', NOW(), NOW());
                """);
        }

        // 1. Cancel order while awaiting deposit
        var orderVerCancel = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var cancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVerCancel,
            seeded.OwnerUserId,
            seeded.OwnerUserId,
            CaptureCancellationDecisionKind.CustomerBeforeCapture,
            0m,
            null,
            "CANCELLATION_REQUESTED",
            "Customer cancelled survey order before payment",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cancel-fp"));
        Assert.True(cancelRes.Success, cancelRes.ErrorMessage);

        // Verify order is Cancelled
        var cancelledOrder = await context.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.Cancelled, cancelledOrder.Status);

        // 2. Late incoming payment arrival via ProcessSurveyPaymentEventCommandHandler
        var payBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(DatabaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(payBuilder);
        var payDataSource = payBuilder.Build();
        var payOptions = new DbContextOptionsBuilder<SurveyPaymentProcessingDbContext>()
            .UseNpgsql(payDataSource, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                PostgreSqlEnumMappings.ConfigureOptions(npgsql);
            })
            .Options;
        await using var payDbContext = new SurveyPaymentProcessingDbContext(payOptions);
        var payRepo = new SurveyPaymentProcessingRepository(payDbContext);
        var clock = TimeProvider.System;
        var payOutbox = new SurveyPaymentProcessingOutbox(
            payDbContext,
            new OutboxMessageFactory(new SystemTextJsonIntegrationMessageSerializer(), clock));
        var execContext = new TestExecutionContext(seeded.OwnerUserId);
        var handler = new ProcessSurveyPaymentEventCommandHandler(payDbContext, payRepo, payOutbox, execContext);

        using var payload = JsonDocument.Parse("{}");
        var paymentCmd = new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            Guid.NewGuid().ToString("N"),
            "payment.succeeded",
            100000m,
            "VND",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            payload);

        var result = await handler.Handle(paymentCmd, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error.Description);

        // 3. Verify order remains Cancelled (not resurrected) and refund obligation created
        await using var verifyContext = CreateContext(DatabaseName);
        var postPaymentOrder = await verifyContext.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.Cancelled, postPaymentOrder.Status);

        var refundObligations = await verifyContext.Set<SurveyRefundObligation>()
            .Where(l => l.SurveyOrderId == seeded.OrderId)
            .ToListAsync();
        Assert.NotEmpty(refundObligations);
        Assert.Contains(refundObligations, l => l.Amount >= 100000m);

        // 4. Duplicate webhook callback with identical provider event
        var dupResult = await handler.Handle(paymentCmd, CancellationToken.None);
        Assert.True(dupResult.IsSuccess);
        Assert.True(dupResult.Value.Duplicate);

        // Confirm refund obligations were not duplicated
        var refundCount = await verifyContext.Set<SurveyRefundObligation>()
            .CountAsync(l => l.SurveyOrderId == seeded.OrderId);
        Assert.Equal(1, refundCount);
    }
}
