using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.SharedInfrastructure.Validation;

public static class DependencyInjection
{
    public static IServiceCollection AddValidationPipeline(
        this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(
                typeof(ValidationPipelineBehavior<,>).Assembly);
            configuration.AddOpenBehavior(
                typeof(ValidationPipelineBehavior<,>));
        });

        return services;
    }
}
