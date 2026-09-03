using System.Text.RegularExpressions;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace AgriDrone.Integrations.Media;

internal sealed class MinioObjectStorage(
    IMinioClient minioClient,
    IOptions<MinioStorageOptions> options,
    TimeProvider timeProvider)
    : IObjectStorage
{
    private const string StorageUriScheme = "minio";
    private const string DefaultMimeType =
        "application/octet-stream";

    private readonly MinioStorageOptions _options =
        options.Value;

    public async Task<ObjectUploadSession>
        CreateUploadSessionAsync(
            ObjectUploadRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var safeObjectName =
            SanitizeObjectName(request.ObjectName);

        var objectName =
            $"tenants/{request.TenantId:D}/" +
            $"media/{request.MediaAssetId:D}/" +
            safeObjectName;

        var expiresAt = timeProvider.GetUtcNow()
            .AddMinutes(
                _options.PresignedUrlExpiryMinutes);

        var expirySeconds = checked(
            _options.PresignedUrlExpiryMinutes * 60);

        var args = new PresignedPutObjectArgs()
            .WithBucket(_options.Bucket)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        var uploadUrl =
            await minioClient.PresignedPutObjectAsync(args);

        return new ObjectUploadSession(
            BuildStorageUri(objectName),
            new Uri(uploadUrl),
            expiresAt);
    }

    public async Task<StoredObjectInfo?> GetInfoAsync(
        string storageUri,
        CancellationToken cancellationToken = default)
    {
        var location = ParseStorageUri(storageUri);

        try
        {
            var args = new StatObjectArgs()
                .WithBucket(location.Bucket)
                .WithObject(location.ObjectName);

            var stat = await minioClient.StatObjectAsync(
                args,
                cancellationToken);

            return new StoredObjectInfo(
                storageUri,
                string.IsNullOrWhiteSpace(stat.ContentType)
                    ? DefaultMimeType
                    : stat.ContentType,
                stat.Size,
                ChecksumAlgorithm: null,
                Checksum: null);
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task ReadAsync(
        string storageUri,
        Func<Stream, CancellationToken, Task> reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var location = ParseStorageUri(storageUri);

        var args = new GetObjectArgs()
            .WithBucket(location.Bucket)
            .WithObject(location.ObjectName)
            .WithCallbackStream(reader);

        await minioClient.GetObjectAsync(
            args,
            cancellationToken);
    }

    public async Task<Uri> CreateDownloadUriAsync(
        string storageUri,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var expirySecondsDouble =
            lifetime.TotalSeconds;

        if (expirySecondsDouble < 1 ||
            expirySecondsDouble > 604_800)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "Download URL lifetime must be between " +
                "1 second and 7 days.");
        }

        var location = ParseStorageUri(storageUri);

        var args = new PresignedGetObjectArgs()
            .WithBucket(location.Bucket)
            .WithObject(location.ObjectName)
            .WithExpiry(
                checked((int)expirySecondsDouble));

        var downloadUrl =
            await minioClient.PresignedGetObjectAsync(args);

        return new Uri(downloadUrl);
    }

    public async Task DeleteAsync(
        string storageUri,
        CancellationToken cancellationToken = default)
    {
        var location = ParseStorageUri(storageUri);

        var args = new RemoveObjectArgs()
            .WithBucket(location.Bucket)
            .WithObject(location.ObjectName);

        await minioClient.RemoveObjectAsync(
            args,
            cancellationToken);
    }

    private string BuildStorageUri(
        string objectName)
    {
        return $"{StorageUriScheme}://" +
               $"{_options.Bucket}/{objectName}";
    }

    private StorageLocation ParseStorageUri(
        string storageUri)
    {
        if (!Uri.TryCreate(
                storageUri,
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                StorageUriScheme,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Storage URI must use the minio scheme.",
                nameof(storageUri));
        }

        if (!string.Equals(
                uri.Host,
                _options.Bucket,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Storage URI belongs to a different bucket.");
        }

        var objectName = Uri.UnescapeDataString(
            uri.AbsolutePath.TrimStart('/'));

        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException(
                "Storage URI must contain an object name.",
                nameof(storageUri));
        }

        return new StorageLocation(
            uri.Host,
            objectName);
    }

    private static string SanitizeObjectName(
        string objectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            objectName);

        var fileName = Path.GetFileName(
            objectName.Trim());

        var sanitized = Regex.Replace(
            fileName,
            @"[^A-Za-z0-9._-]",
            "_");

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException(
                "Object name is invalid.",
                nameof(objectName));
        }

        return sanitized;
    }

    private sealed record StorageLocation(
        string Bucket,
        string ObjectName);
}