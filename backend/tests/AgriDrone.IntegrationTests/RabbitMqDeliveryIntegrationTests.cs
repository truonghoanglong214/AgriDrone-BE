using System.Text;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class RabbitMqDeliveryIntegrationTests
{
    [Fact]
    public async Task RetryIsRedeliveredAndAcknowledgedOnlyAfterCommit()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var harness = await RabbitHarness.StartAsync(
            suffix,
            ProbeMode.RetryThenCommit);
        var body = Encoding.UTF8.GetBytes("retry-body");

        await harness.PublishAsync(body, Guid.NewGuid(), Guid.NewGuid());
        await harness.Probe.SecondDelivery.Task.WaitAsync(
            TimeSpan.FromSeconds(15));

        Assert.Equal(2, harness.Probe.CallCount);
        Assert.Equal(0, harness.Probe.CommitCount);
        Assert.Null(await harness.GetAsync(harness.QueueName));

        harness.Probe.AllowCommit.TrySetResult();
        await WaitUntilAsync(
            () => harness.Probe.CommitCount == 1,
            TimeSpan.FromSeconds(10));

        Assert.Equal(1, harness.Probe.CommitCount);
        Assert.Null(await harness.GetAsync(harness.QueueName));
        Assert.Null(await harness.GetAsync(harness.DeadLetterQueueName));
    }

    [Fact]
    public async Task PermanentFailurePreservesMessageInDeadLetterQueue()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var harness = await RabbitHarness.StartAsync(
            suffix,
            ProbeMode.PermanentFailure);
        var body = Encoding.UTF8.GetBytes("permanent-body");
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await harness.PublishAsync(body, messageId, correlationId);
        var delivery = await WaitForMessageAsync(
            harness,
            harness.DeadLetterQueueName,
            TimeSpan.FromSeconds(10));

        Assert.Equal(body, delivery.Body.ToArray());
        Assert.Equal(messageId.ToString("D"), delivery.BasicProperties.MessageId);
        Assert.Equal(
            correlationId.ToString("D"),
            delivery.BasicProperties.CorrelationId);
        Assert.Equal(
            "STEP1_PERMANENT",
            ReadHeader(delivery.BasicProperties.Headers, "x-agridrone-error-code"));
        Assert.Equal(
            harness.Exchange,
            ReadHeader(delivery.BasicProperties.Headers, "x-agridrone-original-exchange"));
        Assert.Equal(
            harness.RoutingKey,
            ReadHeader(delivery.BasicProperties.Headers, "x-agridrone-original-routing-key"));
        Assert.False(string.IsNullOrWhiteSpace(
            ReadHeader(delivery.BasicProperties.Headers, "x-agridrone-failed-at")));
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(
        RabbitHarness harness,
        string queue,
        TimeSpan timeout)
    {
        BasicGetResult? delivery = null;
        await WaitUntilAsync(
            async () =>
            {
                delivery = await harness.GetAsync(queue);
                return delivery is not null;
            },
            timeout);
        return Assert.IsType<BasicGetResult>(delivery);
    }

    private static string? ReadHeader(
        IDictionary<string, object?>? headers,
        string name)
    {
        if (headers is null || !headers.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> bytes => Encoding.UTF8.GetString(bytes.Span),
            _ => value?.ToString()
        };
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(50);
        }
    }

    private static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(50);
        }
    }

    private enum ProbeMode
    {
        RetryThenCommit,
        PermanentFailure
    }

    private sealed class DeliveryProbe(ProbeMode mode)
    {
        private int _callCount;
        private int _commitCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public int CommitCount => Volatile.Read(ref _commitCount);

        public TaskCompletionSource SecondDelivery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowCommit { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<IntegrationMessageProcessingResult> ProcessAsync(
            CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (mode == ProbeMode.PermanentFailure)
            {
                return IntegrationMessageProcessingResult.DeadLetter(
                    "STEP1_PERMANENT",
                    "permanent test failure");
            }

            if (call == 1)
            {
                return IntegrationMessageProcessingResult.Retry(
                    "transient test failure");
            }

            SecondDelivery.TrySetResult();
            await AllowCommit.Task.WaitAsync(cancellationToken);
            Interlocked.Increment(ref _commitCount);
            return IntegrationMessageProcessingResult.Acknowledge();
        }
    }

    private sealed class ProbeProcessor(DeliveryProbe probe)
        : IIntegrationMessageProcessor
    {
        public Task<IntegrationMessageProcessingResult> ProcessAsync(
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken) =>
            probe.ProcessAsync(cancellationToken);
    }

    private sealed class RabbitHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IHostedService[] _hostedServices;
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        private RabbitHarness(
            ServiceProvider provider,
            IHostedService[] hostedServices,
            IConnection connection,
            IChannel channel,
            DeliveryProbe probe,
            string exchange,
            string queueName,
            string routingKey)
        {
            _provider = provider;
            _hostedServices = hostedServices;
            _connection = connection;
            _channel = channel;
            Probe = probe;
            Exchange = exchange;
            QueueName = queueName;
            RoutingKey = routingKey;
        }

        public DeliveryProbe Probe { get; }

        public string Exchange { get; }

        public string QueueName { get; }

        public string DeadLetterQueueName => $"{QueueName}.dead";

        public string RoutingKey { get; }

        public static async Task<RabbitHarness> StartAsync(
            string suffix,
            ProbeMode mode)
        {
            var consumerName = $"step1-{suffix}";
            var queueName = $"agridrone.step1.{suffix}";
            var routingKey = $"step1.{suffix}";
            var exchange = $"agridrone.step1.{suffix}.exchange";
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=127.0.0.1;Port=55432;Database=agridrone_step1_inbox;Username=agridrone_test;Password=agridrone_test",
                ["RabbitMq:Enabled"] = "true",
                ["RabbitMq:HostName"] = "127.0.0.1",
                ["RabbitMq:Port"] = "55672",
                ["RabbitMq:VirtualHost"] = "agridrone_step1",
                ["RabbitMq:UserName"] = "agridrone_test",
                ["RabbitMq:Password"] = "agridrone_test",
                ["RabbitMq:ConnectionName"] = $"step1-tests-{suffix}",
                ["RabbitMq:Exchange"] = exchange,
                ["RabbitMq:RetryExchange"] = $"{exchange}.retry",
                ["RabbitMq:DeadLetterExchange"] = $"{exchange}.dlx",
                ["RabbitMq:PrefetchCount"] = "1",
                ["RabbitMq:InitialConnectionRetrySeconds"] = "1",
                ["RabbitMq:NetworkRecoverySeconds"] = "1",
                ["RabbitMq:RetryDelaysSeconds:0"] = "1",
                ["RabbitMq:Consumers:0:Name"] = consumerName,
                ["RabbitMq:Consumers:0:QueueName"] = queueName,
                ["RabbitMq:Consumers:0:RoutingKey"] = routingKey,
                ["Messaging:Outbox:Enabled"] = "false",
                ["Messaging:Retention:Enabled"] = "false",
                ["Redis:Enabled"] = "false"
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            var probe = new DeliveryProbe(mode);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddIntegrationMessagingFoundation(configuration);
            services.AddSingleton(probe);
            services.AddIntegrationConsumer<ProbeProcessor>(consumerName);
            var provider = services.BuildServiceProvider();
            var hostedServices = provider
                .GetServices<IHostedService>()
                .ToArray();
            foreach (var service in hostedServices)
            {
                await service.StartAsync(CancellationToken.None);
            }

            await provider
                .GetRequiredService<RabbitMqTopologyReady>()
                .WaitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(15));

            var factory = new ConnectionFactory
            {
                HostName = "127.0.0.1",
                Port = 55672,
                VirtualHost = "agridrone_step1",
                UserName = "agridrone_test",
                Password = "agridrone_test"
            };
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();
            await channel.QueueDeclarePassiveAsync(queueName);
            return new RabbitHarness(
                provider,
                hostedServices,
                connection,
                channel,
                probe,
                exchange,
                queueName,
                routingKey);
        }

        public async Task PublishAsync(
            byte[] body,
            Guid messageId,
            Guid correlationId)
        {
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = messageId.ToString("D"),
                CorrelationId = correlationId.ToString("D"),
                Type = "step1.test.v1"
            };
            await _channel.BasicPublishAsync(
                Exchange,
                RoutingKey,
                mandatory: true,
                properties,
                body);
        }

        public Task<BasicGetResult?> GetAsync(string queue) =>
            _channel.BasicGetAsync(queue, autoAck: true);

        public async ValueTask DisposeAsync()
        {
            for (var index = _hostedServices.Length - 1; index >= 0; index--)
            {
                await _hostedServices[index].StopAsync(
                    CancellationToken.None);
            }

            await _channel.DisposeAsync();
            await _connection.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
