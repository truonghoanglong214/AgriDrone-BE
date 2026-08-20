using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AgriDrone.SharedInfrastructure.Caching;

internal sealed class RedisConnectionProvider(
    IOptions<RedisCacheOptions> options) : IAsyncDisposable
{
    private readonly RedisCacheOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ConnectionMultiplexer? _connection;

    public async ValueTask<IConnectionMultiplexer> GetConnectionAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException(
                "Redis is disabled by configuration.");
        }

        if (_connection is { IsConnected: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsConnected: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            var configuration = ConfigurationOptions.Parse(
                _options.ConnectionString);
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 2;
            configuration.ConnectTimeout = 2_000;
            configuration.AsyncTimeout = 2_000;
            _connection = await ConnectionMultiplexer.ConnectAsync(
                configuration);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
