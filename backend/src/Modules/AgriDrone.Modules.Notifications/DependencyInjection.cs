using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.Modules.Notifications.Application.EmailTemplates;
using AgriDrone.Modules.Notifications.Infrastructure.Messaging;
using AgriDrone.Modules.Notifications.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredAgriDroneConnectionString();

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IEmailTemplate, TenantInvitationEmailTemplate>();
        services.AddScoped<IEmailTemplate, TenantWelcomeEmailTemplate>();
        services.AddScoped<IEmailTemplate, TenantRegistrationSuccessTemplate>();
        services.AddScoped<EmailTemplateRenderer>();
        services.AddScoped<
            IIntegrationMessageHandler<EmailNotificationRequestedV1>,
            EmailNotificationRequestedHandler>();
        services.AddIntegrationConsumer<EmailNotificationRequestedProcessor>(
            IntegrationConsumerNames.NotificationsEmailV1);

        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(mediatR =>
            mediatR.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(
            assembly,
            includeInternalTypes: true);

        return services;
    }
}
