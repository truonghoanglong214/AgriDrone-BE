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
                    npgsqlOptions.MigrationsAssembly(
                        typeof(AgriDroneSchemaDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable(
                        "__ef_migrations_history",
                        DbSchemas.System);
                })
            .UseSeeding((dbContext, _) =>
                SystemRoleSeeder.Seed(dbContext))
            .UseAsyncSeeding((dbContext, _, cancellationToken) =>
                SystemRoleSeeder.SeedAsync(dbContext, cancellationToken));
    }
}
