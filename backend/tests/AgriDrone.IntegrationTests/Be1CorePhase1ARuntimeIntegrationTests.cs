using AgriDrone.Database;
using AgriDrone.Modules.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class Be1CorePhase1ARuntimeIntegrationTests
{
    private const string AdminConnection =
        "Host=127.0.0.1;Port=56432;Database=postgres;" +
        "Username=agridrone_test;Password=agridrone_test";

    [Fact]
    public async Task RuntimeModuleResolvesAndReadsMigratedCatalogue()
    {
        var databaseName =
            $"agridrone_be1_core_1a_{Guid.NewGuid():N}";

        try
        {
            await RecreateDatabaseAsync(databaseName);
            await using (var migrationContext =
                CreateMigrationContext(databaseName))
            {
                await migrationContext.Database.MigrateAsync();
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSurveysModule(CreateConfiguration(databaseName));

            await using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });
            await using var scope = provider.CreateAsyncScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<SurveysDbContext>();
            Assert.True(await dbContext.Database.CanConnectAsync());
            Assert.Same(
                dbContext,
                scope.ServiceProvider
                    .GetRequiredService<ISurveysUnitOfWork>());

            var catalogue = await scope.ServiceProvider
                .GetRequiredService<ISurveyCatalogueQueries>()
                .GetServicesAsync();
            Assert.Equal(
                ["HARVEST_READINESS", "PLANT_HEALTH"],
                catalogue.Select(service => service.Code));

            Assert.NotNull(dbContext.Model.FindEntityType(typeof(AuditLog)));
            Assert.NotNull(dbContext.Model.FindEntityType(typeof(OutboxMessage)));

            var health = await provider
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(
                    registration =>
                        registration.Name == "survey-catalogue");
            Assert.Equal(HealthStatus.Healthy, health.Status);
        }
        finally
        {
            await DropDatabaseAsync(databaseName);
        }
    }

    private static IConfiguration CreateConfiguration(string databaseName) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    BuildConnectionString(databaseName)
            })
            .Build();

    private static AgriDroneSchemaDbContext CreateMigrationContext(
        string databaseName)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            BuildConnectionString(databaseName));
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        var dataSource = dataSourceBuilder.Build();
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, dataSource);
        return new AgriDroneSchemaDbContext(options.Options);
    }

    private static async Task RecreateDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE); " +
            $"CREATE DATABASE {databaseName};";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE);";
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildConnectionString(string databaseName) =>
        $"Host=127.0.0.1;Port=56432;Database={databaseName};" +
        "Username=agridrone_test;Password=agridrone_test";
}
