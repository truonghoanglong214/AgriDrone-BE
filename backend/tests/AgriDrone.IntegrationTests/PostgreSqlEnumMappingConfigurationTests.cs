using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class PostgreSqlEnumMappingConfigurationTests
{
    [Fact]
    public void SharedDataSourceContextsUseNativePostgreSqlEnumMappings()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=enum_mapping_model_test;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection()
            .AddAgriDroneDatabase(configuration)
            .AddMappingPublicationPersistence(configuration)
            .AddSurveyResultPublicationPersistence(configuration)
            .AddSurveyApprovalPersistence(configuration)
            .AddSurveyBoundaryVerificationPersistence(configuration)
            .AddSurveyAppointmentSchedulingPersistence(configuration)
            .AddSurveyPricingConfirmationPersistence(configuration)
            .AddSurveyPaymentProcessingPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var contexts = new DbContext[]
        {
            scope.ServiceProvider.GetRequiredService<AgriDroneSchemaDbContext>(),
            scope.ServiceProvider.GetRequiredService<MappingPublicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyResultPublicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyApprovalDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyBoundaryVerificationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyAppointmentSchedulingDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyPricingConfirmationDbContext>(),
            scope.ServiceProvider.GetRequiredService<SurveyPaymentProcessingDbContext>()
        };

        foreach (var context in contexts)
        {
            var enumProperties = context.Model.GetEntityTypes()
                .SelectMany(entity => entity.GetProperties())
                .Where(IsPostgreSqlEnum)
                .ToArray();

            Assert.NotEmpty(enumProperties);
            Assert.All(enumProperties, property =>
                Assert.Contains(
                    "NpgsqlEnumTypeMapping",
                    property.GetRelationalTypeMapping().GetType().Name,
                    StringComparison.Ordinal));
        }
    }

    private static bool IsPostgreSqlEnum(IReadOnlyProperty property)
    {
        var storeType = property.GetColumnType();
        return storeType?.StartsWith(
            "system.",
            StringComparison.OrdinalIgnoreCase) == true;
    }
}
