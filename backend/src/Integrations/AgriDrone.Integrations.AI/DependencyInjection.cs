using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddAiIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeotagOptions>(configuration.GetSection(GeotagOptions.SectionName));
        services.Configure<MetashapeOptions>(configuration.GetSection(MetashapeOptions.SectionName));

        services.AddScoped<IGeotagService, PythonGeotagService>();

        services.AddHttpClient<IMetashapeService, MetashapeService>((serviceProvider, client) =>
        {
            var metashapeOpts = serviceProvider.GetRequiredService<IOptions<MetashapeOptions>>().Value;
            if (Uri.TryCreate(metashapeOpts.Endpoint, UriKind.Absolute, out var endpointUri))
            {
                client.BaseAddress = endpointUri;
            }
            client.Timeout = TimeSpan.FromMinutes(metashapeOpts.TimeoutMinutes);
        });

        return services;
    }
}

