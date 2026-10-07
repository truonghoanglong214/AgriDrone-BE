using AgriDrone.Database.Mapping;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AgriDrone.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddAgriDroneDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetRequiredAgriDroneConnectionString();

        services.AddSingleton(_ =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
            return dataSourceBuilder.Build();
        });

        services.AddDbContext<AgriDroneSchemaDbContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            AgriDroneSchemaDbContextOptions.Configure(options, dataSource);
        });

        return services;
    }

    public static IServiceCollection AddMappingPublicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<MappingPublicationDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql => npgsql.UseNetTopologySuite());
            });

        services.AddScoped<IMappingPublicationUnitOfWork>(serviceProvider =>
            serviceProvider.GetRequiredService<MappingPublicationDbContext>());

        services.AddScoped<
            IIntegrationMessageHandler<MappingCandidatesApprovedV1>,
            MappingCandidatesApprovedHandler>();
        services.AddIntegrationConsumer<MappingCandidatesApprovedProcessor>(
            IntegrationConsumerNames.Be1MappingCandidatesApprovedV1);

        return services;
    }

    public static IServiceCollection AddSurveyResultPublicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<SurveyResultPublicationDbContext>(
            (serviceProvider, options) =>
            {
                var dataSource =
                    serviceProvider.GetRequiredService<NpgsqlDataSource>();
                options.UseNpgsql(
                    dataSource,
                    npgsql => npgsql.UseNetTopologySuite());
            });

        services.AddScoped<ISurveyResultPublicationUnitOfWork>(
            serviceProvider => serviceProvider.GetRequiredService<
                SurveyResultPublicationDbContext>());

        return services;
    }
}
