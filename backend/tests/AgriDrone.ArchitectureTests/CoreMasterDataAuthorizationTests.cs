using AgriDrone.Api.Controllers;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class CoreMasterDataAuthorizationTests
{
    [Theory]
    [InlineData(typeof(SystemPlantConditionsController))]
    [InlineData(typeof(SystemHarvestQualityGradeController))]
    public void MutationControllersRequireSystemAdministrator(Type controllerType)
    {
        var authorize = Assert.Single(
            controllerType.GetCustomAttributes(
                typeof(AuthorizeAttribute),
                inherit: true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(
            AccessAuthorizationPolicies.SystemAdmin,
            authorize.Policy);
    }
}
