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
        services.AddScoped<IPlantReferenceQuery, CachedPlantReferenceQuery>();
        return services;
    }
}
