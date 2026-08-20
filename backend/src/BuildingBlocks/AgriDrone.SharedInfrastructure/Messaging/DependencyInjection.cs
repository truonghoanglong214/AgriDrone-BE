using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Caching;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.RabbitMq;
using AgriDrone.SharedInfrastructure.Messaging.Retention;
using AgriDrone.SharedInfrastructure.Messaging.Recovery;
using AgriDrone.SharedInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Messaging;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationMessagingFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetRequiredAgriDroneConnectionString();

        services.AddRedisCachingFoundation(configuration);

        services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAuditWriter, AuditWriter>();
        services.AddSingleton<
            IIntegrationMessageSerializer,
            SystemTextJsonIntegrationMessageSerializer>();
        services.AddSingleton<
            IIntegrationMessageReader,
            IntegrationMessageReader>();
        services.AddSingleton<OutboxMessageFactory>();

        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<RabbitMqOptions>,
            RabbitMqOptionsValidator>();

        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<OutboxOptions>,
            OutboxOptionsValidator>();

        services.AddOptions<MessagingRetentionOptions>()
            .Bind(configuration.GetSection(
                MessagingRetentionOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<MessagingRetentionOptions>,
            MessagingRetentionOptionsValidator>();

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<RabbitMqTopologyReady>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.AddScoped<OutboxStore>();
        services.AddScoped<
            IMessagingRecoveryService,
            MessagingRecoveryService>();
        services.AddSingleton<InboxExecutionCoordinator>();

        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<RabbitMqConsumerHost>();
        services.AddHostedService<MessagingRetentionService>();

        return services;
    }

    public static IServiceCollection AddIntegrationConsumer<TProcessor>(
        this IServiceCollection services,
        string consumerName)
        where TProcessor : class, IIntegrationMessageProcessor
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        services.AddScoped<TProcessor>();
        services.AddSingleton(
            new IntegrationConsumerRegistration(
                consumerName.Trim(),
                typeof(TProcessor)));

        return services;
    }
}
