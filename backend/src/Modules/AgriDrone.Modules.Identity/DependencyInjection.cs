using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Authentication;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.Modules.Identity.Infrastructure.Queries;
using AgriDrone.Modules.Identity.Infrastructure.Repositories;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MapEnum<UserStatus>("user_status", "system", translator)
                    .MapEnum<FarmMemberRole>("farm_member_role", "system", translator)
                    .MapEnum<FarmAccessScope>("farm_access_scope", "system", translator)
                    .MapEnum<TenantMemberRole>("tenant_member_role", "system", translator)
                    .MapEnum<GeneralStatus>("general_status", "system", translator)));

        services.AddScoped<IIdentityUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
