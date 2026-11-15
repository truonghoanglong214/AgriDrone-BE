using System.Text.Json;
using AgriDrone.Database.Surveys;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303H2CancellationAcceptanceTests : V303H2IntegrationTestBase
{
    public V303H2CancellationAcceptanceTests() : base("v303h2_cancel")
    {
    }

    [Fact]
    public async Task Scenario03T10CustomerBeforeCaptureAndOperationalPostponementRefundFullDeposit()
    {
        // 1. Customer before capture cancellation
        var seeded = await SeedV3OrderGraphAsync(DatabaseName, isSatisfiedDeposit: true, hasConfirmedAppointment: true);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var ownerCancelVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var ownerCancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            ownerCancelVer,
            seeded.OwnerUserId,
            seeded.OwnerUserId,
            CaptureCancellationDecisionKind.CustomerBeforeCapture,
            0m,
            null,
            "OWNER_PRE_FLIGHT_CANCEL",
            "Owner cancelled before flight",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "owner-cancel-fp"));

        Assert.True(ownerCancelRes.Success, ownerCancelRes.ErrorMessage);

        await using var verifyCtx = CreateContext(DatabaseName);
        var decision = await verifyCtx.Set<SurveyCancellationDecision>()
            .SingleAsync(d => d.SurveyOrderId == seeded.OrderId);

        Assert.Equal(100000m, decision.RefundAmount);
        Assert.Equal(0m, decision.RetainedAmount);

        var refundObligation = await verifyCtx.Set<SurveyRefundObligation>()
            .SingleAsync(r => r.SurveyOrderId == seeded.OrderId);

        Assert.Equal(100000m, refundObligation.Amount);
        Assert.Equal(SurveyRefundObligationStatus.Pending, refundObligation.Status);
    }

    [Fact]
    public async Task Scenario03T10OperationalPostponementHasZeroRetention()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName, isSatisfiedDeposit: true, hasConfirmedAppointment: true);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        var cancelVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var cancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            cancelVer,
            seeded.AdminUserId,
            seeded.AdminUserId,
            CaptureCancellationDecisionKind.OperationalPostponement,
            0m,
            null,
            "WEATHER_POSTPONEMENT",
            "Severe rainstorm prevents flight within season",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "weather-cancel-fp"));

        Assert.True(cancelRes.Success, cancelRes.ErrorMessage);

        await using var verifyCtx = CreateContext(DatabaseName);
        var decision = await verifyCtx.Set<SurveyCancellationDecision>()
            .SingleAsync(d => d.SurveyOrderId == seeded.OrderId);

        Assert.Equal(100000m, decision.RefundAmount);
        Assert.Equal(0m, decision.RetainedAmount);
    }

    [Fact]
    public async Task Scenario03T11CustomerAfterAcceptableCaptureRequiresAdminAndVerifiedApprovalEvidence()
    {
        var seeded = await SeedV3OrderGraphAsync(DatabaseName, isSatisfiedDeposit: true, hasConfirmedAppointment: true);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);

        // Advance to flight completed
        var orderVerPrep = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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
        Assert.True(startRes.Success);

        var now = DateTimeOffset.UtcNow;
        var orderVerEnd = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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

        // Add real media asset and freeze manifest
        var asset = MediaAsset.Create(
            Guid.NewGuid(),
            seeded.TenantId,
            seeded.FarmId,
            "minio",
            $"captures/{prepRes.AttemptId}/image-001.jpg",
            $"s3://agridrone-test/captures/{prepRes.AttemptId}/image-001.jpg",
            MediaType.Image,
            "image/jpeg",
            2048,
            new string('e', 64),
            seeded.ManagerUserId,
            now);

        var missionMedia = MissionMedia.Create(
            prepRes.MissionId,
            asset.Id,
            MissionMediaRole.RawImage,
            now,
            now);

        context.Set<MediaAsset>().Add(asset);
        context.Set<MissionMedia>().Add(missionMedia);
        await context.SaveChangesAsync();

        var orderVerFreeze = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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
        Assert.True(freezeRes.Success);

        var processingJobId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $$"""
                INSERT INTO mission.ai_processing_jobs (id, mission_id, job_type, status, parameters, attempt_number, input_manifest, output_manifest, queued_at)
                VALUES ('{{processingJobId}}', '{{prepRes.MissionId}}', 'FRAME_EXTRACTION'::system.ai_job_type, 'QUEUED'::system.ai_job_status, '{}'::jsonb, 1, '{}'::jsonb, '{}'::jsonb, NOW());
                """);
        }

        var orderVerApprove = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
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
            "Quality pass"));
        Assert.True(approveRes.Success);

        // 1. Manager cannot make CustomerAfterAcceptableCapture decision (System Admin only)
        var orderVerMgr = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var managerCancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVerMgr,
            seeded.ManagerUserId,
            seeded.ManagerUserId,
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

        // 2. Admin cancellation with deposit retention
        var orderVerAdmin = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var adminCancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVerAdmin,
            seeded.AdminUserId,
            seeded.AdminUserId,
            CaptureCancellationDecisionKind.CustomerAfterAcceptableCapture,
            100000m,
            approveRes.ApprovalId,
            "ADMIN_ABORT",
            "Admin cancelled post-capture with policy deposit retention",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "admin-cancel-fp"));

        Assert.True(adminCancelRes.Success, adminCancelRes.ErrorMessage);

        await using var verifyCtx = CreateContext(DatabaseName);
        var postCaptureDecision = await verifyCtx.Set<SurveyCancellationDecision>()
            .SingleAsync(d => d.SurveyOrderId == seeded.OrderId);

        Assert.Equal(0m, postCaptureDecision.RefundAmount);
        Assert.Equal(100000m, postCaptureDecision.RetainedAmount);
    }
}
