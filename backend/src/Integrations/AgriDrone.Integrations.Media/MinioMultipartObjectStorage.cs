using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.Media;

internal sealed class MinioMultipartObjectStorage : IMultipartObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly MinioStorageOptions _options;

    public MinioMultipartObjectStorage(IOptions<MinioStorageOptions> options)
    {
        _options = options.Value;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKey, _options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = _options.Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = _options.Region
            });
    }

    public async Task<string> InitiateAsync(string storageUri, string mimeType,
        CancellationToken cancellationToken = default)
    {
        var key = GetObjectKey(storageUri);
        var response = await _client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                ContentType = mimeType
            }, cancellationToken);
        return response.UploadId;
    }

    public async Task<Uri> CreatePartUploadUriAsync(string storageUri,
        string uploadId, int partNumber, TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(partNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(partNumber, 10_000);
        if (lifetime < TimeSpan.FromSeconds(1) ||
            lifetime > TimeSpan.FromDays(7))
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        var url = await _client.GetPreSignedURLAsync(
            new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = GetObjectKey(storageUri),
                UploadId = uploadId,
                PartNumber = partNumber,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.Add(lifetime),
                Protocol = new Uri(_options.Endpoint).Scheme == Uri.UriSchemeHttps
                    ? Protocol.HTTPS : Protocol.HTTP
            });
        return new Uri(url);
    }

    public async Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string storageUri, string uploadId,
        CancellationToken cancellationToken = default)
    {
        var key = GetObjectKey(storageUri);
        var parts = new List<MultipartUploadedPart>();
        string? marker = null;
        while (true)
        {
            var response = await _client.ListPartsAsync(new ListPartsRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                UploadId = uploadId,
                PartNumberMarker = marker
            }, cancellationToken);
            parts.AddRange(response.Parts.Select(part =>
                new MultipartUploadedPart(
                    part.PartNumber ?? throw new InvalidOperationException("Part number missing."),
                    part.Size ?? throw new InvalidOperationException("Part size missing."),
                    part.ETag)));
            if (response.IsTruncated != true)
                return parts;
            marker = response.NextPartNumberMarker?.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            if (marker is null)
                throw new InvalidOperationException("Multipart part listing has no continuation marker.");
        }
    }

    public async Task CompleteAsync(string storageUri, string uploadId,
        IReadOnlyList<MultipartUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        await _client.CompleteMultipartUploadAsync(
            new CompleteMultipartUploadRequest
            {
                BucketName = _options.Bucket,
                Key = GetObjectKey(storageUri),
                UploadId = uploadId,
                PartETags = parts.Select(part =>
                    new PartETag(part.Number, part.ETag)).ToList()
            }, cancellationToken);
    }

    public async Task AbortAsync(string storageUri, string uploadId,
        CancellationToken cancellationToken = default)
    {
        await _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = GetObjectKey(storageUri),
            UploadId = uploadId
        }, cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    private string GetObjectKey(string storageUri)
    {
        if (!Uri.TryCreate(storageUri, UriKind.Absolute, out var uri) ||
            uri.Scheme != "minio" || uri.Host != _options.Bucket ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Invalid storage URI.", nameof(storageUri));
        var key = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (string.IsNullOrWhiteSpace(key) || key.Contains("../", StringComparison.Ordinal))
            throw new ArgumentException("Invalid storage object key.", nameof(storageUri));
        return key;
    }
}
