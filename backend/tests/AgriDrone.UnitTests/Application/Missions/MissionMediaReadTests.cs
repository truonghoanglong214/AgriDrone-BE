using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMedia;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDetails;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDownloadUrl;
using AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Queries;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class MissionMediaReadTests
{
    [Theory]
    [InlineData("tenant")]
    [InlineData("farm")]
    [InlineData("mission")]
    [InlineData("media")]
    [InlineData("asset-tenant")]
    [InlineData("asset-farm")]
    public async Task WrongScopeCannotGetDetailsOrSignDownload(string mismatch)
    {
        var fixture = new Fixture();
        var farmId = fixture.FarmId;
        var missionId = fixture.Mission.Id;
        var mediaId = fixture.Asset.Id;
        switch (mismatch)
        {
            case "tenant": fixture.TenantId = Guid.NewGuid(); break;
            case "farm": farmId = Guid.NewGuid(); break;
            case "mission": missionId = Guid.NewGuid(); break;
            case "media": mediaId = Guid.NewGuid(); break;
            case "asset-tenant": Set(fixture.Asset, nameof(MediaAsset.TenantId), Guid.NewGuid()); break;
            case "asset-farm": Set(fixture.Asset, nameof(MediaAsset.FarmId), Guid.NewGuid()); break;
        }

        var details = await new GetMissionMediaDetailsQueryHandler(fixture, fixture)
            .Handle(new(farmId, missionId, mediaId), default);
        var download = await fixture.Download(farmId, missionId, mediaId);
        Assert.True(details.IsFailure);
        Assert.True(download.IsFailure);
        Assert.Equal(0, fixture.InfoCalls);
        Assert.Equal(0, fixture.SignCalls);
    }

    [Theory]
    [InlineData(MediaStorageStatus.Archived)]
    [InlineData(MediaStorageStatus.DeletePending)]
    [InlineData(MediaStorageStatus.Deleted)]
    [InlineData(MediaStorageStatus.DeleteFailed)]
    public async Task InactiveMediaIsExcludedFromListDetailsAndDownload(MediaStorageStatus status)
    {
        var fixture = new Fixture();
        Set(fixture.Asset, nameof(MediaAsset.StorageStatus), status);

        var list = await fixture.List();
        var details = await new GetMissionMediaDetailsQueryHandler(fixture, fixture)
            .Handle(new(fixture.FarmId, fixture.Mission.Id, fixture.Asset.Id), default);
        var download = await fixture.Download();
        Assert.True(list.IsSuccess);
        Assert.Empty(list.Value.Items);
        Assert.True(details.IsFailure);
        Assert.True(download.IsFailure);
        Assert.Equal(0, fixture.SignCalls);
    }

    [Fact]
    public async Task DeletionRequestedBlocksDownloadEvenIfStatusIsStillActive()
    {
        var fixture = new Fixture();
        Set(fixture.Asset, nameof(MediaAsset.DeletionRequestedAt), fixture.Now);
        Assert.True((await fixture.Download()).IsFailure);
        Assert.Equal(0, fixture.SignCalls);
    }

    [Fact]
    public async Task MissingTenantDoesNotQueryStorage()
    {
        var fixture = new Fixture { TenantId = null };
        Assert.True((await fixture.Download()).IsFailure);
        Assert.True((await fixture.List()).IsFailure);
        Assert.Equal(0, fixture.InfoCalls);
    }

    [Fact]
    public async Task MissingObjectDoesNotGetSignedUrl()
    {
        var fixture = new Fixture { ObjectExists = false };
        var result = await fixture.Download();
        Assert.True(result.IsFailure);
        Assert.Equal("MissionMedia.NotFound", result.Error.Code);
        Assert.Equal(1, fixture.InfoCalls);
        Assert.Equal(0, fixture.SignCalls);
    }

    [Fact]
    public async Task ActiveObjectGetsFiveMinuteUrl()
    {
        var fixture = new Fixture();
        var result = await fixture.Download();
        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.Asset.Id, result.Value.MediaId);
        Assert.Equal(TimeSpan.FromMinutes(5), fixture.SignedLifetime);
        Assert.Equal(fixture.Now.AddMinutes(5), result.Value.ExpiresAt);
        Assert.Equal("https://storage.example.test/signed", result.Value.DownloadUrl);
        Assert.Equal(1, fixture.SignCalls);
    }

    [Fact]
    public async Task EmptyMissionReturnsEmptyPageButUnknownMissionReturnsNotFound()
    {
        var fixture = new Fixture();
        fixture.Items.Clear();
        var empty = await fixture.List();
        var missing = await new GetMissionMediaQueryHandler(fixture, fixture)
            .Handle(new(fixture.FarmId, Guid.NewGuid(), 1, 20, null, null), default);
        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Value.Items);
        Assert.Equal(0, empty.Value.TotalCount);
        Assert.True(missing.IsFailure);
        Assert.Equal("Mission.NotFound", missing.Error.Code);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void UnsafePaginationIsRejectedForBothLists(int page, int size)
    {
        Assert.False(new GetMissionsQueryValidator().Validate(
            new GetMissionsQuery(Guid.NewGuid(), page, size, null, null, null, null, null)).IsValid);
        Assert.False(new GetMissionMediaQueryValidator().Validate(
            new GetMissionMediaQuery(Guid.NewGuid(), Guid.NewGuid(), page, size, null, null)).IsValid);
    }

    [Fact]
    public void InvalidFiltersAreRejected()
    {
        var mission = new GetMissionsQuery(Guid.NewGuid(), 1, 20, null, null, null, null, null);
        Assert.False(new GetMissionsQueryValidator().Validate(
            mission with { MissionType = (MissionType)999 }).IsValid);
        Assert.False(new GetMissionsQueryValidator().Validate(
            mission with { ZoneId = Guid.Empty }).IsValid);
        Assert.False(new GetMissionsQueryValidator().Validate(
            mission with { DroneId = Guid.Empty }).IsValid);
        Assert.True(new GetMissionsQueryValidator().Validate(mission).IsValid);

        var media = new GetMissionMediaQuery(Guid.NewGuid(), Guid.NewGuid(), 1, 20, null, null);
        Assert.False(new GetMissionMediaQueryValidator().Validate(
            media with { MediaType = (MediaType)999 }).IsValid);
    }

    private static void Set(object target, string property, object value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Fixture : IMissionMediaQueries, IObjectStorage, IExecutionContext
    {
        public DateTimeOffset Now { get; } = new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);
        public Guid? TenantId { get; set; } = Guid.NewGuid();
        public Guid? ActorId { get; } = Guid.NewGuid();
        public Guid FarmId { get; } = Guid.NewGuid();
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public bool IsInitialized => true;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
        public DroneMission Mission { get; }
        public MediaAsset Asset { get; }
        public List<MissionMedia> Items { get; } = [];
        public bool ObjectExists { get; set; } = true;
        public int InfoCalls { get; private set; }
        public int SignCalls { get; private set; }
        public TimeSpan SignedLifetime { get; private set; }

        public Fixture()
        {
            using var parameters = JsonDocument.Parse("{}");
            Mission = DroneMission.Create(TenantId!.Value, FarmId, Guid.NewGuid(),
                Guid.NewGuid(), null, "READ-TEST", MissionType.Mapping, null,
                parameters, null, ActorId!.Value, Now);
            Asset = MediaAsset.Create(Guid.NewGuid(), TenantId.Value, FarmId,
                "MinIO", "private/key", "s3://private-bucket/key", MediaType.Image,
                "image/png", 100, new string('a', 64), ActorId.Value, Now);
            var item = MissionMedia.Create(Mission.Id, Asset.Id, MissionMediaRole.RawImage, Now);
            Set(item, nameof(MissionMedia.Mission), Mission);
            Set(item, nameof(MissionMedia.Media), Asset);
            Items.Add(item);
        }

        public Task<AgriDrone.SharedKernel.Application.Result<MissionMediaDownloadResponse>> Download(
            Guid? farmId = null, Guid? missionId = null, Guid? mediaId = null) =>
            new GetMissionMediaDownloadUrlQueryHandler(this, this, this, new FixedTimeProvider(Now))
                .Handle(new(farmId ?? FarmId, missionId ?? Mission.Id, mediaId ?? Asset.Id), default);

        public Task<AgriDrone.SharedKernel.Application.Result<PagedResult<MissionMediaResponse>>> List() =>
            new GetMissionMediaQueryHandler(this, this)
                .Handle(new(FarmId, Mission.Id, 1, 20, null, null), default);

        public Task<bool> MissionExistsAsync(
            Guid tenantId, Guid farmId, Guid missionId, CancellationToken cancellationToken) =>
            Task.FromResult(Mission.TenantId == tenantId && Mission.FarmId == farmId && Mission.Id == missionId);

        private IQueryable<MissionMedia> Scope(Guid tenantId, Guid farmId, Guid missionId) =>
            MissionMediaQueries.VisibleMedia(Items.AsQueryable(), tenantId, farmId, missionId);

        private static MissionMediaResponse Project(MissionMedia item) =>
            new(item.MediaId, item.MissionId, item.Media.MediaType, item.MediaRole,
                item.Media.MimeType, item.Media.FileSizeBytes, item.Media.Checksum,
                item.Media.WidthPx, item.Media.HeightPx, item.Media.DurationMs,
                item.CapturedAt, item.TelemetryTimeOffsetMs, item.CaptureClockSource, item.CreatedAt);

        public Task<PagedResult<MissionMediaResponse>> GetPageAsync(
            Guid tenantId, Guid farmId, Guid missionId, PagedRequest page,
            MediaType? mediaType, MissionMediaRole? mediaRole, CancellationToken cancellationToken)
        {
            var items = Scope(tenantId, farmId, missionId).Select(Project).ToArray();
            return Task.FromResult(new PagedResult<MissionMediaResponse>(
                items, page.PageNumber, page.PageSize, items.Length));
        }

        public Task<MissionMediaResponse?> GetDetailsAsync(
            Guid tenantId, Guid farmId, Guid missionId, Guid mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(Scope(tenantId, farmId, missionId).Where(item => item.MediaId == mediaId)
                .Select(Project).SingleOrDefault());

        public Task<string?> GetDownloadSourceAsync(
            Guid tenantId, Guid farmId, Guid missionId, Guid mediaId, CancellationToken cancellationToken) =>
            Task.FromResult(Scope(tenantId, farmId, missionId).Where(item => item.MediaId == mediaId)
                .Select(item => (string?)item.Media.Url).SingleOrDefault());

        public Task<StoredObjectInfo?> GetInfoAsync(string storageUri, CancellationToken cancellationToken = default)
        {
            InfoCalls++;
            return Task.FromResult(ObjectExists
                ? new StoredObjectInfo(storageUri, "MinIO", "private/key", "image/png", 100, "SHA256", null)
                : null);
        }

        public Task<Uri> CreateDownloadUriAsync(
            string storageUri, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            SignCalls++;
            SignedLifetime = lifetime;
            return Task.FromResult(new Uri("https://storage.example.test/signed"));
        }

        public Task<ObjectUploadSession> CreateUploadSessionAsync(
            ObjectUploadRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task ReadAsync(string storageUri, Func<Stream, CancellationToken, Task> reader,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageUri, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
