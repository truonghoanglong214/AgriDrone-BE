using System.Text.Json;
using AgriDrone.IntegrationContracts.Plants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgriDrone.SharedInfrastructure.Caching;

internal sealed partial class RedisPlantReferenceCache(
    RedisConnectionProvider connectionProvider,
    IOptions<RedisCacheOptions> options,
    ILogger<RedisPlantReferenceCache> logger) : IPlantReferenceCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web);
    private readonly RedisCacheOptions _options = options.Value;

    public async Task<IReadOnlyList<PlantReferenceV1>?> TryGetAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(tenantId, farmId, zoneId, mapVersionId);
        if (!_options.Enabled)
        {
            return null;
        }

        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            var epoch = await GetEpochAsync(
                database,
                tenantId,
                farmId,
                zoneId);
            var value = await database.StringGetAsync(
                BuildDataKey(
                    tenantId,
                    farmId,
                    zoneId,
                    mapVersionId,
                    epoch));
            return value.IsNull
                ? null
                : JsonSerializer.Deserialize<PlantReferenceV1[]>(
                    (byte[])value!,
                    SerializerOptions);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogReadFailure(logger, tenantId, farmId, zoneId, exception);
            return null;
        }
    }

    public async Task SetAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        IReadOnlyList<PlantReferenceV1> references,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(tenantId, farmId, zoneId, mapVersionId);
        ArgumentNullException.ThrowIfNull(references);
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            var epoch = await GetEpochAsync(
                database,
                tenantId,
                farmId,
                zoneId);
            var value = JsonSerializer.SerializeToUtf8Bytes(
                references,
                SerializerOptions);
            await database.StringSetAsync(
                BuildDataKey(
                    tenantId,
                    farmId,
                    zoneId,
                    mapVersionId,
                    epoch),
                value,
                TimeSpan.FromSeconds(_options.PlantReferenceTtlSeconds));
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogWriteFailure(logger, tenantId, farmId, zoneId, exception);
        }
    }

    public async Task InvalidateZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(tenantId, farmId, zoneId);
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            var key = BuildEpochKey(tenantId, farmId, zoneId);
            await database.StringIncrementAsync(key);
            await database.KeyExpireAsync(
                key,
                TimeSpan.FromSeconds(
                    _options.InvalidationEpochTtlSeconds));
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogInvalidationFailure(
                logger,
                tenantId,
                farmId,
                zoneId,
                exception);
        }
    }

    internal string GetDataKeyForDiagnostics(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        long epoch = 0) =>
        BuildDataKey(tenantId, farmId, zoneId, mapVersionId, epoch);

    private async ValueTask<IDatabase> GetDatabaseAsync(
        CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(
            cancellationToken);
        return connection.GetDatabase();
    }

    private async Task<long> GetEpochAsync(
        IDatabase database,
        Guid tenantId,
        Guid farmId,
        Guid zoneId)
    {
        var value = await database.StringGetAsync(
            BuildEpochKey(tenantId, farmId, zoneId));
        return value.TryParse(out long epoch) ? epoch : 0;
    }

    private string BuildDataKey(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        long epoch) =>
        $"{_options.InstancePrefix}:v1:tenant:{tenantId:D}:farm:{farmId:D}:zone:{zoneId:D}:plant-references:epoch:{epoch}:map:{mapVersionId:D}";

    private string BuildEpochKey(
        Guid tenantId,
        Guid farmId,
        Guid zoneId) =>
        $"{_options.InstancePrefix}:v1:{BuildEpochKeyStatic(tenantId, farmId, zoneId)}";

    private static string BuildEpochKeyStatic(
        Guid tenantId,
        Guid farmId,
        Guid zoneId) =>
        $"tenant:{tenantId:D}:farm:{farmId:D}:zone:{zoneId:D}:plant-references:epoch";

    private static void ValidateScope(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid? mapVersionId = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(zoneId, Guid.Empty);
        if (mapVersionId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(mapVersionId));
        }
    }

    private static bool IsRedisFailure(Exception exception) =>
        exception is RedisException or InvalidOperationException or
            ObjectDisposedException;

    [LoggerMessage(
        EventId = 500,
        Level = LogLevel.Warning,
        Message = "Redis Plant reference read failed for Tenant={TenantId}, Farm={FarmId}, Zone={ZoneId}; treating it as a cache miss.")]
    private static partial void LogReadFailure(
        ILogger logger,
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Exception exception);

    [LoggerMessage(
        EventId = 501,
        Level = LogLevel.Warning,
        Message = "Redis Plant reference write failed for Tenant={TenantId}, Farm={FarmId}, Zone={ZoneId}; the database result remains authoritative.")]
    private static partial void LogWriteFailure(
        ILogger logger,
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Exception exception);

    [LoggerMessage(
        EventId = 502,
        Level = LogLevel.Warning,
        Message = "Redis Plant reference invalidation failed for Tenant={TenantId}, Farm={FarmId}, Zone={ZoneId}; cached entries remain bounded by TTL.")]
    private static partial void LogInvalidationFailure(
        ILogger logger,
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Exception exception);
}
