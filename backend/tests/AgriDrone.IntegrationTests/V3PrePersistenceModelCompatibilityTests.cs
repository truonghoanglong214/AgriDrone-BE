using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.IntegrationTests;

// Real Npgsql models and SQL translation, without a database connection.
// Replace this pre-03B compatibility expectation when the expand migration ships.
public sealed class V3PrePersistenceModelCompatibilityTests
{
    [Fact]
    public void ExistingContextsDoNotReadUnmigratedV3DomainProperties()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=127.0.0.1;Port=1;Database=v3_model_test;Username=test;Password=test;Timeout=1"
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
        var checkedOrderModels = 0;
        var checkedHistoryModels = 0;

        foreach (var context in contexts)
        {
            var order = context.Model.FindEntityType(typeof(SurveyOrder));
            if (order is not null)
            {
                checkedOrderModels++;
                Assert.Null(order.FindProperty(nameof(SurveyOrder.DepositObligationId)));
                var sql = context.Set<SurveyOrder>().AsNoTracking().ToQueryString();
                Assert.DoesNotContain("DepositObligationId", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("deposit_obligation_id", sql, StringComparison.OrdinalIgnoreCase);
            }

            var history = context.Model.FindEntityType(typeof(OrderStateHistory));
            if (history is not null)
            {
                checkedHistoryModels++;
                Assert.Null(history.FindProperty(nameof(OrderStateHistory.TransitionSequence)));
                var sql = context.Set<OrderStateHistory>().AsNoTracking().ToQueryString();
                Assert.DoesNotContain("TransitionSequence", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("transition_sequence", sql, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Equal(contexts.Length, checkedOrderModels);
        Assert.True(checkedHistoryModels > 0);
    }
}
