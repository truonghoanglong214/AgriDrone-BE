using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Api.Controllers;
using AgriDrone.SharedInfrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class SurveyRequestStep2ArchitectureTests
{
    [Fact]
    public void PublicIntakeIsExplicitlyAnonymous()
    {
        Assert.NotNull(typeof(PublicSurveyRequestsController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .SingleOrDefault());
    }

    [Theory]
    [InlineData(typeof(TenantOwnerSurveyRequestsController),
        AccessAuthorizationPolicies.TenantOwner)]
    [InlineData(typeof(SystemSurveyRequestsController),
        AccessAuthorizationPolicies.SystemAdmin)]
    public void AuthenticatedRequestControllersUseExpectedPolicy(
        Type controllerType,
        string expectedPolicy)
    {
        var authorization = Assert.Single(controllerType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(expectedPolicy, authorization.Policy);
    }

    [Fact]
    public void TenantOwnerBodiesCannotChooseTenantUserOrFarmIdentity()
    {
        AssertNoProperties(
            typeof(SubmitNewFarmSurveyRequest),
            "TenantId",
            "RequestedByUserId",
            "UserId",
            "FarmId");
        AssertNoProperties(
            typeof(SubmitExistingFarmSurveyRequest),
            "TenantId",
            "RequestedByUserId",
            "UserId",
            "FarmId");
    }

    private static void AssertNoProperties(
        Type contractType,
        params string[] prohibitedNames)
    {
        var actualNames = contractType.GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(prohibitedNames, actualNames.Contains);
    }
}
