using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AgriDrone.Database;

public sealed class AgriDroneSchemaDbContextFactory
    : IDesignTimeDbContextFactory<AgriDroneSchemaDbContext>
{
    private const string ConnectionStringVariable = "AGRIDRONE_DB_CONNECTION";
    private const string ConnectionStringName = "AgriDrone";

    public AgriDroneSchemaDbContext CreateDbContext(string[] args)
    {
        var configurationDirectory = FindConfigurationDirectory();
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configurationDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable)
            ?? configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not found. Configure it in " +
                $"appsettings.{environment}.json, through " +
                $"'ConnectionStrings__{ConnectionStringName}', or through the legacy " +
                $"'{ConnectionStringVariable}' environment variable.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        PostgreSqlEnumMappings.ConfigureDataSource(dataSourceBuilder);
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        optionsBuilder.UseNpgsql(
            dataSource,
            npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
                npgsqlOptions.MigrationsAssembly(typeof(AgriDroneSchemaDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    DbSchemas.System);
            });

        return new AgriDroneSchemaDbContext(optionsBuilder.Options);
    }

    private static string FindConfigurationDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var directCandidate = directory.FullName;
            var sourceCandidate = Path.Combine(directory.FullName, "src", "AgriDrone.Api");
            var repositoryCandidate = Path.Combine(
                directory.FullName,
                "backend",
                "src",
                "AgriDrone.Api");

            foreach (var candidate in new[]
                     {
                         directCandidate,
                         sourceCandidate,
                         repositoryCandidate
                     })
            {
                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate src/AgriDrone.Api/appsettings.json from the current directory.");
    }
}
