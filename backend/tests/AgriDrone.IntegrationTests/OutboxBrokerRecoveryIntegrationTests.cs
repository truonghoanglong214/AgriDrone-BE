using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class OutboxBrokerRecoveryIntegrationTests
{
    private const string DatabaseName = "agridrone_step1_outbox";
    private const string AdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=agridrone_test;Password=agridrone_test";
    private const string ConnectionString =
        "Host=127.0.0.1;Port=55432;Database=" + DatabaseName +
        ";Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task OutboxSurvivesBrokerOutageAndPublishesAfterRecovery()
    {
        await RecreateDatabaseAsync();
        var dbOptions = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using (var setup = new OutboxTestDbContext(dbOptions))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        var messageId = Guid.NewGuid();
        var body = "{\"step1\":true}"u8.ToArray();
        var now = DateTimeOffset.UtcNow;
        await using (var seed = new OutboxTestDbContext(dbOptions))
        {
            seed.OutboxMessages.Add(
                OutboxMessage.Create(
                    messageId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "step1.outbox.v1",
                    1,
                    "step1.outbox",
                    body,
                    "application/json",
                    null,
                    now,
                    now));
            await seed.SaveChangesAsync();
        }

        await using (var outage = CreateProvider(
                         rabbitPort: 1,
                         $"agridrone.step1.outbox.{messageId:N}"))
        {
            var hosted = await StartHostedServicesAsync(outage);
            await WaitUntilAsync(
                async () => await ReadStatusAsync(dbOptions, messageId) ==
                    OutboxMessageStatus.Processing,
                TimeSpan.FromSeconds(10));
            await StopHostedServicesAsync(hosted);
        }

        Assert.Equal(
            OutboxMessageStatus.Processing,
            await ReadStatusAsync(dbOptions, messageId));
        await Task.Delay(TimeSpan.FromSeconds(2));

        var exchange = $"agridrone.step1.outbox.{messageId:N}";
        var queue = $"{exchange}.queue";
        var factory = new ConnectionFactory
        {
            HostName = "127.0.0.1",
            Port = 55672,
            VirtualHost = "agridrone_step1",
            UserName = "agridrone_test",
            Password = "agridrone_test"
        };
        await using var rabbitConnection =
            await factory.CreateConnectionAsync();
        await using var rabbitChannel =
            await rabbitConnection.CreateChannelAsync();
        await rabbitChannel.ExchangeDeclareAsync(
            exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false);
        await rabbitChannel.QueueDeclareAsync(
            queue,
            durable: false,
            exclusive: true,
            autoDelete: true);
        await rabbitChannel.QueueBindAsync(
            queue,
            exchange,
            "step1.outbox");

        await using var recovery = CreateProvider(55672, exchange);
        var recoveryHosted = await StartHostedServicesAsync(recovery);
        BasicGetResult? delivery = null;
        await WaitUntilAsync(
            async () =>
            {
                delivery = await rabbitChannel.BasicGetAsync(queue, true);
                return delivery is not null;
            },
            TimeSpan.FromSeconds(15));
        await WaitUntilAsync(
            async () => await ReadStatusAsync(dbOptions, messageId) ==
                OutboxMessageStatus.Published,
            TimeSpan.FromSeconds(10));
        await StopHostedServicesAsync(recoveryHosted);

        Assert.NotNull(delivery);
        Assert.Equal(body, delivery.Body.ToArray());
        Assert.Equal(messageId.ToString("D"), delivery.BasicProperties.MessageId);
        Assert.Equal(
            OutboxMessageStatus.Published,
            await ReadStatusAsync(dbOptions, messageId));
    }

    private static ServiceProvider CreateProvider(
        int rabbitPort,
        string exchange)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] = ConnectionString,
                ["RabbitMq:Enabled"] = "true",
                ["RabbitMq:HostName"] = "127.0.0.1",
                ["RabbitMq:Port"] = rabbitPort.ToString(
                    CultureInfo.InvariantCulture),
                ["RabbitMq:VirtualHost"] = "agridrone_step1",
                ["RabbitMq:UserName"] = "agridrone_test",
                ["RabbitMq:Password"] = "agridrone_test",
                ["RabbitMq:ConnectionName"] = "step1-outbox-test",
                ["RabbitMq:Exchange"] = exchange,
                ["RabbitMq:RetryExchange"] = $"{exchange}.retry",
                ["RabbitMq:DeadLetterExchange"] = $"{exchange}.dlx",
                ["RabbitMq:PrefetchCount"] = "1",
                ["RabbitMq:InitialConnectionRetrySeconds"] = "1",
                ["RabbitMq:NetworkRecoverySeconds"] = "1",
                ["RabbitMq:RetryDelaysSeconds:0"] = "1",
                ["Messaging:Outbox:Enabled"] = "true",
                ["Messaging:Outbox:BatchSize"] = "1",
                ["Messaging:Outbox:PollIntervalMilliseconds"] = "50",
                ["Messaging:Outbox:LeaseSeconds"] = "1",
                ["Messaging:Outbox:MaximumAttempts"] = "3",
                ["Messaging:Outbox:RetryBaseSeconds"] = "1",
                ["Messaging:Outbox:RetryMaximumSeconds"] = "2",
                ["Messaging:Retention:Enabled"] = "false",
                ["Redis:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationMessagingFoundation(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<IHostedService[]> StartHostedServicesAsync(
        ServiceProvider provider)
    {
        var hosted = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hosted)
        {
            await service.StartAsync(CancellationToken.None);
        }

        return hosted;
    }

    private static async Task StopHostedServicesAsync(
        IHostedService[] hosted)
    {
        for (var index = hosted.Length - 1; index >= 0; index--)
        {
            await hosted[index].StopAsync(CancellationToken.None);
        }
    }

    private static async Task<OutboxMessageStatus> ReadStatusAsync(
        DbContextOptions<OutboxTestDbContext> options,
        Guid messageId)
    {
        await using var context = new OutboxTestDbContext(options);
        return await context.OutboxMessages
            .Where(message => message.MessageId == messageId)
            .Select(message => message.Status)
            .SingleAsync();
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

    private static async Task RecreateDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE {DatabaseName}";
        await create.ExecuteNonQueryAsync();
    }

    private sealed class OutboxTestDbContext(
        DbContextOptions<OutboxTestDbContext> options) : DbContext(options)
    {
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.ApplyConfiguration(
                new OutboxMessageConfiguration());
    }
}
