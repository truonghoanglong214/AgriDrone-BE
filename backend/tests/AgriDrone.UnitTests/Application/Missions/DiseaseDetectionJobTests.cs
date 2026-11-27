using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Application.Features.Processing;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class DiseaseDetectionJobTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 28, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task EnqueueRejectsMissingActor()
    {
        using var fixture = new Fixture { ActorIdValue = null };

        var result = await fixture.Handler.Handle(fixture.DefaultCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MissionError.CurrentUserRequired().Code, result.Error.Code);
    }

    [Fact]
    public async Task EnqueueRejectsManagerWithoutFarmAccess()
    {
        using var fixture = new Fixture { AllowFarmAccess = false };

        var result = await fixture.Handler.Handle(fixture.DefaultCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DetectDiseases.FarmAccessDenied", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task GetJobRejectsManagerWithoutFarmAccess()
    {
        using var fixture = new Fixture { AllowFarmAccess = false };
        var query = new GetMissionDiseaseJobQuery(
            fixture.TenantIdValue,
            fixture.FarmIdValue,
            fixture.MissionIdValue,
            null);

        var result = await fixture.Handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DetectDiseases.FarmAccessDenied", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Theory]
    [MemberData(nameof(InvalidCommands))]
    public async Task EnqueueRejectsInvalidInputParameters(
        double confidence,
        double? matchingToleranceMeters,
        double? zoneRadiusMeters,
        int? minDiseasedTrees,
        string? polygonJson)
    {
        using var fixture = new Fixture();
        using var doc = polygonJson is null ? null : JsonDocument.Parse(polygonJson);
        JsonElement? polygon = doc?.RootElement.Clone();

        var command = new DetectMissionDiseasesCommand(
            fixture.TenantIdValue,
            fixture.FarmIdValue,
            fixture.MissionIdValue,
            confidence,
            polygon,
            matchingToleranceMeters,
            zoneRadiusMeters,
            minDiseasedTrees);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DetectDiseases.InvalidInput", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    public static IEnumerable<object?[]> InvalidCommands()
    {
        yield return [-0.01, null, null, null, null];
        yield return [1.01, null, null, null, null];
        yield return [double.NaN, null, null, null, null];
        yield return [0.25, 0.0, null, null, null];
        yield return [0.25, -1.5, null, null, null];
        yield return [0.25, double.PositiveInfinity, null, null, null];
        yield return [0.25, null, 0.0, null, null];
        yield return [0.25, null, -2.0, null, null];
        yield return [0.25, null, null, 0, null];
        yield return [0.25, null, null, -3, null];
        yield return [0.25, null, null, null, "[1,2,3]"];
        yield return [0.25, null, null, null, "\"Polygon\""];
    }

    [Fact]
    public void ToDiseaseResponseQueuedJobMapsOuterJobIdWithoutResult()
    {
        var jobId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var createdAt = Now;
        var job = new MissionImageJobStore.Job(
            jobId,
            missionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MissionImageJobStore.Disease,
            "QUEUED",
            "{}",
            null,
            null,
            createdAt,
            null,
            null);

        var response = job.ToDiseaseResponse();

        Assert.Equal(jobId, response.JobId);
        Assert.Equal(missionId, response.MissionId);
        Assert.Equal("QUEUED", response.Status);
        Assert.Equal(createdAt, response.CreatedAt);
        Assert.Null(response.StartedAt);
        Assert.Null(response.CompletedAt);
        Assert.Null(response.Result);
        Assert.Null(response.Error);
    }

    [Fact]
    public void ToDiseaseResponseCompletedJobPreservesDistinctOuterJobIdAndInnerRevision()
    {
        var outerTrackingJobId = Guid.NewGuid();
        var innerAiJobId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var startedAt = Now.AddMinutes(1);
        var completedAt = Now.AddMinutes(4);

        var result = new DetectMissionDiseasesResult(
            IsSuccess: true,
            MissionId: missionId,
            ModelName: "best_disease.pt",
            TotalImagesScanned: 216,
            TotalDetections: 1,
            DiseaseSummary: new Dictionary<string, int> { ["LeafSpot"] = 1 },
            CsvDownloadUrl: "/api/v1/disease/download/test.csv",
            Detections:
            [
                new DiseaseDetectionItem(
                    Id: "det-1",
                    ClassId: 0,
                    ClassName: "LeafSpot",
                    Lat: 10.75,
                    Lon: 106.66,
                    Confidence: 0.91,
                    BboxX: 10,
                    BboxY: 20,
                    BboxW: 30,
                    BboxH: 40,
                    ImageName: "img_0001.jpg",
                    CropImageUrl: null,
                    SourceStorageKey: "tenants/t/media/m/raw_1.jpg")
            ],
            JobId: innerAiJobId,
            SourceRevision: 3);

        var job = new MissionImageJobStore.Job(
            outerTrackingJobId,
            missionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MissionImageJobStore.Disease,
            "COMPLETED",
            "{}",
            JsonSerializer.Serialize(result),
            null,
            Now,
            startedAt,
            completedAt);

        var response = job.ToDiseaseResponse();

        Assert.Equal(outerTrackingJobId, response.JobId);
        Assert.Equal("COMPLETED", response.Status);
        Assert.Equal(startedAt, response.StartedAt);
        Assert.Equal(completedAt, response.CompletedAt);
        Assert.Null(response.Error);
        Assert.NotNull(response.Result);
        Assert.NotEqual(response.JobId, response.Result.JobId);
        Assert.Equal(innerAiJobId, response.Result.JobId);
        Assert.Equal(3, response.Result.SourceRevision);
        Assert.Equal(216, response.Result.TotalImagesScanned);
        Assert.Single(response.Result.Detections);
    }

    [Fact]
    public void ToDiseaseResponseFailedJobPreservesErrorAndNullResult()
    {
        var job = new MissionImageJobStore.Job(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            MissionImageJobStore.Disease,
            "FAILED",
            "{}",
            null,
            "DetectDiseases.ComboPolicyRequired: Missing policy.",
            Now,
            Now.AddSeconds(2),
            Now.AddSeconds(5));

        var response = job.ToDiseaseResponse();

        Assert.Equal("FAILED", response.Status);
        Assert.Equal("DetectDiseases.ComboPolicyRequired: Missing policy.", response.Error);
        Assert.Null(response.Result);
    }

    [Fact]
    public void MissionDiseaseInputRoundTripsJsonAndPolygonObject()
    {
        using var polygonDoc = JsonDocument.Parse("""{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}""");
        var actorId = Guid.NewGuid();
        var original = new MissionDiseaseInput(
            ActorId: actorId,
            ConfidenceThreshold: 0.35,
            PolygonGeoJson: polygonDoc.RootElement.Clone(),
            MatchingToleranceMeters: 3.5,
            ZoneRadiusMeters: 15.0,
            MinDiseasedTrees: 4);

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<MissionDiseaseInput>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(actorId, roundTripped.ActorId);
        Assert.Equal(0.35, roundTripped.ConfidenceThreshold);
        Assert.Equal(3.5, roundTripped.MatchingToleranceMeters);
        Assert.Equal(15.0, roundTripped.ZoneRadiusMeters);
        Assert.Equal(4, roundTripped.MinDiseasedTrees);
        Assert.NotNull(roundTripped.PolygonGeoJson);
        Assert.Equal(JsonValueKind.Object, roundTripped.PolygonGeoJson.Value.ValueKind);
        Assert.Equal("Polygon", roundTripped.PolygonGeoJson.Value.GetProperty("type").GetString());
    }

    [Fact]
    public void HasCompleteCoverageReturnsTrueWhenAllExpectedKeysSucceedAndDetectionsMatch()
    {
        const string key1 = "tenants/t/media/m/1.jpg";
        const string key2 = "tenants/t/media/m/2.jpg";
        var expected = new HashSet<string>(StringComparer.Ordinal) { key1, key2 };
        var result = new DetectMissionDiseasesResult(
            IsSuccess: true,
            MissionId: Guid.NewGuid(),
            ModelName: "best_disease.pt",
            TotalImagesScanned: 2,
            TotalDetections: 1,
            DiseaseSummary: new Dictionary<string, int> { ["LeafSpot"] = 1 },
            CsvDownloadUrl: null,
            Detections:
            [
                new DiseaseDetectionItem("1", 0, "LeafSpot", 10.1, 106.1, 0.88, 1, 2, 3, 4, "img_1.jpg", null, SourceStorageKey: key1)
            ],
            ImageOutcomes:
            [
                new ImageInferenceOutcome(key1, "SUCCESS"),
                new ImageInferenceOutcome(key2, "SUCCESS")
            ]);

        Assert.True(MissionDiseaseJobProcessor.HasCompleteCoverage(result, expected));
    }

    [Theory]
    [MemberData(nameof(IncompleteCoverageCases))]
    public void HasCompleteCoverageReturnsFalseWhenCoverageIsIncomplete(
        ImageInferenceOutcome[]? outcomes,
        string? detectionSourceKey)
    {
        const string key1 = "tenants/t/media/m/1.jpg";
        const string key2 = "tenants/t/media/m/2.jpg";
        var expected = new HashSet<string>(StringComparer.Ordinal) { key1, key2 };
        var result = new DetectMissionDiseasesResult(
            IsSuccess: true,
            MissionId: Guid.NewGuid(),
            ModelName: "best_disease.pt",
            TotalImagesScanned: 2,
            TotalDetections: 1,
            DiseaseSummary: new Dictionary<string, int> { ["LeafSpot"] = 1 },
            CsvDownloadUrl: null,
            Detections:
            [
                new DiseaseDetectionItem("1", 0, "LeafSpot", 10.1, 106.1, 0.88, 1, 2, 3, 4, "img_1.jpg", null, SourceStorageKey: detectionSourceKey)
            ],
            ImageOutcomes: outcomes);

        Assert.False(MissionDiseaseJobProcessor.HasCompleteCoverage(result, expected));
    }

    public static IEnumerable<object?[]> IncompleteCoverageCases()
    {
        const string key1 = "tenants/t/media/m/1.jpg";
        const string key2 = "tenants/t/media/m/2.jpg";

        yield return [null, key1];
        yield return [new[] { new ImageInferenceOutcome(key1, "SUCCESS") }, key1];
        yield return [new[] { new ImageInferenceOutcome(key1, "SUCCESS"), new ImageInferenceOutcome(key2, "FAILED", "decode error") }, key1];
        yield return [new[] { new ImageInferenceOutcome(key1, "SUCCESS"), new ImageInferenceOutcome(key2, "SKIPPED") }, key1];
        yield return [new[] { new ImageInferenceOutcome(key1, "SUCCESS"), new ImageInferenceOutcome(key1, "SUCCESS") }, key1];
        yield return [new[] { new ImageInferenceOutcome(key1, "SUCCESS"), new ImageInferenceOutcome(key2, "SUCCESS") }, null];
        yield return [new[] { new ImageInferenceOutcome(key1, "SUCCESS"), new ImageInferenceOutcome(key2, "SUCCESS") }, "tenants/t/media/m/other.jpg"];
    }

    [Fact]
    public async Task ExecuteJobAsyncInvalidInputJsonFailsWithoutCallingDetector()
    {
        using var fixture = new Fixture();
        var job = fixture.CreateJob("{not-valid-json");

        var (handled, error) = await fixture.Processor.ExecuteJobAsync(job, Guid.NewGuid(), CancellationToken.None);

        Assert.False(handled);
        Assert.NotNull(error);
        Assert.StartsWith("DetectDiseases.InvalidInput:", error, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Metashape.DetectDiseaseCalls);
    }

    [Fact]
    public async Task ExecuteJobAsyncMissingMissionPropagatesNotFoundCode()
    {
        using var fixture = new Fixture { MissionToReturn = null };
        var job = fixture.CreateJob(JsonSerializer.Serialize(fixture.DefaultInput()));

        var (handled, error) = await fixture.Processor.ExecuteJobAsync(job, Guid.NewGuid(), CancellationToken.None);

        Assert.False(handled);
        Assert.NotNull(error);
        Assert.StartsWith($"{MissionError.NotFound(fixture.MissionIdValue).Code}:", error, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Metashape.DetectDiseaseCalls);
    }

    [Fact]
    public async Task ExecuteJobAsyncMissionWithoutOrderPropagatesOrderRequiredCode()
    {
        using var fixture = new Fixture();
        fixture.MissionToReturn = CreateStandaloneMission(
            fixture.TenantIdValue,
            fixture.FarmIdValue,
            fixture.ActorIdValue!.Value);
        var job = fixture.CreateJob(JsonSerializer.Serialize(fixture.DefaultInput()));

        var (handled, error) = await fixture.Processor.ExecuteJobAsync(job, Guid.NewGuid(), CancellationToken.None);

        Assert.False(handled);
        Assert.NotNull(error);
        Assert.StartsWith("DetectDiseases.OrderRequired:", error, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Metashape.DetectDiseaseCalls);
    }

    [Fact]
    public async Task ExecuteJobAsyncUnsupportedMissionPurposePropagatesInvalidPurposeCode()
    {
        using var fixture = new Fixture();
        fixture.MissionToReturn = CreateOrderBoundMission(
            fixture.TenantIdValue,
            fixture.FarmIdValue,
            Guid.NewGuid(),
            MissionPurpose.BaselineMapping,
            fixture.ActorIdValue!.Value);
        var job = fixture.CreateJob(JsonSerializer.Serialize(fixture.DefaultInput()));

        var (handled, error) = await fixture.Processor.ExecuteJobAsync(job, Guid.NewGuid(), CancellationToken.None);

        Assert.False(handled);
        Assert.NotNull(error);
        Assert.StartsWith("DetectDiseases.InvalidPurpose:", error, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Metashape.DetectDiseaseCalls);
    }

    private static DroneMission CreateStandaloneMission(
        Guid tenantId,
        Guid farmId,
        Guid actorId)
    {
        using var parameters = JsonDocument.Parse("{}");
        return DroneMission.Create(
            tenantId,
            farmId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            $"MSN-{Guid.NewGuid():N}"[..16],
            MissionType.Mapping,
            null,
            parameters,
            null,
            actorId,
            Now);
    }

    private static DroneMission CreateOrderBoundMission(
        Guid tenantId,
        Guid farmId,
        Guid surveyOrderId,
        MissionPurpose purpose,
        Guid actorId)
    {
        using var parameters = JsonDocument.Parse("{}");
        return DroneMission.CreateOrderBound(
            surveyOrderId,
            tenantId,
            farmId,
            [Guid.NewGuid()],
            Guid.NewGuid(),
            Guid.NewGuid(),
            actorId,
            $"MSN-{Guid.NewGuid():N}"[..16],
            purpose,
            Guid.NewGuid(),
            null,
            requiresBaselineCompletion: false,
            parameters,
            actorId,
            Now);
    }

    private sealed class Fixture : IExecutionContext, ISystemManagerAccessService, IDroneMissionRepository, IObjectStorage, IDisposable
    {
        private readonly MissionsDbContext _dbContext;
        public readonly MissionImageJobStore JobStore;
        public readonly FakeMetashapeService Metashape = new();
        public readonly MissionDiseaseJobProcessor Processor;
        public readonly DetectMissionDiseasesCommandHandler Handler;

        public Guid TenantIdValue { get; } = Guid.NewGuid();
        public Guid FarmIdValue { get; } = Guid.NewGuid();
        public Guid MissionIdValue { get; } = Guid.NewGuid();
        public Guid? ActorIdValue { get; set; } = Guid.NewGuid();
        public bool AllowFarmAccess { get; set; } = true;
        public DroneMission? MissionToReturn { get; set; }

        public Guid? TenantId => TenantIdValue;
        public Guid? ActorId => ActorIdValue;
        public bool IsInitialized => true;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => default;

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<MissionsDbContext>()
                .UseInMemoryDatabase($"disease-job-tests-{Guid.NewGuid():N}")
                .Options;
            _dbContext = new MissionsDbContext(options);
            JobStore = new MissionImageJobStore(_dbContext);
            Processor = new MissionDiseaseJobProcessor(
                this,
                _dbContext,
                JobStore,
                Metashape,
                this,
                TimeProvider.System,
                NullLogger<MissionDiseaseJobProcessor>.Instance);
            Handler = new DetectMissionDiseasesCommandHandler(
                _dbContext,
                JobStore,
                Processor,
                this,
                this,
                TimeProvider.System);
        }

        public DetectMissionDiseasesCommand DefaultCommand() => new(
            TenantIdValue,
            FarmIdValue,
            MissionIdValue,
            ConfidenceThreshold: 0.25,
            MatchingToleranceMeters: 2.5,
            ZoneRadiusMeters: 15.0,
            MinDiseasedTrees: 3);

        public MissionDiseaseInput DefaultInput() => new(
            ActorIdValue ?? Guid.NewGuid(),
            0.25,
            null,
            2.5,
            15.0,
            3);

        public MissionImageJobStore.Job CreateJob(string inputJson) => new(
            Guid.NewGuid(),
            MissionIdValue,
            TenantIdValue,
            FarmIdValue,
            MissionImageJobStore.Disease,
            "RUNNING",
            inputJson,
            null,
            null,
            Now,
            Now,
            null);

        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AllowFarmAccess && farmId == FarmIdValue
                ? SystemManagerFarmAccess.Allowed(TenantIdValue, FarmIdValue, Guid.NewGuid())
                : SystemManagerFarmAccess.Denied("Farm access denied."));

        public Task<DroneMission?> GetByIdAsync(Guid missionId, Guid tenantId, Guid farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(MissionToReturn);

        public Task<bool> CodeExistsAsync(Guid farmId, string missionCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public void Add(DroneMission mission)
        {
        }

        public Task<ObjectUploadSession> CreateUploadSessionAsync(ObjectUploadRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<StoredObjectInfo?> GetInfoAsync(string storageUri, CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredObjectInfo?>(null);

        public Task ReadAsync(string storageUri, Func<Stream, CancellationToken, Task> reader, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Uri> CreateDownloadUriAsync(string storageUri, TimeSpan lifetime, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageUri, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Dispose() => _dbContext.Dispose();
    }

    private sealed class FakeMetashapeService : IMetashapeService
    {
        public int DetectDiseaseCalls { get; private set; }

        public Task<MetashapeTaskCreatedResult> CreateTaskAsync(Guid missionId, Guid tenantId, Guid farmId, IEnumerable<MetashapeTaskImageItem> images, MetashapeTaskOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MetashapeTaskCreatedResult> CreateTaskAsync(Guid missionId, Guid tenantId, Guid farmId, IEnumerable<MetashapeTaskImageItem> images, MetashapeTaskOptions? options, string engine, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MetashapeTaskStatusResult> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CancelTaskAsync(string taskId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SliceTilesResult> SliceTilesAsync(Guid missionId, Guid tenantId, string? tifStorageKey = null, int tileSize = 640, int overlap = 64, bool dualScale = true, object? polygonGeoJson = null, bool uploadToMinio = false, Guid? tilingJobId = null, Guid? tilingRunId = null, string? sourceSha256 = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DetectMissionTreesResult> DetectMissionTreesAsync(Guid missionId, Guid tenantId, double confidenceThreshold = 0.25, double rowSpacing = 2.8, double colSpacing = 2.5, object? polygonGeoJson = null, double? anchorLat = null, double? anchorLon = null, double? customAngleDeg = null, string? tileManifestStorageKey = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DetectMissionTreesResult> RecalculateTreeGridAsync(Guid missionId, IReadOnlyList<TreeDetectionItem> activeTrees, double rowSpacing = 2.8, double colSpacing = 2.5, object? polygonGeoJson = null, double? anchorLat = null, double? anchorLon = null, double? customAngleDeg = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(byte[] Bytes, string FileName)?> DownloadTreesCsvAsync(Guid missionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DetectMissionDiseasesResult> DetectMissionDiseasesAsync(
            Guid missionId,
            Guid tenantId,
            IEnumerable<MetashapeTaskImageItem>? images = null,
            double confidenceThreshold = 0.25,
            object? polygonGeoJson = null,
            CancellationToken cancellationToken = default)
        {
            DetectDiseaseCalls++;
            throw new NotSupportedException();
        }

        public Task<(byte[] Bytes, string FileName)?> DownloadDiseasesCsvAsync(Guid missionId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
