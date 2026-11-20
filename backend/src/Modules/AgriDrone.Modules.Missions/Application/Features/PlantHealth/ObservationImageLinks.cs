using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

internal sealed record ObservationImageIds(Guid? CropMediaId, Guid? AnnotatedMediaId)
{
    public static readonly ObservationImageIds None = new(null, null);
}

internal sealed record ObservationImageUrls(string? CropImageUrl, string? AnnotatedImageUrl);

/// <summary>
/// Link xem ảnh cắt / ảnh khoanh vùng của vết bệnh, ký mới mỗi lần đọc từ media đã đăng ký
/// (crop_media_id / annotated_media_id trong matching_parameters).
/// Không dùng crop_image_url / annotated_image_url cũ: đó là link ký sẵn 7 ngày, hết hạn là chết.
/// Vết bệnh cũ chưa có media id thì không có ảnh (cần chạy lại nhận diện bệnh).
/// </summary>
internal static class ObservationImageLinks
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    internal static ObservationImageIds MediaIds(string? matchingParametersJson)
    {
        if (string.IsNullOrWhiteSpace(matchingParametersJson)) return ObservationImageIds.None;
        try
        {
            using var document = JsonDocument.Parse(matchingParametersJson);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                ? new ObservationImageIds(ReadId(root, "crop_media_id"), ReadId(root, "annotated_media_id"))
                : ObservationImageIds.None;
        }
        catch (JsonException)
        {
            return ObservationImageIds.None;
        }
    }

    /// <summary>Ký link cho các media còn Active và thuộc đúng Farm. Media không tìm thấy hoặc ký lỗi thì bỏ qua.</summary>
    internal static async Task<IReadOnlyDictionary<Guid, string>> CreateAsync(
        MissionsDbContext dbContext,
        IObjectStorage storage,
        Guid farmId,
        IEnumerable<ObservationImageIds> images,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var wanted = images
            .SelectMany(image => new[] { image.CropMediaId, image.AnnotatedMediaId })
            .OfType<Guid>()
            .Distinct()
            .ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, string>();

        var sources = await dbContext.MediaAssets.AsNoTracking()
            .Where(media => wanted.Contains(media.Id)
                && media.FarmId == farmId
                && media.StorageStatus == MediaStorageStatus.Active)
            .Select(media => new { media.Id, media.Url })
            .ToDictionaryAsync(media => media.Id, media => media.Url, cancellationToken);

        return await SignAsync(sources, storage, logger, cancellationToken);
    }

    internal static async Task<IReadOnlyDictionary<Guid, string>> SignAsync(
        IReadOnlyDictionary<Guid, string> sources,
        IObjectStorage storage,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Ký tuần tự: ký link chỉ tính chữ ký tại chỗ nên song song không nhanh hơn,
        // còn khi chưa biết region của bucket thì song song sẽ dồn nhiều request hỏi region lên MinIO cùng lúc.
        var signed = new Dictionary<Guid, string>(sources.Count);
        foreach (var (mediaId, storageUri) in sources)
        {
            try
            {
                var uri = await storage.CreateDownloadUriAsync(storageUri, Lifetime, cancellationToken);
                signed[mediaId] = uri.AbsoluteUri;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Failed to sign observation image {MediaId}", mediaId);
            }
        }

        return signed;
    }

    internal static ObservationImageUrls Resolve(ObservationImageIds image, IReadOnlyDictionary<Guid, string> signed) =>
        new(Lookup(image.CropMediaId, signed), Lookup(image.AnnotatedMediaId, signed));

    private static string? Lookup(Guid? mediaId, IReadOnlyDictionary<Guid, string> signed) =>
        mediaId is Guid id && signed.TryGetValue(id, out var url) ? url : null;

    private static Guid? ReadId(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && Guid.TryParse(value.GetString(), out var id)
        && id != Guid.Empty
            ? id
            : null;
}
