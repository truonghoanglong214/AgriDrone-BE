using System.Text.Json;
using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Identity.Application.Features.SystemManagers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.Modules.Identity.Infrastructure.Repositories;
using AgriDrone.Modules.Missions.Application.Features.Drones.ChangeDroneStatus;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.Modules.Missions.Infrastructure.Repositories;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303H2CaptureTransactionRaceTests : V303H2IntegrationTestBase
{
    public V303H2CaptureTransactionRaceTests() : base("v303h2_race")
    {
    }

    [Fact]
    public async Task Scenario03T04StartCaptureVsCancellationDeterministicBarrierRace()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        // Preflight & Prepare flight
        var orderVerInitial = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        var orderVerForRace = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        // Use BarrierCaptureProbe to force both transactions to meet at BeforeOrderLock before racing on row lock
        var barrierProbe = new BarrierCaptureProbe(2);

        var startTask = Task.Run(async () =>
        {
            await using var ctx = CreateContext(DatabaseName);
            var coord = CreateCoordinator(ctx, barrierProbe);
            return await coord.StartFlightAsync(startReq);
        });

        var cancelTask = Task.Run(async () =>
        {
            await using var ctx = CreateContext(DatabaseName);
            var coord = CreateCoordinator(ctx, barrierProbe);
            return await coord.CancelOrderAsync(cancelReq);
        });

        await Task.WhenAll(startTask, cancelTask);
        var startResult = await startTask;
        var cancelResult = await cancelTask;

        // In serializable isolation with row-level locks, exactly one can succeed
        Assert.False(startResult.Success && cancelResult.Success, "Both Start and Cancel succeeded simultaneously!");

        await using var verifyContext = CreateContext(DatabaseName);
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
    public async Task Scenario03T04StartCaptureVsDroneMaintenanceConflictProtection()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerInitial = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerInitial,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-flight-maint",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success, prepRes.ErrorMessage);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("{}"), true,
            "Preflight pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        // 1. If drone goes into Maintenance first
        var drone = await context.Set<Drone>().SingleAsync(d => d.Id == seeded.DroneId);
        drone.SendToMaintenance(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var orderVerStart = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        // Must reject because drone is in maintenance
        Assert.False(startRes.Success);
        Assert.Contains("PREFLIGHT_OR_DRONE_NOT_READY", startRes.ErrorCode);
    }

    [Fact]
    public async Task Scenario03T04StartCaptureVsManagerAssignmentRevokeConflictProtection()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerInitial = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerInitial,
            seeded.ManagerUserId,
            seeded.DroneId,
            "prep-flight-revoke",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success, prepRes.ErrorMessage);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("{}"), true,
            "Preflight pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        // Start capture flight
        var orderVerStart = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        // Attempting to end primary manager assignment while capture is InFlight must be blocked by IActiveCaptureAssignmentGuard
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(BuildConnectionString(DatabaseName))
            .Options;
        await using var identityContext = new IdentityDbContext(identityOptions);
        var assignmentRepo = new FarmManagerAssignmentRepository(identityContext);
        var guard = new ActiveCaptureAssignmentGuard(context);
        var execContext = new TestExecutionContext(seeded.AdminUserId);

        var revokeHandler = new EndPrimaryFarmManagerAssignmentCommandHandler(
            assignmentRepo,
            identityContext,
            new AuditWriter(),
            new NoopAuditLogSink(),
            execContext,
            TimeProvider.System,
            guard);

        var revokeRes = await revokeHandler.Handle(
            new EndPrimaryFarmManagerAssignmentCommand(seeded.FarmId, "Admin reassignment", 1),
            CancellationToken.None);

        Assert.True(revokeRes.IsFailure);
        Assert.Equal("ACTIVE_CAPTURE_IN_FLIGHT", revokeRes.Error.Code);
    }

    [Theory]
    [InlineData(SurveyCaptureCheckpoint.AfterAttemptAndMissionTracked)]
    [InlineData(SurveyCaptureCheckpoint.AfterStateTransitionsTracked)]
    [InlineData(SurveyCaptureCheckpoint.AfterAuditAndOutboxTracked)]
    [InlineData(SurveyCaptureCheckpoint.AfterSaveBeforeCommit)]
    public async Task Scenario03T05StartCaptureFaultRollbackMaintainsZeroGhostAttempts(SurveyCaptureCheckpoint checkpoint)
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var orderVerPrep = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var prepRes = await coordinator.PrepareAttemptAsync(new PrepareCaptureAttemptRequest(
            seeded.OrderId,
            orderVerPrep,
            seeded.ManagerUserId,
            seeded.DroneId,
            $"prep-fault-{checkpoint}",
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
        var startReq = new StartCaptureFlightRequest(
            seeded.OrderId,
            orderVerStart,
            prepRes.AttemptId,
            prepRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "start-fp");

        // 1. Execute with injected failure probe at the specified checkpoint (e.g. AfterSaveBeforeCommit)
        var throwProbe = new ThrowAtCheckpointProbe(checkpoint);
        await using (var faultCtx = CreateContext(DatabaseName))
        {
            var faultCoordinator = CreateCoordinator(faultCtx, throwProbe);
            var faultRes = await faultCoordinator.StartFlightAsync(startReq);
            Assert.False(faultRes.Success);
        }

        // 2. Open brand new DbContext and assert strict rollback of all mutations
        await using (var verifyCtx = CreateContext(DatabaseName))
        {
            var order = await verifyCtx.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
            Assert.Equal(SurveyOrderStatus.ReadyForCapture, order.Status);

            var attempt = await verifyCtx.Set<CaptureAttempt>().AsNoTracking().SingleAsync(a => a.Id == prepRes.AttemptId);
            Assert.Equal(CaptureAttemptStatus.Planned, attempt.Status);
            Assert.Null(attempt.StartedAt);

            var missionPost = await verifyCtx.Set<DroneMission>().AsNoTracking().SingleAsync(m => m.Id == prepRes.MissionId);
            Assert.Equal(MissionStatus.Scheduled, missionPost.Status);

            var drone = await verifyCtx.Set<Drone>().AsNoTracking().SingleAsync(d => d.Id == seeded.DroneId);
            Assert.Equal(DroneStatus.Available, drone.Status);

            var capturingHistories = await verifyCtx.Set<OrderStateHistory>()
                .Where(h => h.SurveyOrderId == seeded.OrderId && h.ToStatus == SurveyOrderStatus.Capturing)
                .ToListAsync();
            Assert.Empty(capturingHistories);

            var captureStartOutbox = (await verifyCtx.Set<AgriDrone.SharedInfrastructure.Messaging.Persistence.OutboxMessage>()
                .Where(m => m.PartitionKey == seeded.OrderId.ToString("D") && m.EventType == IntegrationEventTypes.SurveyCaptureStateChangedV1)
                .ToListAsync())
                .Where(m => System.Text.Encoding.UTF8.GetString(m.Body).Contains("CAPTURE_STARTED"))
                .ToList();
            Assert.Empty(captureStartOutbox);
        }

        // 3. Retry with normal coordinator succeeds exactly once
        await using (var retryCtx = CreateContext(DatabaseName))
        {
            var retryCoord = CreateCoordinator(retryCtx);
            var retryOrderVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
            var retryReq = startReq with { ExpectedOrderVersion = retryOrderVer };
            var retryRes = await retryCoord.StartFlightAsync(retryReq);
            Assert.True(retryRes.Success, retryRes.ErrorMessage);
        }
    }

    [Fact]
    public async Task Scenario03T06QualityReflightEnforcesSingleActiveSuccessorAndReusesDeposit()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        // Advance to flight completed & recorded failure
        var orderVerPrep = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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
            Guid.NewGuid(), "1.0", JsonDocument.Parse("{}"), true,
            "Preflight pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        var orderVerStart = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        var orderVerFail = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        // Seed a replacement available drone
        var reflightDroneId = Guid.NewGuid();
        var reflightDroneCode = $"DRN-{reflightDroneId:N}"[..15];
        var reflightRegNum = $"VN-{reflightDroneId:N}"[..8].ToUpperInvariant();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $"""
                INSERT INTO mission.drones (id, code, name, status, specifications, registration_number, registration_date, registration_expiry_date)
                VALUES ('{reflightDroneId}', '{reflightDroneCode}', 'Reflight Drone', 'AVAILABLE'::system.drone_status, '{"{\"capabilities\":[\"BASELINE_MAPPING\",\"PLANT_HEALTH\"]}"}'::jsonb, '{reflightRegNum}', '2020-01-01', '2030-01-01');
                """);
        }

        var orderVerReflight = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var reflightReq1 = new CreateQualityReflightRequest(
            seeded.OrderId,
            orderVerReflight,
            prepRes.AttemptId,
            failFlightRes.Version,
            reflightDroneId,
            DateTimeOffset.UtcNow.AddHours(4),
            DateTimeOffset.UtcNow.AddHours(6),
            seeded.ManagerUserId,
            Guid.NewGuid(),
            "Turbulence failure requires full re-flight",
            "INCIDENT-REF-01",
            Guid.NewGuid(),
            "reflight-fp-1");

        var reflightReq2 = new CreateQualityReflightRequest(
            seeded.OrderId,
            orderVerReflight,
            prepRes.AttemptId,
            failFlightRes.Version,
            reflightDroneId,
            DateTimeOffset.UtcNow.AddHours(4),
            DateTimeOffset.UtcNow.AddHours(6),
            seeded.ManagerUserId,
            Guid.NewGuid(),
            "Turbulence failure requires full re-flight",
            "INCIDENT-REF-02",
            Guid.NewGuid(),
            "reflight-fp-2");

        var task1 = Task.Run(async () =>
        {
            await using var ctx = CreateContext(DatabaseName);
            var coord = CreateCoordinator(ctx);
            return await coord.CreateQualityReflightAsync(reflightReq1);
        });

        var task2 = Task.Run(async () =>
        {
            await using var ctx = CreateContext(DatabaseName);
            var coord = CreateCoordinator(ctx);
            return await coord.CreateQualityReflightAsync(reflightReq2);
        });

        await Task.WhenAll(task1, task2);
        var res1 = await task1;
        var res2 = await task2;

        Assert.True(res1.Success ^ res2.Success, "Exactly one reflight command must succeed!");
        var winner = res1.Success ? res1 : res2;
        var loser = res1.Success ? res2 : res1;

        Assert.True(winner.Success);
        Assert.False(loser.Success);
        Assert.Equal("REFLIGHT_CONFLICT", loser.ErrorCode);

        // Check successor attempt created and deposit obligation reused
        await using var verifyContext = CreateContext(DatabaseName);
        var attempts = await verifyContext.Set<CaptureAttempt>().Where(a => a.OrderId == seeded.OrderId).ToListAsync();
        Assert.Equal(2, attempts.Count);

        var successor = attempts.Single(a => a.AttemptIndex == 2);
        Assert.Equal(prepRes.AttemptId, successor.SupersededAttemptId);
        Assert.Equal(CaptureAttemptStatus.Planned, successor.Status);

        // Check order requires NO new deposit payment
        var obligationCount = await verifyContext.Set<SurveyDepositObligation>()
            .CountAsync(o => o.SurveyOrderId == seeded.OrderId);
        Assert.Equal(1, obligationCount);
    }
}
