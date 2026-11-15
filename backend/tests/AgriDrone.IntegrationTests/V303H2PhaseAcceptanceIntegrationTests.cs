using System.Data;
using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Application.Features.Media.ResetMissionUpload;
using AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Domain.Missions;
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

public sealed class V303H2PhaseAcceptanceIntegrationTests : IAsyncLifetime
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";

    private static readonly string[] StartConflictCodes = ["CAPTURE_IN_FLIGHT", "CANCELLATION_VERSION_CONFLICT", "CAPTURE_START_CONFLICT", "CAPTURE_VERSION_CONFLICT"];
    private static readonly string[] CancelConflictCodes = ["CAPTURE_VERSION_CONFLICT", "CAPTURE_GATE_CLOSED", "CAPTURE_IN_FLIGHT", "CANCELLATION_CONFLICT"];

    private readonly string _databaseName = $"agridrone_v303h2_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await RecreateDatabaseAsync(_databaseName);
        await using var context = CreateContext(_databaseName);
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DropDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Scenario03T04StartVsCancelRaceSerializesSafelyAndNeverCorruptsOrder()
    {
        var seeded = await SeedV3OrderGraphAsync(_databaseName);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        // Preflight & Prepare flight
        var orderVerInitial = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerInitial,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-flight",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success, prepRes.ErrorMessage);

        // Record preflight on mission
        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(),
            "1.0",
            JsonDocument.Parse("""{"battery": "ok"}"""),
            true,
            "Battery: 98%, Propellers: OK, GPS: Locked",
            seeded.ManagerUserId,
            DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var orderVerForRace = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var startReq = new StartCaptureFlightRequest(
            seeded.OrderId,
            orderVerForRace,
            prepRes.AttemptId,
            prepRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "start-fp");

        var cancelReq = new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVerForRace,
            seeded.OwnerUserId,
            seeded.OwnerUserId,
            CaptureCancellationDecisionKind.CustomerBeforeCapture,
            0m,
            null,
            "CANCELLATION_REQUESTED",
            "Owner cancelled due to weather uncertainty",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cancel-fp");

        // Fire both operations concurrently
        var startTask = Task.Run(async () =>
        {
            await using var ctx = CreateContext(_databaseName);
            var coord = CreateCoordinator(ctx);
            return await coord.StartFlightAsync(startReq);
        });

        var cancelTask = Task.Run(async () =>
        {
            await using var ctx = CreateContext(_databaseName);
            var coord = CreateCoordinator(ctx);
            return await coord.CancelOrderAsync(cancelReq);
        });

        await Task.WhenAll(startTask, cancelTask);
        var startResult = await startTask;
        var cancelResult = await cancelTask;

        // In serializable isolation with row-level locks, exactly one can succeed
        Assert.False(startResult.Success && cancelResult.Success, "Both Start and Cancel succeeded simultaneously!");

        await using var verifyContext = CreateContext(_databaseName);
        var finalOrder = await verifyContext.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);

        if (startResult.Success)
        {
            Assert.False(cancelResult.Success);
            Assert.Contains(cancelResult.ErrorCode, CancelConflictCodes);
            Assert.Equal(SurveyOrderStatus.Capturing, finalOrder.Status);
        }
        else
        {
            Assert.True(cancelResult.Success, $"Cancel failed: {cancelResult.ErrorCode}: {cancelResult.ErrorMessage}; Start result: {startResult.ErrorCode}: {startResult.ErrorMessage}");
            Assert.Contains(startResult.ErrorCode, StartConflictCodes);
            Assert.Equal(SurveyOrderStatus.Cancelled, finalOrder.Status);
        }
    }

    [Fact]
    public async Task Scenario03T06ConcurrentReflightCreatesExactlyOneActiveSuccessor()
    {
        var seeded = await SeedV3OrderGraphAsync(_databaseName);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        // Advance to flight completed & recorded failure
        var orderVerPrep = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-flight",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success, prepRes.ErrorMessage);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(),
            "1.0",
            JsonDocument.Parse("""{"battery": "ok"}"""),
            true,
            "Preflight pass",
            seeded.ManagerUserId,
            DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var orderVerStart = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var startRes = await coordinator.StartFlightAsync(new StartCaptureFlightRequest(
            seeded.OrderId,
            orderVerStart,
            prepRes.AttemptId,
            prepRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "start-fp"));
        Assert.True(startRes.Success, startRes.ErrorMessage);

        var orderVerFail = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var failFlightRes = await coordinator.RecordFlightFailureAsync(new RecordFlightFailureRequest(
            seeded.OrderId,
            orderVerFail,
            prepRes.AttemptId,
            startRes.Version,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Heavy cloud and sudden turbulence",
            "Flight incident log #123",
            Guid.NewGuid(),
            "fail-fp"));
        Assert.True(failFlightRes.Success, failFlightRes.ErrorMessage);

        // Verify order is AwaitingReflightDecision
        var rejectedOrder = await context.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.AwaitingReflightDecision, rejectedOrder.Status);

        // Fire 2 concurrent reflight requests with same expected attempt version
        var reflightDroneId = Guid.NewGuid();
        var reflightDroneCode = $"DRN-{reflightDroneId.ToString("N")[..15]}";
        var reflightRegNum = $"VN-{reflightDroneId.ToString("N")[..8].ToUpperInvariant()}";
        await using (var conn = new NpgsqlConnection(BuildConnectionString(_databaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $$"""
                INSERT INTO mission.drones (id, code, name, status, specifications, registration_number, registration_date, registration_expiry_date)
                VALUES ('{{reflightDroneId}}', '{{reflightDroneCode}}', 'Reflight Drone', 'AVAILABLE'::system.drone_status, '{"capabilities":["BASELINE_MAPPING","PLANT_HEALTH"]}'::jsonb, '{{reflightRegNum}}', '2020-01-01', '2030-01-01');
                """);
        }

        var futureStart = DateTimeOffset.UtcNow.AddHours(2);
        var futureEnd = futureStart.AddHours(2);
        var orderVerForReflight = await GetOrderVersionAsync(_databaseName, seeded.OrderId);

        var reflightTask1 = Task.Run(async () =>
        {
            await using var ctx = CreateContext(_databaseName);
            var coord = CreateCoordinator(ctx);
            return await coord.CreateQualityReflightAsync(new CreateQualityReflightRequest(
                seeded.OrderId,
                orderVerForReflight,
                prepRes.AttemptId,
                failFlightRes.Version,
                reflightDroneId,
                futureStart,
                futureEnd,
                seeded.ManagerUserId,
                Guid.NewGuid(),
                "Reflight 1 attempt",
                "Quality blur evidence #1",
                Guid.NewGuid(),
                "reflight-fp-1"));
        });

        var reflightTask2 = Task.Run(async () =>
        {
            await using var ctx = CreateContext(_databaseName);
            var coord = CreateCoordinator(ctx);
            return await coord.CreateQualityReflightAsync(new CreateQualityReflightRequest(
                seeded.OrderId,
                orderVerForReflight,
                prepRes.AttemptId,
                failFlightRes.Version,
                reflightDroneId,
                futureStart,
                futureEnd,
                seeded.ManagerUserId,
                Guid.NewGuid(),
                "Reflight 2 attempt",
                "Quality blur evidence #2",
                Guid.NewGuid(),
                "reflight-fp-2"));
        });

        await Task.WhenAll(reflightTask1, reflightTask2);
        var r1 = await reflightTask1;
        var r2 = await reflightTask2;

        // Exactly one succeeds
        Assert.True(r1.Success ^ r2.Success, $"Expected exactly 1 reflight success. R1: {r1.Success} ({r1.ErrorCode}: {r1.ErrorMessage}), R2: {r2.Success} ({r2.ErrorCode}: {r2.ErrorMessage})");

        await using var verifyContext = CreateContext(_databaseName);
        var attempts = await verifyContext.Set<CaptureAttempt>()
            .Where(a => a.OrderId == seeded.OrderId)
            .ToListAsync();

        Assert.Equal(2, attempts.Count);
        var activeAttempts = attempts.Where(a => a.Status != CaptureAttemptStatus.FlightFailed).ToList();
        Assert.Single(activeAttempts);
        Assert.Equal(CaptureAttemptStatus.Planned, activeAttempts[0].Status);

        var finalOrder = await verifyContext.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.AwaitingAppointment, finalOrder.Status);
    }

    [Fact]
    public async Task Scenario03T05FaultInjectionRollbackClearsAllArtifactsAndAllowsCleanRetry()
    {
        var seeded = await SeedV3OrderGraphAsync(_databaseName);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        // 1. Fault injection: Attempt PrepareAttempt with empty actor ID
        var orderVerInitial = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var invalidReq = new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerInitial,
            Guid.Empty,
            seeded.DroneId,
            "fault-injection",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invalid-fp");

        var failRes = await coordinator.PrepareAttemptAsync(invalidReq);
        Assert.False(failRes.Success);

        // 2. Verify complete rollback: zero attempts, zero missions created
        await using var checkContext = CreateContext(_databaseName);
        var attempts = await checkContext.Set<CaptureAttempt>()
            .Where(a => a.OrderId == seeded.OrderId).ToListAsync();
        Assert.Empty(attempts);

        var missions = await checkContext.Set<DroneMission>()
            .Where(m => m.SurveyOrderId == seeded.OrderId).ToListAsync();
        Assert.Empty(missions);

        // 3. Clean retry with valid request succeeds
        var orderVerRetry = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var validReq = new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerRetry,
            seeded.ManagerUserId,
            seeded.DroneId,
            "valid-prep",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "valid-fp");

        var retryRes = await coordinator.PrepareAttemptAsync(validReq);
        Assert.True(retryRes.Success, retryRes.ErrorMessage);

        var postRetryAttempts = await checkContext.Set<CaptureAttempt>()
            .Where(a => a.OrderId == seeded.OrderId).ToListAsync();
        Assert.Single(postRetryAttempts);
        Assert.Equal(CaptureAttemptStatus.Planned, postRetryAttempts[0].Status);
    }

    [Fact]
    public async Task Scenario03T02LatePaymentAfterCancellationCreatesRefundObligationWithoutResurrectingOrder()
    {
        var seeded = await SeedV3OrderGraphAsync(_databaseName, initialStatus: SurveyOrderStatus.AwaitingDeposit, isSatisfiedDeposit: false);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        // Pre-create an initiated survey payment for the deposit obligation so the provider callback matches it
        var paymentId = Guid.CreateVersion7();
        var providerReference = $"REF-{Guid.NewGuid():N}";
        await using (var npgConn = new NpgsqlConnection(BuildConnectionString(_databaseName)))
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
        var orderVerCancel = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
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
        var payBuilder = new NpgsqlDataSourceBuilder(BuildConnectionString(_databaseName));
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
        await using var verifyContext = CreateContext(_databaseName);
        var postPaymentOrder = await verifyContext.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.Cancelled, postPaymentOrder.Status);

        var refundObligations = await verifyContext.Set<SurveyRefundObligation>()
            .Where(l => l.SurveyOrderId == seeded.OrderId)
            .ToListAsync();
        Assert.NotEmpty(refundObligations);
        Assert.Contains(refundObligations, l => l.Amount >= 100000m);
    }

    [Fact]
    public async Task Scenario03T10T11CancellationDecisionsEnforceActorPermissionsAndCalculateLiabilities()
    {
        // 1. Owner cancels before flight -> Full refund
        var seeded = await SeedV3OrderGraphAsync(_databaseName, isSatisfiedDeposit: true, hasConfirmedAppointment: true);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        var ownerCancelVer = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var ownerCancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            ownerCancelVer,
            seeded.OwnerUserId,
            seeded.OwnerUserId,
            CaptureCancellationDecisionKind.CustomerBeforeCapture,
            0m,
            null,
            "OWNER_WITHDRAWAL",
            "Owner changed mind before flight",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "owner-cancel-fp"));
        Assert.True(ownerCancelRes.Success, ownerCancelRes.ErrorMessage);

        var ownerDecision = await context.Set<SurveyCancellationDecision>()
            .SingleAsync(d => d.SurveyOrderId == seeded.OrderId);
        Assert.Equal(100000m, ownerDecision.RefundAmount);
        Assert.Equal(0m, ownerDecision.RetainedAmount);

        // 2. Post-capture cancellation: non-admin cannot make admin decision; admin can retain deposit
        var seededPostCapture = await SeedV3OrderGraphAsync(_databaseName, isSatisfiedDeposit: true, hasConfirmedAppointment: true);
        await using var ctxPost = CreateContext(_databaseName);
        var coordPost = CreateCoordinator(ctxPost);

        // Advance to flight complete
        var postVerPrep = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var prepRes = await coordPost.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seededPostCapture.OrderId, postVerPrep, seededPostCapture.ManagerUserId, seededPostCapture.DroneId,
            "prep-fp", Guid.NewGuid(), Guid.NewGuid(), "prep-fp"));
        Assert.True(prepRes.Success);

        var mission = await ctxPost.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("""{"test": 1}"""), true,
            "Preflight pass", seededPostCapture.ManagerUserId, DateTimeOffset.UtcNow);
        await ctxPost.SaveChangesAsync();

        var postVerStart = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var startRes = await coordPost.StartFlightAsync(new StartCaptureFlightRequest(
            seededPostCapture.OrderId, postVerStart, prepRes.AttemptId, prepRes.Version, prepRes.MissionId,
            seededPostCapture.ManagerUserId, Guid.NewGuid(), Guid.NewGuid(), "start-fp"));
        Assert.True(startRes.Success);

        var postVerEnd = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var endRes = await coordPost.EndFlightAsync(new EndCaptureFlightRequest(
            seededPostCapture.OrderId, postVerEnd, prepRes.AttemptId, startRes.Version,
            seededPostCapture.ManagerUserId, Guid.NewGuid(), DateTimeOffset.UtcNow,
            "receipt-ok", Guid.NewGuid(), "end-fp"));
        Assert.True(endRes.Success);

        // Add pre-existing uploaded manifest to attempt & freeze
        var manifestId = Guid.NewGuid();
        var manifestFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var manifest = CaptureInputManifest.Create(
            manifestId,
            seededPostCapture.TenantId,
            seededPostCapture.FarmId,
            seededPostCapture.OrderId,
            prepRes.AttemptId,
            prepRes.MissionId,
            manifestFingerprint,
            100,
            1024L * 1024L * 10L,
            DateTimeOffset.UtcNow);
        ctxPost.Set<CaptureInputManifest>().Add(manifest);
        await ctxPost.SaveChangesAsync();

        var postVerFreeze = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var freezeRes = await coordPost.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seededPostCapture.OrderId,
            postVerFreeze,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seededPostCapture.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "freeze-fp"));
        Assert.True(freezeRes.Success);

        var processingJobId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(_databaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $$"""
                INSERT INTO mission.ai_processing_jobs (id, mission_id, job_type, status, parameters, attempt_number, input_manifest, output_manifest, queued_at)
                VALUES ('{{processingJobId}}', '{{prepRes.MissionId}}', 'FRAME_EXTRACTION'::system.ai_job_type, 'QUEUED'::system.ai_job_status, '{}'::jsonb, 1, '{}'::jsonb, '{}'::jsonb, NOW());
                """);
        }

        var postVerApprove = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var approveRes = await coordPost.ApproveInputAsync(new ApproveCaptureInputRequest(
            seededPostCapture.OrderId,
            postVerApprove,
            prepRes.AttemptId,
            freezeRes.Version,
            freezeRes.ManifestId,
            freezeRes.ManifestFingerprint,
            processingJobId,
            seededPostCapture.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "approve-fp",
            "Quality pass"));
        Assert.True(approveRes.Success);

        // Manager cannot make CustomerAfterAcceptableCapture decision (admin only)
        var postVerMgr = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var managerCancelRes = await coordPost.CancelOrderAsync(new CancelCaptureOrderRequest(
            seededPostCapture.OrderId,
            postVerMgr,
            seededPostCapture.ManagerUserId,
            seededPostCapture.ManagerUserId,
            CaptureCancellationDecisionKind.CustomerAfterAcceptableCapture,
            100000m,
            approveRes.ApprovalId,
            "MGR_ABORT",
            "Manager attempted admin cancellation decision",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "mgr-cancel-fp"));
        Assert.False(managerCancelRes.Success);
        Assert.Equal("ADMIN_DECISION_REQUIRED", managerCancelRes.ErrorCode);

        // Admin cancels post-capture retaining deposit
        var postVerAdmin = await GetOrderVersionAsync(_databaseName, seededPostCapture.OrderId);
        var adminCancelRes = await coordPost.CancelOrderAsync(new CancelCaptureOrderRequest(
            seededPostCapture.OrderId,
            postVerAdmin,
            seededPostCapture.AdminUserId,
            seededPostCapture.AdminUserId,
            CaptureCancellationDecisionKind.CustomerAfterAcceptableCapture,
            100000m,
            approveRes.ApprovalId,
            "ADMIN_ABORT",
            "Admin cancelled post-capture with policy deposit retention",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "admin-cancel-fp"));
        Assert.True(adminCancelRes.Success, adminCancelRes.ErrorMessage);

        var postCaptureDecision = await ctxPost.Set<SurveyCancellationDecision>()
            .SingleAsync(d => d.SurveyOrderId == seededPostCapture.OrderId);
        Assert.Equal(0m, postCaptureDecision.RefundAmount);
        Assert.Equal(100000m, postCaptureDecision.RetainedAmount);
    }

    [Fact]
    public async Task Scenario03T12OldRouteBypassRejectsV3CaptureMissions()
    {
        var seeded = await SeedV3OrderGraphAsync(_databaseName);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerPrep = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-fp",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success);

        var missionId = prepRes.MissionId;
        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == missionId);
        Assert.NotNull(mission.CaptureAttemptId);

        // 1. Direct domain method RecoverFailedFlight throws InvalidOperationException on V3 capture missions
        var domainEx = Assert.Throws<InvalidOperationException>(() => mission.RecoverFailedFlight(
            seeded.DroneId,
            DateTimeOffset.UtcNow.AddHours(1),
            DateTimeOffset.UtcNow.AddHours(2),
            DateTimeOffset.UtcNow));
        Assert.Contains("V3 capture Missions cannot be recovered in place", domainEx.Message);

        // 2. TransitionMissionCommandHandler must reject
        var missionRepo = new TestMissionRepository(mission);
        var accessService = new TestManagerAccessService(seeded.TenantId, seeded.FarmId);
        var execContext = new TestExecutionContext(seeded.ManagerUserId);

        var transitionHandler = new TransitionMissionCommandHandler(
            missionRepo,
            null!,
            null!,
            null!,
            null!,
            accessService,
            null!,
            null!,
            null!,
            null!,
            execContext,
            TimeProvider.System);

        var transResult = await transitionHandler.Handle(
            new TransitionMissionCommand(seeded.FarmId, missionId, MissionStatus.InFlight, mission.Version, "Test transition"),
            CancellationToken.None);

        Assert.True(transResult.IsFailure);
        Assert.Equal("MissionOperation.V3CoordinatorRequired", transResult.Error.Code);

        // 3. RescheduleOrderMissionCommandHandler must reject
        var rescheduleHandler = new RescheduleOrderMissionCommandHandler(
            missionRepo,
            null!,
            null!,
            null!,
            null!,
            accessService,
            null!,
            null!,
            execContext,
            TimeProvider.System);

        var schedResult = await rescheduleHandler.Handle(
            new RescheduleOrderMissionCommand(
                seeded.FarmId,
                missionId,
                mission.Version,
                DateTimeOffset.UtcNow.AddHours(5),
                DateTimeOffset.UtcNow.AddHours(7),
                seeded.DroneId),
            CancellationToken.None);

        Assert.True(schedResult.IsFailure);
        Assert.Equal("MissionOperation.V3CoordinatorRequired", schedResult.Error.Code);

        // 4. ResetMissionUploadCommandHandler must reject
        var resetHandler = new ResetMissionUploadCommandHandler(
            missionRepo,
            null!,
            null!,
            null!,
            null!,
            execContext,
            accessService,
            TimeProvider.System,
            null!);

        var resetResult = await resetHandler.Handle(
            new ResetMissionUploadCommand(seeded.TenantId, seeded.FarmId, missionId, mission.Version, false),
            CancellationToken.None);

        Assert.True(resetResult.IsFailure);
        Assert.Equal("MissionUpload.V3ImmutableEvidence", resetResult.Error.Code);
    }

    [Fact]
    public async Task Scenario03T09ManifestFreezeAndInputApprovalWorksWithoutWaitingForTerminalAccepted()
    {
        var seeded = await SeedV3OrderGraphAsync(_databaseName);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerPrep = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-fp",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("""{"chk": "ok"}"""), true,
            "Checklist pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var orderVerStart = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var startRes = await coordinator.StartFlightAsync(new StartCaptureFlightRequest(
            seeded.OrderId,
            orderVerStart,
            prepRes.AttemptId,
            prepRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "start-fp"));
        Assert.True(startRes.Success, $"{startRes.ErrorCode}: {startRes.ErrorMessage}");

        var now = DateTimeOffset.UtcNow;
        var orderVerEnd = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var endRes = await coordinator.EndFlightAsync(new EndCaptureFlightRequest(
            seeded.OrderId,
            orderVerEnd,
            prepRes.AttemptId,
            startRes.Version,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            now,
            "receipt-ok",
            Guid.NewGuid(),
            "end-fp"));
        Assert.True(endRes.Success);

        // Add pre-existing uploaded manifest to attempt
        var manifestId = Guid.NewGuid();
        var manifestFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var manifest = CaptureInputManifest.Create(
            manifestId,
            seeded.TenantId,
            seeded.FarmId,
            seeded.OrderId,
            prepRes.AttemptId,
            prepRes.MissionId,
            manifestFingerprint,
            150,
            1024L * 1024L * 50L,
            now);
        context.Set<CaptureInputManifest>().Add(manifest);
        await context.SaveChangesAsync();

        // Freeze input manifest
        var orderVerFreeze = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var freezeRes = await coordinator.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seeded.OrderId,
            orderVerFreeze,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "freeze-fp"));
        Assert.True(freezeRes.Success, freezeRes.ErrorMessage);

        // Verify Order is now at AwaitingInputReview
        var awaitingReviewOrder = await context.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.AwaitingInputReview, awaitingReviewOrder.Status);

        // Approve input manifest without waiting for terminal Accepted
        var processingJobId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(_databaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $$"""
                INSERT INTO mission.ai_processing_jobs (id, mission_id, job_type, status, parameters, attempt_number, input_manifest, output_manifest, queued_at)
                VALUES ('{{processingJobId}}', '{{prepRes.MissionId}}', 'FRAME_EXTRACTION'::system.ai_job_type, 'QUEUED'::system.ai_job_status, '{}'::jsonb, 1, '{}'::jsonb, '{}'::jsonb, NOW());
                """);
        }

        var orderVerApprove = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var approveRes = await coordinator.ApproveInputAsync(new ApproveCaptureInputRequest(
            seeded.OrderId,
            orderVerApprove,
            prepRes.AttemptId,
            freezeRes.Version,
            freezeRes.ManifestId,
            freezeRes.ManifestFingerprint,
            processingJobId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "approve-fp",
            "Quality report pass"));

        Assert.True(approveRes.Success, approveRes.ErrorMessage);

        // Verify Order transitioned to Processing (not terminal Accepted)
        var finalOrder = await context.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.Processing, finalOrder.Status);

        // Verify CaptureInputApproval record exists
        var approvalRecord = await context.Set<CaptureInputApproval>()
            .SingleOrDefaultAsync(a => a.CaptureAttemptId == prepRes.AttemptId);
        Assert.NotNull(approvalRecord);
        Assert.Equal(manifestId, approvalRecord.InputManifestId);
    }

    [Fact]
    public async Task Scenario03T08RevokedManagerDeniedAccessToCoordinator()
    {
        // Seed graph with manager assignment revoked (ended_at = NOW())
        var seeded = await SeedV3OrderGraphAsync(_databaseName, isAssignedManager: false);
        await using var context = CreateContext(_databaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerPrep = await GetOrderVersionAsync(_databaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-fp",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));

        Assert.False(prepRes.Success);
        Assert.Equal("MANAGER_FORBIDDEN", prepRes.ErrorCode);
    }

    private static AgriDroneSchemaDbContext CreateContext(string databaseName)
    {
        var builder = new NpgsqlDataSourceBuilder(BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, builder.Build());
        return new AgriDroneSchemaDbContext(options.Options);
    }

    private static SurveyCaptureExecutionCoordinator CreateCoordinator(AgriDroneSchemaDbContext context)
    {
        var clock = TimeProvider.System;
        return new SurveyCaptureExecutionCoordinator(
            context,
            clock,
            new OutboxMessageFactory(
                new SystemTextJsonIntegrationMessageSerializer(),
                clock));
    }

    private static async Task<uint> GetOrderVersionAsync(string databaseName, Guid orderId)
    {
        await using var ctx = CreateContext(databaseName);
        return await ctx.Set<SurveyOrder>()
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => o.Version)
            .SingleAsync();
    }

    private static string BuildConnectionString(string databaseName) =>
        $"Host=127.0.0.1;Port=55432;Database={databaseName};Username=agridrone_test;Password=agridrone_test";

    private sealed record V3OrderTestGraph(
        Guid TenantId,
        Guid FarmId,
        Guid DroneId,
        Guid OrderId,
        Guid RequestId,
        Guid ManagerUserId,
        Guid OwnerUserId,
        Guid AdminUserId,
        Guid ObligationId,
        Guid PolicyVersionId);

    private static async Task<V3OrderTestGraph> SeedV3OrderGraphAsync(
        string databaseName,
        SurveyOrderStatus initialStatus = SurveyOrderStatus.ReadyForCapture,
        bool isAssignedManager = true,
        bool isSatisfiedDeposit = true,
        bool hasConfirmedAppointment = true)
    {
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var obligationId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var depositPolicyId = Guid.NewGuid();
        var claimPolicyId = Guid.NewGuid();
        var cancellationPolicyId = Guid.NewGuid();
        var policyVersionId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var priceVersionId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        const string policyFingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        await using var connection = new NpgsqlConnection(BuildConnectionString(databaseName));
        await connection.OpenAsync();

        // 1. Get seeded COMBO service id
        using var getService = connection.CreateCommand();
        getService.CommandText = "SELECT id FROM survey.survey_services WHERE code = 'COMBO' LIMIT 1;";
        var serviceId = (Guid)(await getService.ExecuteScalarAsync())!;

        var hasExistingPolicies = false;
        using var getExistingPolicy = connection.CreateCommand();
        getExistingPolicy.CommandText = "SELECT id, survey_deposit_policy_id, claim_policy_id, cancellation_policy_id FROM survey.business_policy_versions WHERE stream_key = 'DEFAULT' AND version_number = 1 LIMIT 1;";
        await using (var reader = await getExistingPolicy.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                hasExistingPolicies = true;
                policyVersionId = reader.GetGuid(0);
                depositPolicyId = reader.GetGuid(1);
                claimPolicyId = reader.GetGuid(2);
                cancellationPolicyId = reader.GetGuid(3);
            }
        }

        if (hasExistingPolicies)
        {
            using var getRev = connection.CreateCommand();
            getRev.CommandText = $"SELECT id FROM survey.survey_service_revisions WHERE survey_service_id = '{serviceId}' AND version_number = 1 LIMIT 1;";
            revisionId = (Guid)(await getRev.ExecuteScalarAsync())!;

            using var getPrice = connection.CreateCommand();
            getPrice.CommandText = $"SELECT id FROM survey.survey_service_prices WHERE survey_service_id = '{serviceId}' AND version_number = 1 LIMIT 1;";
            priceVersionId = (Guid)(await getPrice.ExecuteScalarAsync())!;
        }

        var now = DateTimeOffset.UtcNow;
        var appStart = now.AddHours(-1);
        var appEnd = now.AddHours(3);

        var assignedEndedAtSql = isAssignedManager ? "NULL" : "NOW()";
        var depositPaymentStatusSql = isSatisfiedDeposit ? "PAID" : "PENDING";
        var depositObligationStatusSql = isSatisfiedDeposit ? "SATISFIED" : "PENDING";
        var depositAllocatedAmountSql = isSatisfiedDeposit ? "100000" : "0";
        var depositSatisfiedAtSql = isSatisfiedDeposit ? "NOW()" : "NULL";
        var appointmentStatusSql = hasConfirmedAppointment ? "CONFIRMED" : "PROPOSED";
        var confirmedByOwnerSql = hasConfirmedAppointment ? $"'{ownerUserId}'" : "NULL";
        var confirmedAtSql = hasConfirmedAppointment ? "NOW()" : "NULL";
        var tenantCode = $"TEN-{tenantId.ToString("N")[..15]}";
        var farmCode = $"FARM-{farmId.ToString("N")[..15]}";
        var droneCode = $"DRN-{droneId.ToString("N")[..15]}";
        var registrationNumber = $"VN-{droneId.ToString("N")[..8].ToUpperInvariant()}";
        var requestNumber = $"REQ-{requestId.ToString("N")[..15]}";
        var idempotencyKey = $"IDEM-{requestId.ToString("N")[..15]}";
        var orderNumber = $"ORD-{orderId.ToString("N")[..15]}";
        var orderStatusSql = ToDbEnumString(initialStatus);

        await ExecuteAsync(connection, $$"""
            -- Users
            INSERT INTO identity.users (id, email, password_hash, full_name, status)
            VALUES 
              ('{{managerUserId}}', 'mgr-{{managerUserId:N}}@test.local', 'hash', 'Manager User', 'ACTIVE'::system.user_status),
              ('{{ownerUserId}}', 'owner-{{ownerUserId:N}}@test.local', 'hash', 'Owner User', 'ACTIVE'::system.user_status),
              ('{{adminUserId}}', 'admin-{{adminUserId:N}}@test.local', 'hash', 'Admin User', 'ACTIVE'::system.user_status);

            -- Role & UserRole for admin
            INSERT INTO identity.roles (id, code, name)
            VALUES ('{{adminRoleId}}', 'SYSTEM_ADMIN', 'System Administrator')
            ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name;

            INSERT INTO identity.user_roles (user_id, role_id)
            SELECT '{{adminUserId}}', id FROM identity.roles WHERE code = 'SYSTEM_ADMIN'
            ON CONFLICT DO NOTHING;

            -- Tenant & Owner Membership
            INSERT INTO identity.tenants (id, code, name)
            VALUES ('{{tenantId}}', '{{tenantCode}}', 'Test Tenant');

            INSERT INTO identity.tenant_memberships (id, tenant_id, user_id, role, status)
            VALUES ('{{membershipId}}', '{{tenantId}}', '{{ownerUserId}}', 'OWNER'::system.tenant_member_role, 'ACTIVE'::system.general_status);

            -- Farm
            INSERT INTO farm.farms (id, tenant_id, code, name, created_by)
            VALUES ('{{farmId}}', '{{tenantId}}', '{{farmCode}}', 'Test Farm', '{{ownerUserId}}');

            -- System Manager Profile & Farm Manager Assignment
            INSERT INTO identity.system_manager_profiles (id, user_id, availability, status, created_at, updated_at)
            VALUES ('{{profileId}}', '{{managerUserId}}', 'AVAILABLE'::system.system_manager_availability_status, 'ACTIVE'::system.system_manager_profile_status, NOW(), NOW());

            -- Farm Manager Assignment
            INSERT INTO identity.farm_manager_assignments (id, tenant_id, farm_id, system_manager_profile_id, assigned_by, assignment_reason, assigned_at, ended_at)
            VALUES ('{{assignmentId}}', '{{tenantId}}', '{{farmId}}', '{{profileId}}', '{{adminUserId}}', 'Assignment', NOW(), {{assignedEndedAtSql}});

            -- Drone
            INSERT INTO mission.drones (id, code, name, status, specifications, registration_number, registration_date, registration_expiry_date)
            VALUES ('{{droneId}}', '{{droneCode}}', 'Test Drone', 'AVAILABLE'::system.drone_status, '{"capabilities":["BASELINE_MAPPING","PLANT_HEALTH"]}'::jsonb, '{{registrationNumber}}', '2020-01-01', '2030-01-01');
            """);

        if (!hasExistingPolicies)
        {
            await ExecuteAsync(connection, $$"""
                -- Policies
                INSERT INTO survey.v3_survey_deposit_policies (id, version_number, amount, currency, effective_from, created_by, created_at, reason_code)
                VALUES ('{{depositPolicyId}}', 1, 100000, 'VND', NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Claim Policies
                INSERT INTO survey.claim_policies (id, version_number, calendar_window_days, time_zone_id, exclude_notice_date, effective_from, created_by, created_at, reason_code)
                VALUES ('{{claimPolicyId}}', 1, 7, 'Asia/Ho_Chi_Minh', TRUE, NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Cancellation Policies
                INSERT INTO survey.cancellation_policies (id, version_number, refundable_before_capture, retain_deposit_after_capture, allowed_free_no_show_reschedules, operational_postponement_refundable, service_failure_refundable, effective_from, created_by, created_at, reason_code)
                VALUES ('{{cancellationPolicyId}}', 1, TRUE, TRUE, 1, TRUE, TRUE, NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Service Revision & Price
                INSERT INTO survey.survey_service_revisions (id, survey_service_id, version_number, name, description, maps_poles, screens_disease, effective_from, created_by, created_at, reason_code)
                VALUES ('{{revisionId}}', '{{serviceId}}', 1, 'Combo Survey Service', 'Combo mapping and disease survey', TRUE, TRUE, NOW(), '{{adminUserId}}', NOW(), 'INIT');

                INSERT INTO survey.survey_service_prices (id, survey_service_id, version_number, price_per_pole, currency, effective_from, created_by, created_at, reason_code)
                VALUES ('{{priceVersionId}}', '{{serviceId}}', 1, 5000, 'VND', NOW(), '{{adminUserId}}', NOW(), 'INIT');

                -- Business Policy Versions
                INSERT INTO survey.business_policy_versions (id, stream_key, version_number, survey_deposit_policy_id, claim_policy_id, cancellation_policy_id, service_revision_ids, pricing_currency, policy_fingerprint, effective_from, created_by, created_at, reason_code)
                VALUES ('{{policyVersionId}}', 'DEFAULT', 1, '{{depositPolicyId}}', '{{claimPolicyId}}', '{{cancellationPolicyId}}', ARRAY['{{revisionId}}'::uuid], 'VND', '{{policyFingerprint}}', NOW(), '{{adminUserId}}', NOW(), 'INIT');
                """);
        }

        await ExecuteAsync(connection, $$"""
            -- Survey Request
            INSERT INTO survey.survey_requests
              (id, request_number, kind, tenant_id, farm_id, requested_by_user_id,
               survey_service_id, caller_scope, idempotency_key, applicant_name,
               applicant_email, applicant_phone, farm_name, farm_address,
               approximate_area_ha, map_location, status)
            VALUES
              ('{{requestId}}', '{{requestNumber}}', 'EXISTING_FARM_SURVEY', '{{tenantId}}', '{{farmId}}', '{{ownerUserId}}',
               '{{serviceId}}', 'tenant:{{tenantId}}', '{{idempotencyKey}}', 'Applicant',
               'owner@test.local', '0900000000', 'Test Farm', 'Address',
               1.0000, ST_SetSRID(ST_MakePoint(106.5, 10.5), 4326), 'APPROVED');

            -- Survey Order
            INSERT INTO survey.survey_orders
              (id, order_number, tenant_id, farm_id, survey_request_id,
               survey_service_id, scope, requires_baseline_mapping, status,
               deposit_amount, deposit_payment_status, deposit_obligation_id,
               business_policy_version_id, created_at, updated_at)
            VALUES
              ('{{orderId}}', '{{orderNumber}}', '{{tenantId}}', '{{farmId}}', '{{requestId}}',
               '{{serviceId}}', 'V3'::system.contract_scope, FALSE, '{{orderStatusSql}}'::system.survey_order_status,
               100000, '{{depositPaymentStatusSql}}'::system.survey_payment_status, '{{obligationId}}',
               '{{policyVersionId}}', NOW(), NOW());

            -- Commercial Snapshot
            INSERT INTO survey.order_commercial_snapshots
              (survey_order_id, business_policy_version_id, policy_fingerprint,
               survey_service_revision_id, survey_service_revision_version, service_code,
               maps_poles, screens_disease, price_version_id, price_version_number,
               price_per_pole, currency, deposit_policy_id, deposit_version_number,
               deposit_amount, claim_policy_id, claim_policy_version_number,
               claim_window_days, claim_time_zone_id, exclude_notice_date,
               cancellation_policy_id, cancellation_policy_version_number,
               refundable_before_capture, retain_deposit_after_capture,
               allowed_free_no_show_reschedules, evaluated_at, created_at)
            VALUES
              ('{{orderId}}', '{{policyVersionId}}', '{{policyFingerprint}}',
               '{{revisionId}}', 1, 'COMBO',
               TRUE, TRUE, '{{priceVersionId}}', 1,
               5000, 'VND', '{{depositPolicyId}}', 1,
               100000, '{{claimPolicyId}}', 1,
               7, 'Asia/Ho_Chi_Minh', TRUE,
               '{{cancellationPolicyId}}', 1,
               TRUE, TRUE,
               1, NOW(), NOW());

            -- Deposit Obligation
            INSERT INTO survey.survey_deposit_obligations
              (id, survey_order_id, tenant_id, farm_id, business_policy_version_id,
               deposit_policy_id, amount, currency, allocated_amount, status,
               created_at, satisfied_at, cancelled_at)
            VALUES
              ('{{obligationId}}', '{{orderId}}', '{{tenantId}}', '{{farmId}}', '{{policyVersionId}}',
               '{{depositPolicyId}}', 100000, 'VND', {{depositAllocatedAmountSql}},
               '{{depositObligationStatusSql}}'::system.survey_deposit_obligation_status,
               NOW(), {{depositSatisfiedAtSql}}, NULL);

            -- Appointment
            INSERT INTO survey.survey_appointments
              (id, survey_order_id, purpose, revision_number,
               proposed_by_system_manager_id, proposed_start_at, proposed_end_at,
               status, confirmed_by_tenant_owner_id, confirmed_at, created_at, updated_at)
            VALUES
              ('{{appointmentId}}', '{{orderId}}', 'CAPTURE'::system.survey_appointment_purpose, 1,
               '{{managerUserId}}', '{{appStart:O}}', '{{appEnd:O}}',
               '{{appointmentStatusSql}}'::system.survey_appointment_status,
               {{confirmedByOwnerSql}},
               {{confirmedAtSql}}, NOW(), NOW());
            """);

        if (isSatisfiedDeposit)
        {
            var txId = $"TX-{receiptId.ToString("N")[..12]}";
            var refId = $"REF-{receiptId.ToString("N")[..12]}";
            await ExecuteAsync(connection, $$"""
                INSERT INTO survey.survey_funding_receipts
                  (id, survey_order_id, tenant_id, amount, currency, deposit_obligation_id, payload_fingerprint, payment_reference, provider, provider_transaction_id, received_at, created_at)
                VALUES
                  ('{{receiptId}}', '{{orderId}}', '{{tenantId}}', 100000, 'VND', '{{obligationId}}', 'receipt-fp', '{{refId}}', 'VNPAY', '{{txId}}', NOW(), NOW());
                """);
        }

        return new V3OrderTestGraph(
            tenantId,
            farmId,
            droneId,
            orderId,
            requestId,
            managerUserId,
            ownerUserId,
            adminUserId,
            obligationId,
            policyVersionId);
    }

    private static async Task RecreateDatabaseAsync(string databaseName)
    {
        await DropDatabaseAsync(databaseName);
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string ToDbEnumString(SurveyOrderStatus status) => status switch
    {
        SurveyOrderStatus.PendingBoundaryVerification => "PENDING_BOUNDARY_VERIFICATION",
        SurveyOrderStatus.AwaitingBaselineAppointment => "AWAITING_BASELINE_APPOINTMENT",
        SurveyOrderStatus.AwaitingBaselineDeposit => "AWAITING_BASELINE_DEPOSIT",
        SurveyOrderStatus.AwaitingBoundaryCapture => "AWAITING_BOUNDARY_CAPTURE",
        SurveyOrderStatus.BaselineReady => "BASELINE_READY",
        SurveyOrderStatus.BaselineInProgress => "BASELINE_IN_PROGRESS",
        SurveyOrderStatus.AwaitingBaselineReview => "AWAITING_BASELINE_REVIEW",
        SurveyOrderStatus.AwaitingPricing => "AWAITING_PRICING",
        SurveyOrderStatus.AwaitingPaidAppointment => "AWAITING_PAID_APPOINTMENT",
        SurveyOrderStatus.AwaitingPayment => "AWAITING_PAYMENT",
        SurveyOrderStatus.ReadyForPaidService => "READY_FOR_PAID_SERVICE",
        SurveyOrderStatus.InProgress => "IN_PROGRESS",
        SurveyOrderStatus.PendingReview => "PENDING_REVIEW",
        SurveyOrderStatus.Completed => "COMPLETED",
        SurveyOrderStatus.Cancelled => "CANCELLED",
        SurveyOrderStatus.AwaitingAppointment => "AWAITING_APPOINTMENT",
        SurveyOrderStatus.AwaitingDeposit => "AWAITING_DEPOSIT",
        SurveyOrderStatus.ReadyForCapture => "READY_FOR_CAPTURE",
        SurveyOrderStatus.Capturing => "CAPTURING",
        SurveyOrderStatus.AwaitingCaptureUpload => "AWAITING_CAPTURE_UPLOAD",
        SurveyOrderStatus.AwaitingInputReview => "AWAITING_INPUT_REVIEW",
        SurveyOrderStatus.AwaitingProcessing => "AWAITING_PROCESSING",
        SurveyOrderStatus.Processing => "PROCESSING",
        SurveyOrderStatus.AwaitingReflightDecision => "AWAITING_REFLIGHT_DECISION",
        SurveyOrderStatus.AwaitingSettlement => "AWAITING_SETTLEMENT",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private sealed class TestExecutionContext(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? ActorId => actorId;
        public Guid? TenantId => null;
        public Guid CorrelationId => Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class TestMissionRepository(DroneMission mission) : IDroneMissionRepository
    {
        public Task<DroneMission?> GetByIdAsync(Guid id, Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DroneMission?>(mission.Id == id ? mission : null);
        public Task<bool> CodeExistsAsync(Guid farmId, string missionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<IReadOnlyList<DroneMission>> GetBySurveyOrderIdAsync(Guid surveyOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneMission>>([mission]);
        public void Add(DroneMission mission) { }
    }

    private sealed class TestManagerAccessService(Guid tenantId, Guid farmId) : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid targetFarmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Allowed(tenantId, farmId, Guid.NewGuid()));
    }
}
