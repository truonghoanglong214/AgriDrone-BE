using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database;

internal static class AgriDroneSchemaDbContextOptions
{
    public static void Configure(
        DbContextOptionsBuilder optionsBuilder,
        NpgsqlDataSource dataSource)
    {
        optionsBuilder
            .UseNpgsql(
                dataSource,
                npgsqlOptions =>
                {
                    npgsqlOptions.UseNetTopologySuite();
                    PostgreSqlEnumMappings.ConfigureOptions(npgsqlOptions);
                    npgsqlOptions.MigrationsAssembly(
                        typeof(AgriDroneSchemaDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable(
                        "__ef_migrations_history",
                        DbSchemas.System);
                })
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .UseSeeding((dbContext, _) =>
            {
                SystemRoleSeeder.Seed(dbContext);
                SurveyServiceV3Seeder.Seed(dbContext);
            })
            .UseAsyncSeeding(async (dbContext, _, cancellationToken) =>
            {
                await SystemRoleSeeder.SeedAsync(dbContext, cancellationToken);
                await SurveyServiceV3Seeder.SeedAsync(dbContext, cancellationToken);
            });
    }
}
