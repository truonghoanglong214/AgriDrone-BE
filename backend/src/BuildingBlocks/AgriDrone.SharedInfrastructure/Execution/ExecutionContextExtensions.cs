using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.SharedInfrastructure.Execution;

public static class ExecutionContextExtensions
{
    public static IServiceCollection AddExecutionContext(
        this IServiceCollection services)
    {
        services.AddScoped<ScopedExecutionContext>();
        services.AddScoped<IExecutionContext>(serviceProvider =>
            serviceProvider.GetRequiredService<ScopedExecutionContext>());
        services.AddScoped<IExecutionContextInitializer>(serviceProvider =>
            serviceProvider.GetRequiredService<ScopedExecutionContext>());
        services.AddSingleton<IExecutionContextRunner, ExecutionContextRunner>();

        return services;
    }

    public static IApplicationBuilder UseExecutionContext(
        this IApplicationBuilder applicationBuilder)
    {
        return applicationBuilder.UseMiddleware<HttpExecutionContextMiddleware>();
    }
}
