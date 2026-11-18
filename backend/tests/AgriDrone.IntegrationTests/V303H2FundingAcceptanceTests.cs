using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Application.Errors;
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
    [InlineData(false, "MAP")]
    [InlineData(false, "COMBO")]
    [InlineData(true, "MAP")]
    [InlineData(true, "COMBO")]
    public async Task Scenario03T01MatrixFundingEnforcesDepositGateForCapture(bool isFollowUp, string serviceCode)
    {
        var intent = isFollowUp ? "Follow-up" : "First";
        // 1. Order awaiting deposit without satisfied obligation
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            initialStatus: SurveyOrderStatus.AwaitingDeposit,
            isSatisfiedDeposit: false,
            hasConfirmedAppointment: true,
            serviceCode: serviceCode,
            isFollowUp: isFollowUp);

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

        var paymentId = Guid.CreateVersion7();
        var providerReference = $"REF-{Guid.NewGuid():N}";
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $"""
                INSERT INTO survey.survey_payments
                  (id, survey_order_id, deposit_obligation_id, amount, currency, provider,
                   provider_reference, status, purpose, created_at, updated_at)
                VALUES
                  ('{paymentId}', '{seeded.OrderId}', '{seeded.ObligationId}', 100000, 'VND', 'sepay',
                   '{providerReference}', 'PROCESSING'::system.survey_payment_status, 'OrderDeposit', NOW(), NOW());
                """);
        }

        var payBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(DatabaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(payBuilder);
        await using var configuredPayDataSource = payBuilder.Build();
        var payOptions = new DbContextOptionsBuilder<SurveyPaymentProcessingDbContext>()
            .UseNpgsql(configuredPayDataSource, npgsql =>
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
        var handler = new ProcessSurveyPaymentEventCommandHandler(
            payDbContext,
            payRepo,
            payOutbox,
            new TestExecutionContext(seeded.OwnerUserId));

        var eventTime = DateTimeOffset.UtcNow.AddSeconds(-20);
        using var partialPayload = JsonDocument.Parse("""{"stage":"partial"}""");
        var partialResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            $"evt-partial-{Guid.NewGuid():N}",
            "payment.succeeded",
            50000m,
            "VND",
            eventTime,
            eventTime,
            partialPayload), CancellationToken.None);
        Assert.True(partialResult.IsSuccess, partialResult.Error.Description);
        Assert.Equal("PARTIAL_RECEIPT_ALLOCATED", partialResult.Value.Disposition);

        // 2. A real partial callback records receipt/allocation but keeps the gate closed.
        await using (var verifyCtx = CreateContext(DatabaseName))
        {
            var order = await verifyCtx.Set<SurveyOrder>()
                .AsNoTracking()
                .SingleAsync(o => o.Id == seeded.OrderId);
            var obligation = await verifyCtx.Set<SurveyDepositObligation>()
                .AsNoTracking()
                .SingleAsync(o => o.Id == seeded.ObligationId);
            Assert.Equal(isFollowUp, !order.RequiresBaselineMapping);
            Assert.Equal(SurveyOrderStatus.AwaitingDeposit, order.Status);
            Assert.False(obligation.IsSatisfied);
            Assert.Equal(50000m, obligation.AllocatedAmount);
        }

        // 3. A second real callback satisfies the same pinned obligation.
        using var finalPayload = JsonDocument.Parse("""{"stage":"final"}""");
        var finalTime = eventTime.AddSeconds(1);
        var finalResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            $"evt-final-{Guid.NewGuid():N}",
            "payment.succeeded",
            50000m,
            "VND",
            finalTime,
            finalTime,
            finalPayload), CancellationToken.None);
        Assert.True(finalResult.IsSuccess, finalResult.Error.Description);
        Assert.Equal("DEPOSIT_OBLIGATION_SATISFIED", finalResult.Value.Disposition);

        // Now prepare capture must succeed for all four real intent/service combinations.
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

    [Fact]
    public async Task Scenario03T04FundingReversalClosesCaptureGateAndPersistsImmutableFact()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        var paymentId = Guid.CreateVersion7();
        var providerReference = $"REF-{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"""
                INSERT INTO survey.survey_payments
                  (id, survey_order_id, deposit_obligation_id, amount, currency, provider,
                   provider_reference, status, purpose, confirmed_at, created_at, updated_at)
                VALUES
                  ('{paymentId}', '{seeded.OrderId}', '{seeded.ObligationId}', 100000, 'VND', 'sepay',
                   '{providerReference}', 'PAID'::system.survey_payment_status, 'OrderDeposit',
                   NOW() - INTERVAL '1 minute', NOW() - INTERVAL '1 minute', NOW() - INTERVAL '1 minute');
                """);
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(DatabaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        await using var dataSource = dataSourceBuilder.Build();
        var options = new DbContextOptionsBuilder<SurveyPaymentProcessingDbContext>()
            .UseNpgsql(dataSource, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                PostgreSqlEnumMappings.ConfigureOptions(npgsql);
            })
            .Options;
        await using var paymentContext = new SurveyPaymentProcessingDbContext(options);
        var repository = new SurveyPaymentProcessingRepository(paymentContext);
        var clock = TimeProvider.System;
        var handler = new ProcessSurveyPaymentEventCommandHandler(
            paymentContext,
            repository,
            new SurveyPaymentProcessingOutbox(
                paymentContext,
                new OutboxMessageFactory(new SystemTextJsonIntegrationMessageSerializer(), clock)),
            new TestExecutionContext(seeded.AdminUserId));

        var occurredAt = DateTimeOffset.UtcNow;
        using var payload = JsonDocument.Parse("""{"reason":"provider-chargeback"}""");
        var result = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            $"chargeback-{Guid.NewGuid():N}",
            "payment.chargeback",
            100000m,
            "VND",
            occurredAt,
            occurredAt,
            payload), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Description);
        Assert.Equal("CHARGEBACK_REVERSED_CAPTURE_READINESS", result.Value.Disposition);

        await using var verifyContext = CreateContext(DatabaseName);
        var order = await verifyContext.Set<SurveyOrder>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == seeded.OrderId);
        var obligation = await verifyContext.Set<SurveyDepositObligation>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == seeded.ObligationId);
        var reversal = await verifyContext.Set<SurveyFundingReversal>()
            .AsNoTracking()
            .SingleAsync(value => value.SurveyPaymentId == paymentId);

        Assert.Equal(SurveyOrderStatus.AwaitingDeposit, order.Status);
        Assert.False(obligation.IsSatisfied);
        Assert.Equal(0m, obligation.AllocatedAmount);
        Assert.Equal(100000m, reversal.Amount);
        Assert.Equal("payment.chargeback", reversal.EventType);
    }

    [Fact]
    public async Task Scenario03T01PaymentEventNegativeValidationAndIdempotencyMatrix()
    {
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            initialStatus: SurveyOrderStatus.AwaitingDeposit,
            isSatisfiedDeposit: false,
            hasConfirmedAppointment: true);

        var paymentId = Guid.CreateVersion7();
        var providerReference = $"REF-NEG-{Guid.NewGuid():N}";
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $"""
                INSERT INTO survey.survey_payments
                  (id, survey_order_id, deposit_obligation_id, amount, currency, provider,
                   provider_reference, status, purpose, created_at, updated_at)
                VALUES
                  ('{paymentId}', '{seeded.OrderId}', '{seeded.ObligationId}', 100000, 'VND', 'sepay',
                   '{providerReference}', 'PROCESSING'::system.survey_payment_status, 'OrderDeposit', NOW() - INTERVAL '1 minute', NOW() - INTERVAL '1 minute');
                """);
        }

        var payBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(DatabaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(payBuilder);
        await using var configuredPayDataSource = payBuilder.Build();
        var payOptions = new DbContextOptionsBuilder<SurveyPaymentProcessingDbContext>()
            .UseNpgsql(configuredPayDataSource, npgsql =>
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
        var handler = new ProcessSurveyPaymentEventCommandHandler(
            payDbContext,
            payRepo,
            payOutbox,
            new TestExecutionContext(seeded.OwnerUserId));

        var eventTime = DateTimeOffset.UtcNow.AddSeconds(-15);
        using var payload = JsonDocument.Parse("""{"stage":"test"}""");

        // 1. Wrong currency (USD instead of VND)
        var wrongCurrencyResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            $"evt-curr-{Guid.NewGuid():N}",
            "payment.succeeded",
            50000m,
            "USD",
            eventTime,
            eventTime,
            payload), CancellationToken.None);
        Assert.True(wrongCurrencyResult.IsSuccess);
        Assert.Equal("AMOUNT_OR_CURRENCY_MISMATCH", wrongCurrencyResult.Value.Disposition);

        // 2. Non-existent provider reference
        var wrongRefResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            $"UNKNOWN-{Guid.NewGuid():N}",
            $"evt-ref-{Guid.NewGuid():N}",
            "payment.succeeded",
            50000m,
            "VND",
            eventTime,
            eventTime,
            payload), CancellationToken.None);
        Assert.True(wrongRefResult.IsFailure);
        Assert.Equal(PaymentError.NotFoundCode, wrongRefResult.Error.Code);

        // 3. Out-of-order event: occurrence time older than payment.UpdatedAt
        var staleTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var staleResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            $"evt-stale-{Guid.NewGuid():N}",
            "payment.processing",
            100000m,
            "VND",
            staleTime,
            staleTime,
            payload), CancellationToken.None);
        Assert.True(staleResult.IsSuccess);
        Assert.False(staleResult.Value.TransitionApplied);
        Assert.Equal("IGNORED_OUT_OF_ORDER", staleResult.Value.Disposition);

        // 4. Successful processing, then duplicate event with changed payload returns DUPLICATE without mutating
        var successEventId = $"evt-idemp-{Guid.NewGuid():N}";
        var validTime = DateTimeOffset.UtcNow.AddSeconds(-5);
        var firstResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            successEventId,
            "payment.succeeded",
            50000m,
            "VND",
            validTime,
            validTime,
            payload), CancellationToken.None);
        Assert.True(firstResult.IsSuccess);
        Assert.Equal("PARTIAL_RECEIPT_ALLOCATED", firstResult.Value.Disposition);

        using var changedPayload = JsonDocument.Parse("""{"changed":"fraudulent-attempt"}""");
        var dupResult = await handler.Handle(new ProcessSurveyPaymentEventCommand(
            "sepay",
            providerReference,
            successEventId,
            "payment.succeeded",
            50000m,
            "VND",
            validTime,
            validTime,
            changedPayload), CancellationToken.None);
        Assert.True(dupResult.IsSuccess);
        Assert.True(dupResult.Value.Duplicate);
        Assert.Equal("DUPLICATE", dupResult.Value.Disposition);
    }

    [Fact]
    public async Task Scenario03T04StartCaptureVsFundingReversalDeterministicBarrierRace()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        var paymentId = Guid.CreateVersion7();
        var providerReference = $"REF-RACE-{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"""
                INSERT INTO survey.survey_payments
                  (id, survey_order_id, deposit_obligation_id, amount, currency, provider,
                   provider_reference, status, purpose, confirmed_at, created_at, updated_at)
                VALUES
                  ('{paymentId}', '{seeded.OrderId}', '{seeded.ObligationId}', 100000, 'VND', 'sepay',
                   '{providerReference}', 'PAID'::system.survey_payment_status, 'OrderDeposit',
                   NOW() - INTERVAL '1 minute', NOW() - INTERVAL '1 minute', NOW() - INTERVAL '1 minute');
                """);
        }

        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerPrep = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-flight-reversal-race",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success, prepRes.ErrorMessage);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("{}"), true,
            "Preflight pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var orderVerStart = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var startRequest = new StartCaptureFlightRequest(
            seeded.OrderId,
            orderVerStart,
            prepRes.AttemptId,
            prepRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "start-fp");

        using var barrier = new BarrierCaptureProbe(2, SurveyCaptureCheckpoint.BeforeOrderLock);

        var startTask = Task.Run(async () =>
        {
            await using var startCtx = CreateContext(DatabaseName);
            var startCoord = CreateCoordinator(startCtx, barrier);
            return await startCoord.StartFlightAsync(startRequest);
        });

        var reversalTask = Task.Run(async () =>
        {
            await barrier.HitAsync(SurveyCaptureCheckpoint.BeforeOrderLock, CancellationToken.None);
            var dsBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(DatabaseName));
            PostgreSqlEnumMappings.ConfigureDataSource(dsBuilder);
            await using var ds = dsBuilder.Build();
            var opts = new DbContextOptionsBuilder<SurveyPaymentProcessingDbContext>()
                .UseNpgsql(ds, npgsql =>
                {
                    npgsql.UseNetTopologySuite();
                    PostgreSqlEnumMappings.ConfigureOptions(npgsql);
                })
                .Options;
            await using var payCtx = new SurveyPaymentProcessingDbContext(opts);
            var repo = new SurveyPaymentProcessingRepository(payCtx);
            var clk = TimeProvider.System;
            var h = new ProcessSurveyPaymentEventCommandHandler(
                payCtx,
                repo,
                new SurveyPaymentProcessingOutbox(payCtx, new OutboxMessageFactory(new SystemTextJsonIntegrationMessageSerializer(), clk)),
                new TestExecutionContext(seeded.AdminUserId));

            using var p = JsonDocument.Parse("""{"reason":"provider-chargeback-race"}""");
            var occ = DateTimeOffset.UtcNow;
            return await h.Handle(new ProcessSurveyPaymentEventCommand(
                "sepay",
                providerReference,
                $"chargeback-race-{Guid.NewGuid():N}",
                "payment.chargeback",
                100000m,
                "VND",
                occ,
                occ,
                p), CancellationToken.None);
        });

        await Task.WhenAll(startTask, reversalTask);
        var startRes = await startTask;
        var reversalRes = await reversalTask;

        await using var verifyCtx = CreateContext(DatabaseName);
        var finalOrder = await verifyCtx.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        var finalAttempt = await verifyCtx.Set<CaptureAttempt>().AsNoTracking().SingleAsync(a => a.Id == prepRes.AttemptId);

        if (startRes.Success)
        {
            Assert.Equal(SurveyOrderStatus.Capturing, finalOrder.Status);
            Assert.Equal(CaptureAttemptStatus.InFlight, finalAttempt.Status);
            if (reversalRes.IsSuccess)
            {
                Assert.Equal("FUNDING_REVERSAL_RECORDED_AFTER_CAPTURE_START", reversalRes.Value.Disposition);
            }
            else
            {
                Assert.Equal(PaymentError.PersistenceConflictCode, reversalRes.Error.Code);
            }
        }
        else
        {
            Assert.True(reversalRes.IsSuccess, reversalRes.Error.Description);
            Assert.Equal(SurveyOrderStatus.AwaitingDeposit, finalOrder.Status);
            Assert.Equal("CHARGEBACK_REVERSED_CAPTURE_READINESS", reversalRes.Value.Disposition);
            Assert.Contains(startRes.ErrorCode, StartConflictCodes);
        }
    }
}
