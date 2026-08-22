using AgriDrone.IntegrationContracts.Plants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Caching;

public static class DependencyInjection
{
    public static IServiceCollection AddRedisCachingFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RedisCacheOptions>()
            .Bind(configuration.GetSection(RedisCacheOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<RedisCacheOptions>,
            RedisCacheOptionsValidator>();
        services.AddSingleton<RedisConnectionProvider>();
        services.AddSingleton<IPlantReferenceCache, RedisPlantReferenceCache>();
        // The PostgreSQL source belongs to the Plant Reference read use case
        // implemented in a later phase. Use a factory so the Step 1
        // infrastructure can start before that source is registered, while
        // still requiring it whenever the query is actually resolved.
        services.AddScoped<IPlantReferenceQuery>(provider =>
            new CachedPlantReferenceQuery(
                provider.GetRequiredService<IPlantReferenceCache>(),
                provider.GetRequiredService<IPlantReferenceSource>()));
        return services;
    }
}
