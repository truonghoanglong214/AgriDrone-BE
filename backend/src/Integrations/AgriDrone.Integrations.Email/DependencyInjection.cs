using AgriDrone.SharedKernel.Application.Abstractions.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.Email;

public static class DependencyInjection
{
    public static IServiceCollection AddEmailIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SmtpEmailOptions>()
            .Bind(configuration.GetSection(SmtpEmailOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SmtpEmailOptions>, SmtpEmailOptionsValidator>();
        services.AddTransient<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
