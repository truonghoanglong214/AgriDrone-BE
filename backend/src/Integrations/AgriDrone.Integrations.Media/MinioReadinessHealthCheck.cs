using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace AgriDrone.Integrations.Media;

internal sealed class MinioReadinessHealthCheck(
    IMinioClient minioClient,
    IOptions<MinioStorageOptions> options)
    : IHealthCheck
{
    private readonly MinioStorageOptions _options =
        options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new BucketExistsArgs()
                .WithBucket(_options.Bucket);

            var exists = await minioClient.BucketExistsAsync(
                args,
                cancellationToken);

            return exists
                ? HealthCheckResult.Healthy(
                    $"MinIO bucket '{_options.Bucket}' is available.")
                : HealthCheckResult.Unhealthy(
                    $"MinIO bucket '{_options.Bucket}' does not exist.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "MinIO is unavailable or its credentials are invalid.",
                exception);
        }
    }
}