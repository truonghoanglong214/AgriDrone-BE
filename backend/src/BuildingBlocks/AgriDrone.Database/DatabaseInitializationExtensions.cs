using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Database;

public static class DatabaseInitializationExtensions
{
    public static async Task MigrateAgriDroneDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<AgriDroneSchemaDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
