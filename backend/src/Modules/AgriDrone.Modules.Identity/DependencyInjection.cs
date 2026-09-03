using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Identity.Application.Abstractions.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Invitations.Creation;
using AgriDrone.Modules.Identity.Application.Invitations.EmailDelivery;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Application.PasswordReset.EmailDelivery;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.PasswordResetTokens;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.TenantInvitations;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Authentication;
using AgriDrone.Modules.Identity.Infrastructure.Authorization;
using AgriDrone.Modules.Identity.Infrastructure.Configuration;
using AgriDrone.Modules.Identity.Infrastructure.Initialization;
using AgriDrone.Modules.Identity.Infrastructure.Messaging;
using AgriDrone.Modules.Identity.Infrastructure.Messaging.Consumers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.Modules.Identity.Infrastructure.Queries;
using AgriDrone.Modules.Identity.Infrastructure.Repositories;
using AgriDrone.Modules.Identity.Infrastructure.Security;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Domain;
using FluentValidation;
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

        services
            .AddOptions<SystemAdminBootstrapOptions>()
            .Bind(configuration.GetSection(SystemAdminBootstrapOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<
            IValidateOptions<SystemAdminBootstrapOptions>,
            SystemAdminBootstrapOptionsValidator>();

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
                    .MapEnum<TenantInvitationPurpose>("tenant_invitation_purpose", "system", translator)
                    .MapEnum<GeneralStatus>("general_status", "system", translator)
                    .MapEnum<AuditActorType>("audit_actor_type", "system", translator)));

        services.AddScoped<IIdentityUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IAuditLogSink>(serviceProvider => serviceProvider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<ITenantQueries, TenantQueries>();
        services.AddScoped<ITenantMembershipQueries, TenantMembershipQueries>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISystemAdminBootstrapLock, SystemAdminBootstrapLock>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ITenantSelectionTokenService, TenantSelectionTokenService>();
        services.AddScoped<IEffectiveAccessService, EffectiveAccessService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantMembershipRepository, TenantMembershipRepository>();
        services.AddScoped<ITenantInvitationRepository, TenantInvitationRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPasswordResetEmailDelivery, PasswordResetEmailDelivery>();
        services.AddScoped<ITenantInvitationService, TenantInvitationService>();
        services.AddScoped<ITenantInvitationEmailDelivery, TenantInvitationEmailDelivery>();
        services.AddScoped<IIntegrationMessageHandler<TenantInvitationEmailRequestedV1>, TenantInvitationEmailRequestedHandler>();
        services.AddScoped<IIdentityIntegrationOutbox, IdentityIntegrationOutbox>();
        services.AddSingleton<IInvitationTokenService, InvitationTokenService>();
        services.AddSingleton<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddIntegrationConsumer<TenantInvitationEmailRequestedProcessor>(IntegrationConsumerNames.EmailTenantInvitationV1);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
