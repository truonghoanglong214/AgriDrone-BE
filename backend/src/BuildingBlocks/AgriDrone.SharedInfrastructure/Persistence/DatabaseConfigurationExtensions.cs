using Microsoft.Extensions.Configuration;

namespace AgriDrone.SharedInfrastructure.Persistence;

public static class DatabaseConfigurationExtensions
{
    public static string GetRequiredAgriDroneConnectionString(
        this IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AgriDrone");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["AGRIDRONE_DB_CONNECTION"];
        }

        return !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException(
                "Connection string 'AgriDrone' was not configured.");
    }
}
