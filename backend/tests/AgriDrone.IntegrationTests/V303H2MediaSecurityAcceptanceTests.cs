using System.Text.Json;
using System.Security.Cryptography;
using AgriDrone.Database.Surveys;
using AgriDrone.Integrations.Media;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.ResetMissionUpload;
using AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class V303H2MediaSecurityAcceptanceTests : V303H2IntegrationTestBase
{
    public V303H2MediaSecurityAcceptanceTests() : base("v303h2_media")
    {
    }

    [Fact]
    public async Task Scenario03T07AttemptLifecycleIsolationAndLegacyRouteRejection()
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
    public async Task Scenario03T08ManifestFreezeRequiresRealMediaAndChecksum()
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
            "prep-fp",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("{}"), true,
            "Checklist pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
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

        // 1. Negative test: Attempting to freeze manifest with NO media attached to mission
        var orderVerFreezeEmpty = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var emptyFreezeRes = await coordinator.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seeded.OrderId,
            orderVerFreezeEmpty,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "freeze-empty-fp"));

        Assert.False(emptyFreezeRes.Success);
        Assert.Equal("CAPTURE_MEDIA_INCOMPLETE", emptyFreezeRes.ErrorCode);

        // 2. Negative test: Pre-created manifest in database without operation history is rejected
        var fakeManifest = CaptureInputManifest.Create(
            Guid.NewGuid(),
            seeded.TenantId,
            seeded.FarmId,
            seeded.OrderId,
            prepRes.AttemptId,
            prepRes.MissionId,
            new string('f', 64),
            1,
            1024,
            now,
            "files=1");
        context.Set<CaptureInputManifest>().Add(fakeManifest);
        await context.SaveChangesAsync();

        var conflictFreezeRes = await coordinator.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seeded.OrderId,
            orderVerFreezeEmpty,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "freeze-conflict-fp"));

        Assert.False(conflictFreezeRes.Success);
        Assert.Equal("CAPTURE_MANIFEST_STATE_CONFLICT", conflictFreezeRes.ErrorCode);

        // Clean up pre-created fake manifest
        context.Set<CaptureInputManifest>().Remove(fakeManifest);
        await context.SaveChangesAsync();

        // 3. Positive test: upload a real object to MinIO, then persist the finalized
        // metadata produced by that object.
        var assetId = Guid.NewGuid();
        var objectBytes = System.Text.Encoding.UTF8.GetBytes(
            $"03H2 immutable capture object {prepRes.AttemptId:D}");
        var rawChecksum = Convert.ToHexString(SHA256.HashData(objectBytes)).ToLowerInvariant();
        var bucket = "agridrone-h2";
        var objectName = $"captures/{prepRes.AttemptId:D}/image-001.jpg";
        var objectStorage = new TestObjectStorage();
        var storageUri = $"minio://{bucket}/{objectName}";
        await using (var content = new MemoryStream(objectBytes, writable: false))
        {
            await objectStorage.UploadAsync(
                storageUri,
                content,
                objectBytes.LongLength,
                "image/jpeg");
        }
        var asset = MediaAsset.Create(
            assetId,
            seeded.TenantId,
            seeded.FarmId,
            "minio",
            objectName,
            storageUri,
            MediaType.Image,
            "image/jpeg",
            objectBytes.LongLength,
            rawChecksum,
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

        // Now freeze manifest succeeds and computes real canonical fingerprint
        var opId = Guid.NewGuid();
        var fp = "freeze-valid-fp";
        var orderVerValidFreeze = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var storageCoordinator = CreateCoordinator(context, objectStorage: objectStorage);

        var validFreezeRes = await storageCoordinator.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seeded.OrderId,
            orderVerValidFreeze,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            opId,
            fp));

        Assert.True(validFreezeRes.Success, validFreezeRes.ErrorMessage);
        Assert.Equal(1, validFreezeRes.TotalFileCount);
        Assert.Equal(objectBytes.LongLength, validFreezeRes.TotalSizeBytes);
        Assert.Equal(64, validFreezeRes.ManifestFingerprint.Length);

        // 4. Idempotent replay with same operationId and payload returns the exact same manifest
        var replayRes = await storageCoordinator.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seeded.OrderId,
            orderVerValidFreeze,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            opId,
            fp));

        Assert.True(replayRes.Success);
        Assert.Equal(validFreezeRes.ManifestId, replayRes.ManifestId);

        // 5. Replay with different payload returns OPERATION_CONFLICT
        var conflictOpRes = await storageCoordinator.FreezeInputManifestAsync(new FreezeCaptureInputManifestRequest(
            seeded.OrderId,
            orderVerValidFreeze,
            prepRes.AttemptId,
            endRes.Version,
            prepRes.MissionId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            opId,
            "different-payload-fp"));

        Assert.False(conflictOpRes.Success);
        Assert.Equal("OPERATION_CONFLICT", conflictOpRes.ErrorCode);
    }

    [Fact]
    public async Task Scenario03T09TechnicalInputApprovalAdvancesToAwaitingReviewBeforeAccepted()
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
            "prep-fp",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "prep-fp"));
        Assert.True(prepRes.Success);

        var mission = await context.Set<DroneMission>().SingleAsync(m => m.Id == prepRes.MissionId);
        mission.CompletePreflight(
            Guid.NewGuid(), "1.0", JsonDocument.Parse("{}"), true,
            "Checklist pass", seeded.ManagerUserId, DateTimeOffset.UtcNow);
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

        // Add real media asset to compute valid manifest
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
            new string('c', 64),
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
        Assert.True(freezeRes.Success, freezeRes.ErrorMessage);

        // Insert processing job record to satisfy foreign key fk_capture_input_approvals_processing_job
        var processingJobId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(BuildConnectionString(DatabaseName)))
        {
            await conn.OpenAsync();
            await ExecuteAsync(conn, $$"""
                INSERT INTO mission.ai_processing_jobs (id, mission_id, job_type, status, parameters, attempt_number, input_manifest, output_manifest, queued_at)
                VALUES ('{{processingJobId}}', '{{prepRes.MissionId}}', 'FRAME_EXTRACTION'::system.ai_job_type, 'QUEUED'::system.ai_job_status, '{}'::jsonb, 1, '{}'::jsonb, '{}'::jsonb, NOW());
                """);
        }

        // Approval with wrong manifest fingerprint must fail
        var orderVerApprove = await GetOrderVersionAsync(DatabaseName, seeded.OrderId);
        var badApproveRes = await coordinator.ApproveInputAsync(new ApproveCaptureInputRequest(
            seeded.OrderId,
            orderVerApprove,
            prepRes.AttemptId,
            freezeRes.Version,
            freezeRes.ManifestId,
            new string('0', 64), // Mismatched fingerprint
            processingJobId,
            seeded.ManagerUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "bad-approve-fp",
            "Fingerprint mismatch test"));

        Assert.False(badApproveRes.Success);
        Assert.Equal("INPUT_VERSION_CONFLICT", badApproveRes.ErrorCode);

        // Valid approval succeeds
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

        Assert.True(approveRes.Success, approveRes.ErrorMessage);

        // Verify order is now Processing (ready for AI processing ingestion in V3-04)
        // NOT terminal Completed
        await using var verifyCtx = CreateContext(DatabaseName);
        var finalOrder = await verifyCtx.Set<SurveyOrder>().AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        Assert.Equal(SurveyOrderStatus.Processing, finalOrder.Status);
        Assert.NotEqual(SurveyOrderStatus.Completed, finalOrder.Status);

        var finalAttempt = await verifyCtx.Set<CaptureAttempt>().AsNoTracking().SingleAsync(a => a.Id == prepRes.AttemptId);
        Assert.Equal(CaptureAttemptStatus.Processing, finalAttempt.Status);
    }

    private sealed class TestManagerAccessService(Guid tenantId, Guid farmId) : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid targetFarmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(targetFarmId == farmId
                ? SystemManagerFarmAccess.Allowed(tenantId, farmId, Guid.NewGuid())
                : SystemManagerFarmAccess.Denied("Forbidden"));
    }

    private sealed class TestMissionRepository(DroneMission mission) : IDroneMissionRepository
    {
        public Task<DroneMission?> GetByIdAsync(Guid missionId, Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(mission.Id == missionId ? mission : null);

        public Task<bool> CodeExistsAsync(Guid farmId, string missionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<DroneMission>> GetBySurveyOrderIdAsync(Guid surveyOrderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneMission>>([mission]);

        public void Add(DroneMission droneMission) { }
    }

    private sealed class TestObjectStorage : IObjectStorage, IObjectStorageWriter
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _storage = new();

        public Task UploadAsync(string storageUri, Stream content, long length, string mimeType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            _storage[storageUri] = ms.ToArray();
            return Task.CompletedTask;
        }

        public Task<StoredObjectInfo?> GetInfoAsync(string storageUri, CancellationToken cancellationToken = default)
        {
            if (_storage.TryGetValue(storageUri, out var bytes))
            {
                return Task.FromResult<StoredObjectInfo?>(new StoredObjectInfo(
                    storageUri,
                    "minio",
                    storageUri,
                    "image/jpeg",
                    bytes.LongLength,
                    "SHA256",
                    null));
            }
            return Task.FromResult<StoredObjectInfo?>(null);
        }

        public Task<ObjectUploadSession> CreateUploadSessionAsync(ObjectUploadRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ObjectUploadSession($"minio://test/{request.ObjectName}", new Uri($"http://127.0.0.1:59000/test/{request.ObjectName}"), DateTimeOffset.UtcNow.AddHours(1)));

        public Task ReadAsync(string storageUri, Func<Stream, CancellationToken, Task> reader, CancellationToken cancellationToken = default)
        {
            if (_storage.TryGetValue(storageUri, out var bytes))
            {
                using var ms = new MemoryStream(bytes);
                return reader(ms, cancellationToken);
            }
            throw new FileNotFoundException(storageUri);
        }

        public Task<Uri> CreateDownloadUriAsync(string storageUri, TimeSpan lifetime, CancellationToken cancellationToken = default)
            => Task.FromResult(new Uri($"http://127.0.0.1:59000/download/{storageUri}"));

        public Task DeleteAsync(string storageUri, CancellationToken cancellationToken = default)
        {
            _storage.TryRemove(storageUri, out _);
            return Task.CompletedTask;
        }
    }
}
