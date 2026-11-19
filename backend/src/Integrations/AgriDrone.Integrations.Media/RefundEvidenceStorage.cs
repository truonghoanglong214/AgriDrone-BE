using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.SharedKernel.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.Media;

internal sealed class RefundEvidenceStorage(
    IObjectStorage storage, IObjectStorageWriter writer, IOptions<MinioStorageOptions> options)
    : IRefundEvidenceStorage
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private string Prefix(Guid tenantId, Guid caseId) =>
        $"minio://{options.Value.Bucket}/refund-evidence/{tenantId:D}/{caseId:D}/";

    public async Task<string> UploadAsync(Guid tenantId, Guid caseId, Stream content, CancellationToken cancellationToken)
    {
        // Read a bounded buffer and identify the image by its signature, not its file name.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new ArgumentException("Ảnh biên lai không được lớn hơn 5 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        var bytes = buffer.ToArray();
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!png && !jpeg)
            throw new ArgumentException("Chỉ chấp nhận ảnh PNG hoặc JPEG.");
        var reference = Prefix(tenantId, caseId) + Guid.NewGuid().ToString("N") + (png ? ".png" : ".jpg");
        buffer.Position = 0;
        await writer.UploadAsync(reference, buffer, buffer.Length, png ? "image/png" : "image/jpeg", cancellationToken);
        return reference;
    }

    public async Task<bool> ExistsAsync(Guid tenantId, Guid caseId, string reference, CancellationToken cancellationToken)
    {
        var prefix = Prefix(tenantId, caseId);
        if (!reference.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var name = reference[prefix.Length..];
        if (name.Length != 36 || !Guid.TryParseExact(name[..32], "N", out _) ||
            (name[32..] != ".png" && name[32..] != ".jpg")) return false;
        var info = await storage.GetInfoAsync(reference, cancellationToken);
        return info is { FileSizeBytes: > 0 and <= MaxBytes } &&
            info.MimeType is "image/png" or "image/jpeg";
    }

    public Task<Uri> GetDownloadUriAsync(string reference, CancellationToken cancellationToken) =>
        storage.CreateDownloadUriAsync(reference, TimeSpan.FromMinutes(options.Value.PresignedUrlExpiryMinutes), cancellationToken);
}
