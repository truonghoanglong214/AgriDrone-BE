using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Authorization;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Authentication;
using AgriDrone.Modules.Identity.Infrastructure.Authorization;
using AgriDrone.Modules.Identity.Infrastructure.Configuration;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.Modules.Identity.Infrastructure.Queries;
using AgriDrone.Modules.Identity.Infrastructure.Repositories;
using AgriDrone.Modules.Identity.Infrastructure.Security;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();
        var translator = UpperSnakeCaseNameTranslator.Instance;

        services
            .AddOptions<TenantInvitationOptions>()
            .Bind(configuration.GetSection(TenantInvitationOptions.SectionName))
            .ValidateOnStart();

        services
            .AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<
            IValidateOptions<TenantInvitationOptions>,
            TenantInvitationOptionsValidator>();
        services.AddSingleton<
            IValidateOptions<PasswordResetOptions>,
            PasswordResetOptionsValidator>();

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MapEnum<UserStatus>("user_status", "system", translator)
                    .MapEnum<FarmMemberRole>("farm_member_role", "system", translator)
                    .MapEnum<FarmAccessScope>("farm_access_scope", "system", translator)
                    .MapEnum<TenantMemberRole>("tenant_member_role", "system", translator)
                    .MapEnum<TenantInvitationStatus>("tenant_invitation_status", "system", translator)
                    .MapEnum<GeneralStatus>("general_status", "system", translator)));

        services.AddScoped<IIdentityUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<ITenantMembershipQueries, TenantMembershipQueries>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ITenantSelectionTokenService, TenantSelectionTokenService>();
        services.AddScoped<IAuthorizationHandler, TenantRoleAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, FarmRoleAuthorizationHandler>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<ITenantInvitationRepository, TenantInvitationRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddSingleton<IInvitationTokenService, InvitationTokenService>();
        services.AddSingleton<IPasswordResetTokenService, PasswordResetTokenService>();

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(
                IdentityAuthorizationPolicies.SystemAdmin,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(SystemRoleCodes.SystemAdmin));

            authorization.AddPolicy(
                IdentityAuthorizationPolicies.TenantMember,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new TenantRoleRequirement(
                        TenantMemberRole.Owner,
                        TenantMemberRole.TenantAdmin,
                        TenantMemberRole.Member)));

            authorization.AddPolicy(
                IdentityAuthorizationPolicies.TenantAdmin,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new TenantRoleRequirement(
                        TenantMemberRole.Owner,
                        TenantMemberRole.TenantAdmin)));

            authorization.AddPolicy(
                IdentityAuthorizationPolicies.TenantOwner,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new TenantRoleRequirement(
                        TenantMemberRole.Owner)));

            authorization.AddPolicy(
                IdentityAuthorizationPolicies.FarmMember,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new FarmRoleRequirement(
                        FarmMemberRole.Owner,
                        FarmMemberRole.Manager,
                        FarmMemberRole.Worker)));

            authorization.AddPolicy(
                IdentityAuthorizationPolicies.FarmManager,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new FarmRoleRequirement(
                        FarmMemberRole.Owner,
                        FarmMemberRole.Manager)));

            authorization.AddPolicy(
                IdentityAuthorizationPolicies.FarmOwner,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new FarmRoleRequirement(
                        FarmMemberRole.Owner)));
        });

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
