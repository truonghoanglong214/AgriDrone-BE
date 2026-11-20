using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.PlantHealth;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

// Ảnh vết bệnh trên bản đồ: ký link mới mỗi lần đọc từ media đã đăng ký, không dùng link ký sẵn 7 ngày đã lưu.
public sealed class ObservationImageLinksTests
{
    private static readonly Guid Crop = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Annotated = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void MediaIdsReadsRegisteredMediaAndIgnoresStoredSignedLinks()
    {
        var ids = ObservationImageLinks.MediaIds(
            $$"""{"crop_media_id":"{{Crop}}","annotated_media_id":"{{Annotated}}","crop_image_url":"http://old-7-day-link"}""");

        Assert.Equal(new ObservationImageIds(Crop, Annotated), ids);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{oops")]
    [InlineData("[1,2]")]
    [InlineData("""{"crop_image_url":"http://old-7-day-link"}""")]
    [InlineData("""{"crop_media_id":"not-a-guid","annotated_media_id":"00000000-0000-0000-0000-000000000000"}""")]
    [InlineData("""{"crop_media_id":42,"annotated_media_id":null}""")]
    public void MediaIdsReturnsNoneForLegacyOrInvalidMetadata(string? json)
    {
        Assert.Equal(ObservationImageIds.None, ObservationImageLinks.MediaIds(json));
    }

    [Fact]
    public async Task SignAsyncCreatesShortLivedLinksAndSkipsFailures()
    {
        var storage = new SigningStorage();
        var signed = await ObservationImageLinks.SignAsync(
            new Dictionary<Guid, string>
            {
                [Crop] = "minio://agridrone-media/crop.jpg",
                [Annotated] = "minio://agridrone-media/broken.jpg",
            },
            storage, NullLogger.Instance, CancellationToken.None);

        Assert.Equal("http://minio/agridrone-media/crop.jpg?sig=1", signed[Crop]);
        Assert.False(signed.ContainsKey(Annotated));
        Assert.All(storage.Lifetimes, lifetime => Assert.Equal(TimeSpan.FromHours(1), lifetime));
        Assert.True(ObservationImageLinks.Lifetime < TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task SignAsyncDoesNotSwallowCancellation()
    {
        var storage = new SigningStorage { Cancel = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ObservationImageLinks.SignAsync(
            new Dictionary<Guid, string> { [Crop] = "minio://agridrone-media/crop.jpg" },
            storage, NullLogger.Instance, CancellationToken.None));
    }

    [Fact]
    public void ResolveReturnsNullWhenMediaWasNotSigned()
    {
        var signed = new Dictionary<Guid, string> { [Crop] = "http://fresh" };

        Assert.Equal(new ObservationImageUrls("http://fresh", null),
            ObservationImageLinks.Resolve(new ObservationImageIds(Crop, Annotated), signed));
        Assert.Equal(new ObservationImageUrls(null, null),
            ObservationImageLinks.Resolve(ObservationImageIds.None, signed));
    }

    private sealed class SigningStorage : IObjectStorage
    {
        public List<TimeSpan> Lifetimes { get; } = [];
        public bool Cancel { get; init; }

        public Task<Uri> CreateDownloadUriAsync(string storageUri, TimeSpan lifetime, CancellationToken cancellationToken = default)
        {
            if (Cancel) throw new OperationCanceledException();
            Lifetimes.Add(lifetime);
            if (storageUri.Contains("broken", StringComparison.Ordinal)) throw new InvalidOperationException("sign failed");
            return Task.FromResult(new Uri(storageUri.Replace("minio://", "http://minio/", StringComparison.Ordinal) + "?sig=1"));
        }

        public Task<ObjectUploadSession> CreateUploadSessionAsync(ObjectUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StoredObjectInfo?> GetInfoAsync(string storageUri, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReadAsync(string storageUri, Func<Stream, CancellationToken, Task> reader, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageUri, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
