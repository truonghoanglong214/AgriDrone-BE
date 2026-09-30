using AgriDrone.Modules.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Initialization;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Surveys;

public sealed class SurveysDependencyInjectionTests
{
    [Fact]
    public void AddSurveysModuleRegistersRuntimeDependencies()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSurveysModule(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<SurveysDbContext>();

        Assert.Same(
            dbContext,
            scope.ServiceProvider.GetRequiredService<ISurveysUnitOfWork>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<ISurveyServiceRepository>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<ISurveyCatalogueQueries>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());
        Assert.Same(
            TimeProvider.System,
            scope.ServiceProvider.GetRequiredService<TimeProvider>());

        var healthOptions = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;
        Assert.Contains(
            healthOptions.Registrations,
            registration =>
                registration.Name == "survey-catalogue" &&
                registration.Tags.Contains("ready"));
    }

    [Fact]
    public void RequiredSurveyCatalogueSeedsAcceptLifecycleChanges()
    {
        var result = SurveyCatalogueSeedRules.Validate(
        [
            new SurveyServiceSeedSnapshot(
                "PLANT_HEALTH",
                SurveyServiceType.PlantHealth),
            new SurveyServiceSeedSnapshot(
                "HARVEST_READINESS",
                SurveyServiceType.HarvestReadiness)
        ]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RequiredSurveyCatalogueSeedsRejectMissingOrRepurposedService()
    {
        var result = SurveyCatalogueSeedRules.Validate(
        [
            new SurveyServiceSeedSnapshot(
                "PLANT_HEALTH",
                SurveyServiceType.HarvestReadiness)
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "PLANT_HEALTH",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "HARVEST_READINESS",
                StringComparison.Ordinal));
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=127.0.0.1;Port=5432;Database=agridrone;" +
                    "Username=agridrone;Password=agridrone"
            })
            .Build();
}
