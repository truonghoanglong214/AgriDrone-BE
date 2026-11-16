using AgriDrone.Database;
using AgriDrone.Database.Surveys;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgriDrone.IntegrationTests;

public sealed class SurveyOrderCurrentAppointmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PublishedBaselineSelectsProposedHealthAppointmentBeforePricing()
    {
        var manager = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var order = CreateOrder(requiresBaselineMapping: true);
        var baseline = SurveyAppointment.Create(order.Id, SurveyAppointmentPurpose.BaselineMapping, 3,
            Guid.NewGuid(), manager, Now.AddHours(1), Now.AddHours(2), Now);
        baseline.Confirm(owner, Now);
        order.Appointments.Add(baseline);

        var orders = new[] { order }.AsQueryable();
        Assert.Same(baseline, Assert.Single(SystemManagerSurveyOrderQueries.SelectCurrentAppointments(orders)));

        order.MarkBaselineAppointmentConfirmed(Now);
        order.RecordBaselineDepositConfirmed(Guid.NewGuid(), 5000m, Now);
        order.StartBaselineMapping(Now);
        order.MarkBaselineCandidatesReady(Now);
        order.RecordBaselinePublication(Guid.NewGuid(), ConfirmedSurveyPoleCount.Create(20), manager, Now, Guid.NewGuid());
        Assert.Empty(SystemManagerSurveyOrderQueries.SelectCurrentAppointments(orders));

        var health = SurveyAppointment.Create(order.Id, SurveyAppointmentPurpose.PaidService, 1,
            null, manager, Now.AddDays(1), Now.AddDays(1).AddHours(1), Now);
        order.Appointments.Add(health);

        Assert.Null(order.PricingConfirmedAt);
        Assert.Null(order.ServiceReviewedAt);
        var current = Assert.Single(SystemManagerSurveyOrderQueries.SelectCurrentAppointments(orders));
        Assert.Equal(health.Id, current.Id);
        Assert.Equal(SurveyAppointmentPurpose.PaidService, current.Purpose);
        Assert.Equal(SurveyAppointmentStatus.Proposed, current.Status);
    }

    [Fact]
    public void FollowUpSelectsProposedHealthAppointmentBeforePricing()
    {
        var order = CreateOrder(requiresBaselineMapping: false);
        var manager = Guid.NewGuid();
        order.VerifyBoundaryScopeAndCurrentInventory(Guid.NewGuid(), Guid.NewGuid(),
            ConfirmedSurveyPoleCount.Create(20), manager, Now);
        var health = SurveyAppointment.Create(order.Id, SurveyAppointmentPurpose.PaidService, 1,
            null, manager, Now.AddDays(1), Now.AddDays(1).AddHours(1), Now);
        order.Appointments.Add(health);

        Assert.Null(order.PricingConfirmedAt);
        Assert.Same(health, Assert.Single(SystemManagerSurveyOrderQueries.SelectCurrentAppointments(
            new[] { order }.AsQueryable())));
    }

    [Fact]
    public void CurrentAppointmentQueryTranslatesToPostgreSql()
    {
        var builder = new NpgsqlDataSourceBuilder("Host=127.0.0.1;Database=unused;Username=unused;Password=unused");
        PostgreSqlEnumMappings.ConfigureDataSource(builder);
        using var source = builder.Build();
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>();
        AgriDroneSchemaDbContextOptions.Configure(options, source);
        using var context = new AgriDroneSchemaDbContext(options.Options);

        var sql = SystemManagerSurveyOrderQueries.SelectCurrentAppointments(context.Set<SurveyOrder>())
            .ToQueryString();

        Assert.Contains("PAID_SERVICE", sql);
        Assert.Contains("BASELINE_MAPPING", sql);
        Assert.Contains("farm_base_map_version_id", sql);
    }

    private static SurveyOrder CreateOrder(bool requiresBaselineMapping)
    {
        var order = SurveyOrder.Create("TEST-APPOINTMENT", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), requiresBaselineMapping, null, "v2.6", "NEW_ORDER",
            Now, 5000m, 20m);
        order.EnablePostFlightSettlement(SurveyServiceType.PlantHealth, Guid.NewGuid(), 2500m, 1500m, 1000m);
        return order;
    }
}
