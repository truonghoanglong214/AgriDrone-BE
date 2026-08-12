using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Media;

public sealed class MediaAsset : Entity
{
    private MediaAsset()
    {
    }

    public string Provider { get; private set; } = null!;

    public string StorageKey { get; private set; } = null!;

    public string Url { get; private set; } = null!;

    public MediaType MediaType { get; private set; }

    public string? MimeType { get; private set; }

    public long? FileSizeBytes { get; private set; }

    public int? WidthPx { get; private set; }

    public int? HeightPx { get; private set; }

    public long? DurationMs { get; private set; }

    public string? Checksum { get; private set; }

    public JsonDocument Metadata { get; private set; } = null!;

    public Guid? UploadedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
