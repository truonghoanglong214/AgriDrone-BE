namespace AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;

internal static class UploadedMediaFormat
{
    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (header.StartsWith(new byte[] { 255, 216, 255 }))
            return "image/jpeg";
        if (header.Length >= 12 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = header.Slice(8, 4);
            if (brand.SequenceEqual("qt  "u8)) return "video/quicktime";
            if (brand.SequenceEqual("isom"u8) || brand.SequenceEqual("iso2"u8) ||
                brand.SequenceEqual("mp41"u8) || brand.SequenceEqual("mp42"u8) ||
                brand.SequenceEqual("avc1"u8) || brand.SequenceEqual("M4V "u8))
                return "video/mp4";
        }
        return null;
    }

    public static string Extension(string mimeType) => mimeType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "video/mp4" => ".mp4",
        "video/quicktime" => ".mov",
        _ => throw new ArgumentOutOfRangeException(nameof(mimeType))
    };
}
