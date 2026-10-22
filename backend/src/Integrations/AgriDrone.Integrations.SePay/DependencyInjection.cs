using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.SePay;

public static class DependencyInjection
{
    public static IServiceCollection AddSePayIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SePayOptions>()
            .Bind(configuration.GetSection(SePayOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SePayOptions>, SePayOptionsValidator>();
        services.AddHttpClient("SePayCheckout")
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        var isSePayProvider = string.Equals(
            configuration["SurveyPayments:Provider"]?.Trim(),
            "sepay",
            StringComparison.OrdinalIgnoreCase);
        if (isSePayProvider)
        {
            services.Replace(ServiceDescriptor.Scoped<
                ISurveyPaymentGateway, SePayPaymentService>());
            services.Replace(ServiceDescriptor.Scoped<
                ISurveyPaymentCallbackVerifier, SePayPaymentService>());
        }

        return services;
    }
}
