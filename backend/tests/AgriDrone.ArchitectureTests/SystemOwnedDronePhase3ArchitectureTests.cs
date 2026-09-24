using System.Reflection;
using AgriDrone.Api.Controllers;
using AgriDrone.Database;
using AgriDrone.Database.Migrations;
using AgriDrone.Modules.Missions.Application.Features.Drones.ChangeDroneStatus;
using AgriDrone.Modules.Missions.Application.Features.Drones.RegisterDrone;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class SystemOwnedDronePhase3ArchitectureTests
{
    [Fact]
    public void RegistryApiIsRestrictedToSystemAdmin()
    {
        var attribute = Assert.Single(
            typeof(SystemDronesController).GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AccessAuthorizationPolicies.SystemAdmin, attribute.Policy);

        var route = Assert.Single(
            typeof(SystemDronesController).GetCustomAttributes(
                    typeof(RouteAttribute),
                    inherit: true)
                .Cast<RouteAttribute>());
        Assert.Equal("api/system/drones", route.Template);

        Assert.Single(
            typeof(SystemDronesController)
                .GetMethod(nameof(SystemDronesController.Register))!
                .GetCustomAttributes(typeof(HttpPostAttribute), inherit: true));
        var statusRoute = Assert.Single(
            typeof(SystemDronesController)
                .GetMethod(nameof(SystemDronesController.ChangeStatus))!
                .GetCustomAttributes(typeof(HttpPatchAttribute), inherit: true)
                .Cast<HttpPatchAttribute>());
        Assert.Equal("{droneId:guid}/status", statusRoute.Template);
    }

    [Fact]
    public void AvailabilityApiIsRestrictedToSystemManager()
    {
        var attribute = Assert.Single(
            typeof(SystemManagerDronesController).GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AccessAuthorizationPolicies.SystemManager, attribute.Policy);

        var route = Assert.Single(
            typeof(SystemManagerDronesController).GetCustomAttributes(
                    typeof(RouteAttribute),
                    inherit: true)
                .Cast<RouteAttribute>());
        Assert.Equal(
            "api/system-manager/farms/{farmId:guid}/drones",
            route.Template);
    }

    [Fact]
    public void DroneContractsAndAggregateDoNotExposeTenantOwnership()
    {
        Assert.Null(typeof(Drone).GetProperty("TenantId"));
        Assert.Null(typeof(RegisterDroneCommand).GetProperty("TenantId"));
        Assert.Null(typeof(RegisterDroneResponse).GetProperty("TenantId"));
        Assert.Null(typeof(ChangeDroneStatusCommand).GetProperty("TenantId"));
        Assert.DoesNotContain(
            typeof(SystemDronesController).Assembly.GetTypes(),
            type => type.Name == "DronesController");
    }

    [Fact]
    public void DatabaseModelUsesGlobalDroneKeysAndMissionRelationship()
    {
        using var dbContext = CreateModelContext();
        var drone = Assert.IsType<RuntimeEntityType>(
            dbContext.Model.FindEntityType(typeof(Drone)));
        var mission = Assert.IsType<RuntimeEntityType>(
            dbContext.Model.FindEntityType(typeof(DroneMission)));

        Assert.Null(drone.FindProperty("TenantId"));
        AssertGlobalUniqueIndex(drone, "uq_drones_code", nameof(Drone.Code));
        AssertGlobalUniqueIndex(
            drone,
            "uq_drones_serial_number",
            nameof(Drone.SerialNumber));
        AssertGlobalUniqueIndex(
            drone,
            "uq_drones_registration_number",
            nameof(Drone.RegistrationNumber));

        var scheduleIndex = Assert.Single(
            mission.GetIndexes(),
            index => index.GetDatabaseName() ==
                "ix_drone_missions_drone_schedule");
        Assert.Equal(
            [
                nameof(DroneMission.DroneId),
                nameof(DroneMission.ScheduledAt),
                nameof(DroneMission.ScheduledEndAt)
            ],
            scheduleIndex.Properties.Select(property => property.Name).ToArray());

        var droneForeignKey = Assert.Single(
            mission.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Drone));
        Assert.Equal(
            [nameof(DroneMission.DroneId)],
            droneForeignKey.Properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            "fk_drone_missions_drones_drone_id",
            droneForeignKey.GetConstraintName());
    }

    [Fact]
    public void MigrationFailsFastOnCollisionAndCreatesGlobalScheduleExclusion()
    {
        var migration = new ConvertDronesToSystemOwned();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var up = typeof(ConvertDronesToSystemOwned).GetMethod(
            "Up",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(up);
        up.Invoke(migration, [builder]);

        var sql = string.Join(
            Environment.NewLine,
            builder.Operations
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));
        Assert.Contains("Resolve each physical-drone collision manually", sql);
        Assert.Contains("drone_legacy_tenant_ownership", sql);
        Assert.Contains("drone_id WITH =", sql);

        var addGlobalConstraint = sql[(sql.LastIndexOf(
            "ADD CONSTRAINT ex_drone_missions_no_schedule_overlap",
            StringComparison.Ordinal))..];
        Assert.DoesNotContain("tenant_id WITH =", addGlobalConstraint);
    }

    private static AgriDroneSchemaDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<AgriDroneSchemaDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=model_only;Username=model;Password=model",
                npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new AgriDroneSchemaDbContext(options);
    }

    private static void AssertGlobalUniqueIndex(
        IEntityType entityType,
        string databaseName,
        string propertyName)
    {
        var index = Assert.Single(
            entityType.GetIndexes(),
            candidate => candidate.GetDatabaseName() == databaseName);
        Assert.True(index.IsUnique);
        Assert.Equal(
            [propertyName],
            index.Properties.Select(property => property.Name).ToArray());
    }
}
