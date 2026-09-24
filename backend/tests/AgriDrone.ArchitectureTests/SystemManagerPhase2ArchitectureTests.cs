using AgriDrone.Api.Controllers;
using AgriDrone.Database;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class SystemManagerPhase2ArchitectureTests
{
    [Theory]
    [InlineData(typeof(SystemManagersController))]
    [InlineData(typeof(SystemFarmManagersController))]
    public void ManagerAdministrationIsRestrictedToSystemAdmin(Type controllerType)
    {
        var attribute = Assert.Single(
            controllerType.GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AccessAuthorizationPolicies.SystemAdmin, attribute.Policy);
    }

    [Fact]
    public void ManagerWorkApiUsesSystemManagerPolicy()
    {
        var attribute = Assert.Single(
            typeof(SystemManagerWorkController).GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AccessAuthorizationPolicies.SystemManager, attribute.Policy);
    }

    [Fact]
    public void DatabaseModelEnforcesOneActivePrimaryManagerPerFarm()
    {
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=model_only;Username=model;Password=model",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;
        using var dbContext = new AgriDroneSchemaDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(FarmManagerAssignment));

        Assert.NotNull(entity);
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.GetDatabaseName() ==
                "uq_farm_manager_assignments_active_farm");
        Assert.True(index.IsUnique);
        Assert.Equal("ended_at IS NULL", index.GetFilter());
        Assert.Equal(
            [nameof(FarmManagerAssignment.FarmId)],
            index.Properties.Select(property => property.Name).ToArray());
    }
}
