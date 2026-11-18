using System.Reflection;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Application.Features.Processing;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

// Nối detection của detector với media đã đăng ký: ảnh gốc (theo khóa MinIO) + ảnh khoanh vùng / ảnh cắt.
public sealed class DiseaseDetectionEvidenceTests
{
    private static readonly Guid TenantId = Guid.Parse("0ded8e4f-8970-4ecc-b376-663858515c1c");
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid MissionId = Guid.Parse("62566855-d4eb-43aa-bf05-8ddc634ee88b");
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 0, 0, 0, TimeSpan.Zero);
    private static string MissionPrefix => $"tenants/{TenantId:D}/media/{MissionId:D}";
    private static readonly string Sha = new('a', 64);

    private static MissionMedia Raw(string fileName, DateTimeOffset? capturedAt = null)
    {
        var key = $"{MissionPrefix}/{fileName}";
        var asset = MediaAsset.Create(Guid.NewGuid(), TenantId, FarmId, "MinIO", key, $"minio://agridrone-media/{key}",
            MediaType.Image, "image/jpeg", 100, Sha, Guid.NewGuid(), Now);
        var item = MissionMedia.Create(MissionId, asset.Id, MissionMediaRole.RawImage, Now, capturedAt);
        typeof(MissionMedia).GetProperty(nameof(MissionMedia.Media))!.SetValue(item, asset);
        return item;
    }

    private static DiseaseDetectionItem Detection(string imageName, string? sourceKey = null) =>
        new("disease_0001", 0, "Disease_Brown_Spot", 10.46, 106.49, 0.9, 1, 1, 1, 1, imageName, null, SourceStorageKey: sourceKey);

    [Fact]
    public void FindsSourcePhotoByStorageKeyEvenThoughDetectorRenamedIt()
    {
        var first = Raw("AATM0852.JPG", Now.AddMinutes(-3));
        var second = Raw("AATM0853.JPG");
        var index = new DetectMissionDiseasesCommandHandler.DetectionSourceIndex([first, second]);

        // Regression: detector trả "img_0004.JPG", không khớp tên file gốc nào.
        Assert.Same(second, index.Find(Detection("img_0004_abcdef0123.JPG", second.Media.StorageKey)));
        Assert.Equal(Now.AddMinutes(-3), index.Find(Detection("img_0000.JPG", first.Media.StorageKey))!.CapturedAt);
        Assert.Null(index.Find(Detection("img_0004.JPG", $"{MissionPrefix}/missing.JPG")));
    }

    [Fact]
    public void FallsBackToFileNameOnlyWhenDetectorSentNoStorageKey()
    {
        var raw = Raw("AATM0852.JPG");
        var index = new DetectMissionDiseasesCommandHandler.DetectionSourceIndex([raw]);

        Assert.Same(raw, index.Find(Detection("aatm0852.jpg")));
        Assert.Null(index.Find(Detection("img_0004.JPG")));
    }

    [Fact]
    public void DerivedImageMustLiveUnderThisMissionsDiseaseFolderWithAValidChecksum()
    {
        var raw = Raw("AATM0852.JPG");
        var key = $"{MissionPrefix}/disease/0123456789abcdef0123456789abcdef/annotated/annotated_img_0000.jpg";

        Assert.Equal($"minio://agridrone-media/{key}", DetectMissionDiseasesCommandHandler.DerivedStorageUri(key, Sha, raw, TenantId, MissionId));
        // Thư mục khác, mission khác, path traversal, checksum sai hoặc thiếu ảnh gốc → không đăng ký.
        Assert.Null(DetectMissionDiseasesCommandHandler.DerivedStorageUri($"{MissionPrefix}/AATM0852.JPG", Sha, raw, TenantId, MissionId));
        Assert.Null(DetectMissionDiseasesCommandHandler.DerivedStorageUri(key, Sha, raw, TenantId, Guid.NewGuid()));
        Assert.Null(DetectMissionDiseasesCommandHandler.DerivedStorageUri($"{MissionPrefix}/disease/../other.jpg", Sha, raw, TenantId, MissionId));
        Assert.Null(DetectMissionDiseasesCommandHandler.DerivedStorageUri(key, "not-a-sha", raw, TenantId, MissionId));
        Assert.Null(DetectMissionDiseasesCommandHandler.DerivedStorageUri(key, new string('g', 64), raw, TenantId, MissionId));
        Assert.Null(DetectMissionDiseasesCommandHandler.DerivedStorageUri(key, Sha, null, TenantId, MissionId));
    }

    [Fact]
    public void EvidenceListsSourceAnnotatedAndCropImagesWithRoles()
    {
        var source = Guid.NewGuid();
        var annotated = Guid.NewGuid();
        var crop = Guid.NewGuid();
        using var metadata = JsonDocument.Parse(JsonSerializer.Serialize(new { annotated_media_id = annotated, crop_media_id = crop }));

        var media = MissionPlantHealthPublisher.EvidenceMedia(source, metadata.RootElement);

        Assert.Equal([(source, "source_image"), (annotated, "annotated_image"), (crop, "crop_image")],
            media.Select(m => (m.MediaAssetId, m.Role)).ToArray());
    }

    [Fact]
    public void LegacyObservationsKeepOnlyTheSourcePhotoAndIgnoreBadOrDuplicateIds()
    {
        var source = Guid.NewGuid();
        using var legacy = JsonDocument.Parse("""{"crop_image_url":"http://signed"}""");
        using var bad = JsonDocument.Parse($$"""{"annotated_media_id":"oops","crop_media_id":"{{source}}"}""");

        Assert.Equal([source], MissionPlantHealthPublisher.EvidenceMedia(source, legacy.RootElement).Select(m => m.MediaAssetId));
        Assert.Equal([source], MissionPlantHealthPublisher.EvidenceMedia(source, bad.RootElement).Select(m => m.MediaAssetId));
        Assert.Empty(MissionPlantHealthPublisher.EvidenceMedia(null, legacy.RootElement));
    }
}
