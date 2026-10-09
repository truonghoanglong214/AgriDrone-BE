using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Api.Controllers;
using AgriDrone.Database;
using AgriDrone.Modules.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class OrderBoundMissionPlanningArchitectureTests
{
    [Fact]
    public void MissionsModuleRegistersOrderPlanningPort()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=model_only;Username=model;Password=model"
            })
            .Build();

        services.AddMissionsModule(configuration);

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(ISurveyOrderMissionPlanningQuery) &&
                descriptor.ImplementationType is not null);
    }

    [Fact]
    public void PrepareRouteIsRestrictedToSystemManager()
    {
        var authorize = Assert.Single(
            typeof(SystemManagerWorkController).GetCustomAttributes(
                    typeof(AuthorizeAttribute),
                    inherit: true)
                .Cast<AuthorizeAttribute>());
        Assert.Equal(
            AccessAuthorizationPolicies.SystemManager,
            authorize.Policy);

        var action = typeof(SystemManagerWorkController).GetMethod(
            nameof(SystemManagerWorkController.PrepareMissionSet));
        Assert.NotNull(action);
        var post = Assert.Single(action.GetCustomAttributes(
                typeof(HttpPostAttribute),
                inherit: true)
            .Cast<HttpPostAttribute>());
        Assert.Equal(
            "survey-orders/{surveyOrderId:guid}/missions/prepare",
            post.Template);
    }

    [Fact]
    public void ClientCannotSupplyAuthoritativeOrderDecisions()
    {
        var requestProperties = typeof(PrepareMissionSetRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("TenantId", requestProperties);
        Assert.DoesNotContain("FarmId", requestProperties);
        Assert.DoesNotContain("Purpose", requestProperties);
        Assert.DoesNotContain("RequiresBaselineMapping", requestProperties);
        Assert.DoesNotContain("CurrentBaseMapVersionId", requestProperties);
        Assert.DoesNotContain("ScopeZoneIds", requestProperties);
    }

    [Fact]
    public void MissionModelEnforcesOrderPurposeUniquenessAndConcurrency()
    {
        using var dbContext = CreateModelContext();
        var designTimeModel = dbContext
            .GetService<IDesignTimeModel>()
            .Model;
        var mission = designTimeModel.FindEntityType(typeof(DroneMission));
        Assert.NotNull(mission);

        var orderPurpose = Assert.Single(
            mission.GetIndexes(),
            index => index.GetDatabaseName() ==
                "uq_drone_missions_order_purpose");
        Assert.True(orderPurpose.IsUnique);
        Assert.Equal(
            [nameof(DroneMission.SurveyOrderId), nameof(DroneMission.Purpose)],
            orderPurpose.Properties.Select(property => property.Name));

        Assert.True(
            mission.FindProperty(nameof(DroneMission.Version))!
                .IsConcurrencyToken);
        Assert.Contains(
            mission.GetCheckConstraints(),
            constraint => constraint.Name ==
                "ck_drone_missions_order_binding");
        Assert.Contains(
            mission.GetCheckConstraints(),
            constraint => constraint.Name ==
                "ck_drone_missions_source_map");
    }

    [Fact]
    public void TargetMissionPurposesExcludeLegacyMissionTypes()
    {
        Assert.Equal(
            [
                MissionPurpose.BaselineMapping,
                MissionPurpose.PlantHealth,
                MissionPurpose.HarvestReadiness
            ],
            Enum.GetValues<MissionPurpose>());
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
}
