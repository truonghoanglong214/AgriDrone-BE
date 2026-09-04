namespace AgriDrone.Integrations.Media;

public sealed class MinioStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    public int PresignedUrlExpiryMinutes { get; set; } = 15;
}