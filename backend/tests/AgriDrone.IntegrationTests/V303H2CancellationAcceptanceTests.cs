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
    public async Task Scenario03T10ServiceFailureReturnsFullDeposit()
    {
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            isSatisfiedDeposit: true,
            hasConfirmedAppointment: true);
        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);
        var orderVersion = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);

        var result = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVersion,
            seeded.AdminUserId,
            seeded.AdminUserId,
            CaptureCancellationDecisionKind.ServiceFailure,
            0m,
            null,
            "SERVICE_FAILURE",
            "Platform could not provide the contracted capture service",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "service-failure-fp"));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(100000m, result.RefundAmount);
        Assert.Equal(0m, result.RetainedAmount);

        await using var verifyContext = CreateContext(DatabaseName);
        var obligation = await verifyContext.Set<SurveyRefundObligation>()
            .AsNoTracking()
            .SingleAsync(value => value.SurveyOrderId == seeded.OrderId);
        Assert.Equal(SurveyRefundOriginKind.ServiceFailure, obligation.OriginKind);
        Assert.Equal(100000m, obligation.Amount);
    }

    [Theory]
    [InlineData(1, "FreeReschedule")]
    [InlineData(0, "AdminReviewRequired")]
    public async Task Scenario03T10NoShowUsesPinnedFreeAllowance(
        int allowedFreeReschedules,
        string expectedDisposition)
    {
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            isSatisfiedDeposit: true,
            hasConfirmedAppointment: true);
        await using (var connection = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"""
                UPDATE survey.order_commercial_snapshots
                SET allowed_free_no_show_reschedules = {allowedFreeReschedules}
                WHERE survey_order_id = '{seeded.OrderId}';
                """);
        }

        await using var context = CreateContext(DatabaseName);
        var appointment = await context.Set<SurveyAppointment>()
            .AsNoTracking()
            .SingleAsync(value => value.SurveyOrderId == seeded.OrderId);
        var orderVersion = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var operationId = Guid.NewGuid();
        var coordinator = CreateCoordinator(context);
        var result = await coordinator.RecordNoShowAsync(new RecordCaptureNoShowRequest(
            seeded.OrderId,
            orderVersion,
            appointment.Id,
            appointment.Version,
            seeded.ManagerUserId,
            DateTimeOffset.UtcNow,
            "Owner did not attend the confirmed capture window",
            "OPS-NO-SHOW-001",
            Guid.NewGuid(),
            operationId,
            $"no-show-{allowedFreeReschedules}"));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(expectedDisposition, result.Disposition);
        Assert.Equal(nameof(SurveyOrderStatus.AwaitingAppointment), result.OrderStatus);
        Assert.Equal(nameof(SurveyAppointmentStatus.RescheduleRequested), result.AppointmentStatus);

        await using var verifyContext = CreateContext(DatabaseName);
        var noShow = await verifyContext.Set<SurveyAppointmentNoShow>()
            .AsNoTracking()
            .SingleAsync(value => value.OperationId == operationId);
        Assert.Equal(1, noShow.OccurrenceIndex);
        Assert.Equal(expectedDisposition, noShow.Disposition.ToString());
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

    [Fact]
    public async Task Scenario03T11RefundCapEnforcesActiveReservationsAndCompletedRefunds()
    {
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            isSatisfiedDeposit: true,
            hasConfirmedAppointment: true);

        // Insert a confirmed funding receipt of 100,000 VND
        var receiptId = Guid.CreateVersion7();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $"""
                INSERT INTO survey.survey_funding_receipts
                  (id, tenant_id, survey_order_id, deposit_obligation_id, provider, provider_event_id,
                   amount, currency, provider_reference, payload_fingerprint, received_at, created_at)
                VALUES
                  ('{receiptId}', '{seeded.TenantId}', '{seeded.OrderId}', '{seeded.ObligationId}', 'sepay', 'evt-{Guid.NewGuid():N}',
                   100000, 'VND', 'REF-{Guid.NewGuid():N}', 'fp', NOW() - INTERVAL '10 minutes', NOW() - INTERVAL '10 minutes');

                -- Add an active pending refund obligation of 40,000 VND
                INSERT INTO survey.survey_refund_obligations
                  (id, survey_order_id, tenant_id, origin_kind, origin_decision_id, amount, currency, status, created_at, updated_at)
                VALUES
                  ('{Guid.CreateVersion7()}', '{seeded.OrderId}', '{seeded.TenantId}', 'CancellationReturn'::system.survey_refund_origin_kind,
                   NULL, 40000, 'VND', 'Pending'::system.survey_refund_obligation_status, NOW() - INTERVAL '5 minutes', NOW() - INTERVAL '5 minutes');
                """);
        }

        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);
        var orderVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);

        // Cancel order: total available unreserved = 100,000 - 40,000 = 60,000 VND
        var cancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVer,
            seeded.OwnerUserId,
            seeded.OwnerUserId,
            CaptureCancellationDecisionKind.CustomerBeforeCapture,
            0m,
            null,
            "CANCELLATION_CAP",
            "Customer cancelled; refund must respect active reservation cap",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cancel-cap-fp"));

        Assert.True(cancelRes.Success, cancelRes.ErrorMessage);
        Assert.Equal(60000m, cancelRes.RefundAmount);
        Assert.Equal(0m, cancelRes.RetainedAmount);

        // Now test REFUND_RECONCILIATION_HOLD when total refund reservations exceed receipts
        var overSeeded = await SeedV3OrderGraphAsync(DatabaseName, isSatisfiedDeposit: true, hasConfirmedAppointment: true);
        await using (var conn2 = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn2.OpenAsync();
            await ExecuteAsync(conn2, $"""
                INSERT INTO survey.survey_funding_receipts
                  (id, tenant_id, survey_order_id, deposit_obligation_id, provider, provider_event_id,
                   amount, currency, provider_reference, payload_fingerprint, received_at, created_at)
                VALUES
                  ('{Guid.CreateVersion7()}', '{overSeeded.TenantId}', '{overSeeded.OrderId}', '{overSeeded.ObligationId}', 'sepay', 'evt-{Guid.NewGuid():N}',
                   50000, 'VND', 'REF-{Guid.NewGuid():N}', 'fp', NOW() - INTERVAL '10 minutes', NOW() - INTERVAL '10 minutes');

                -- Existing completed refunds = 80,000 VND > 50,000 receipts
                INSERT INTO survey.survey_refund_obligations
                  (id, survey_order_id, tenant_id, origin_kind, origin_decision_id, amount, currency, status, created_at, updated_at)
                VALUES
                  ('{Guid.CreateVersion7()}', '{overSeeded.OrderId}', '{overSeeded.TenantId}', 'CancellationReturn'::system.survey_refund_origin_kind,
                   NULL, 80000, 'VND', 'Completed'::system.survey_refund_obligation_status, NOW() - INTERVAL '5 minutes', NOW() - INTERVAL '5 minutes');
                """);
        }

        await using var overContext = CreateContext(DatabaseName);
        var overCoordinator = CreateCoordinator(overContext);
        var overOrderVer = await GetOrderVersionAsync(DatabaseName, overSeeded.OrderId);

        var overCancelRes = await overCoordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            overSeeded.OrderId,
            overOrderVer,
            overSeeded.OwnerUserId,
            overSeeded.OwnerUserId,
            CaptureCancellationDecisionKind.CustomerBeforeCapture,
            0m,
            null,
            "OVER_REFUND_FAIL",
            "Hold when reservations exceed receipts",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "over-hold-fp"));

        Assert.False(overCancelRes.Success);
        Assert.Equal("REFUND_RECONCILIATION_HOLD", overCancelRes.ErrorCode);
    }

    [Fact]
    public async Task Scenario03T11MissingQualityProofFailsCustomerAfterAcceptableCapture()
    {
        var seeded = await SeedV3OrderGraphAsync(
            DatabaseName,
            isSatisfiedDeposit: true,
            hasConfirmedAppointment: true);

        await using var context = CreateContext(DatabaseName);
        var coordinator = CreateCoordinator(context);
        var orderVer = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);

        // Attempting CustomerAfterAcceptableCapture with non-existent input approval evidence
        var fakeApprovalEvidenceId = Guid.NewGuid();
        var cancelRes = await coordinator.CancelOrderAsync(new CancelCaptureOrderRequest(
            seeded.OrderId,
            orderVer,
            seeded.AdminUserId,
            seeded.AdminUserId,
            CaptureCancellationDecisionKind.CustomerAfterAcceptableCapture,
            100000m,
            fakeApprovalEvidenceId,
            "MISSING_PROOF",
            "Admin cannot retain deposit without verified input approval proof",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "missing-proof-fp"));

        Assert.False(cancelRes.Success);
        Assert.Equal("INPUT_APPROVAL_NOT_FOUND", cancelRes.ErrorCode);
    }
}
