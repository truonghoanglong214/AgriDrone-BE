using AgriDrone.Modules.Missions.Application.Abstractions.Media;

namespace AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;

internal static class MultipartUploadPolicy
{
    public const long PartSizeBytes = 8L * 1024 * 1024;

    public static int PartCount(long fileSizeBytes) =>
        checked((int)((fileSizeBytes + PartSizeBytes - 1) / PartSizeBytes));

    public static bool HasExpectedParts(long fileSizeBytes,
        IReadOnlyList<MultipartUploadedPart> parts)
    {
        var count = PartCount(fileSizeBytes);
        if (parts.Count != count)
            return false;
        for (var index = 0; index < count; index++)
        {
            var expectedSize = index == count - 1
                ? fileSizeBytes - (long)index * PartSizeBytes
                : PartSizeBytes;
            if (parts[index].Number != index + 1 ||
                parts[index].SizeBytes != expectedSize ||
                string.IsNullOrWhiteSpace(parts[index].ETag))
                return false;
        }
        return true;
    }
}
