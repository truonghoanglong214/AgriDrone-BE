using System.Reflection;
using AgriDrone.Api.Controllers;
using AgriDrone.Api.Legacy;
using AgriDrone.Modules.Farms.Application.Provisioning;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Identity.Application.Provisioning;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class Phase6BoundaryTests
{
    [Fact]
    public void ProvisioningIsExposedThroughExplicitNonHttpPorts()
    {
        Assert.True(typeof(ITenantProvisioningPort).IsInterface);
        Assert.True(typeof(ITenantOwnerInvitationPort).IsInterface);
        Assert.True(typeof(IFarmProvisioningPort).IsInterface);
        Assert.DoesNotContain("Controller", typeof(ITenantProvisioningPort).Name);
    }

    [Fact]
    public void MissionAndMappingExposeOrderAwarePreparationSeams()
    {
        Assert.NotNull(typeof(DroneMission).GetMethod(
            nameof(DroneMission.CreateForSurvey),
            BindingFlags.Public | BindingFlags.Static));
        Assert.True(typeof(IFarmBaseMapPublicationService).IsInterface);
    }

    [Fact]
    public void RetainedCoreModulesDoNotReferenceRemovedModules()
    {
        var assemblies = new[]
        {
            typeof(Farm).Assembly,
            typeof(DroneMission).Assembly,
            typeof(PlantScan).Assembly,
            typeof(SurveyOrder).Assembly
        };

        foreach (var assembly in assemblies)
        {
            var references = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.DoesNotContain("AgriDrone.Modules.FieldTasks", references);
            Assert.DoesNotContain("AgriDrone.Modules.Harvests", references);
        }
    }

    [Theory]
    [InlineData(nameof(FarmController.CreateZone))]
    [InlineData(nameof(FarmController.UpdateZone))]
    [InlineData(nameof(FarmController.ArchiveZone))]
    [InlineData(nameof(FarmController.UpdateFarmDetail))]
    public void FarmMutationsRequireSystemManagerPolicy(string methodName)
    {
        var method = typeof(FarmController).GetMethod(methodName)!;
        var policies = method.GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy);

        Assert.Contains(AccessAuthorizationPolicies.SystemManager, policies);
    }

    [Fact]
    public void Phase6DoesNotPublishSurveyBusinessControllers()
    {
        var surveyControllers = typeof(FarmController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.Name.Contains("Survey", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(surveyControllers);
    }

    [Fact]
    public void DirectFarmRestoreRemainsClosedAsLegacy()
    {
        var method = typeof(FarmController).GetMethod(
            nameof(FarmController.RestoreFarm))!;

        Assert.NotNull(method.GetCustomAttribute<LegacyEndpointAttribute>());
    }
}
