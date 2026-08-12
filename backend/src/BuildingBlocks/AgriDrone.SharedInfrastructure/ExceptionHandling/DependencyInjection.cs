using System;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.SharedInfrastructure.ExceptionHandling
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddGlobalExceptionHandling(
            this IServiceCollection services)
        {
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            return services;
        }
    }
}
