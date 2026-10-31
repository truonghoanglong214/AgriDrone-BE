using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace AgriDrone.Integrations.Media;

internal sealed class SurveyAuthorizationStorageCleaner(
    IMinioClient? minioClient,
    IOptions<MinioStorageOptions> options,
    ILogger<SurveyAuthorizationStorageCleaner> logger)
    : ISurveyAuthorizationStorageCleaner
{
    private const string TargetPrefix = "evidences/orders/";

    public async Task<IReadOnlyList<string>> ListOrphanedEvidencesAsync(CancellationToken cancellationToken = default)
    {
        if (minioClient == null || !options.Value.Enabled)
        {
            return [];
        }

        var bucket = options.Value.Bucket;
        var results = new List<string>();

        var args = new ListObjectsArgs()
            .WithBucket(bucket)
            .WithPrefix(TargetPrefix)
            .WithRecursive(true);

        await foreach (var item in minioClient.ListObjectsEnumAsync(args, cancellationToken))
        {
            if (!item.IsDir && !string.IsNullOrWhiteSpace(item.Key))
            {
                results.Add(item.Key);
            }
        }

        return results;
    }

    public async Task<int> PurgeOrphanedEvidencesAsync(CancellationToken cancellationToken = default)
    {
        var items = await ListOrphanedEvidencesAsync(cancellationToken);
        if (items.Count == 0 || minioClient == null)
        {
            return 0;
        }

        var bucket = options.Value.Bucket;
        var count = 0;

        foreach (var key in items)
        {
            if (!key.StartsWith(TargetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Refusing to delete object {Key} outside target prefix {Prefix}", key, TargetPrefix);
                continue;
            }

            await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(key), cancellationToken);
            count++;
            logger.LogInformation("Purged orphaned authorization evidence from MinIO: {Key}", key);
        }

        return count;
    }
}
