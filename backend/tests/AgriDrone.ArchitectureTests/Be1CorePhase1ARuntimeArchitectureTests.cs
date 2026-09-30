using System.Reflection;
using AgriDrone.Api.Controllers;
using AgriDrone.Modules.Surveys;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class Be1CorePhase1ARuntimeArchitectureTests
{
    [Fact]
    public void ApiReferencesSurveysRuntimeModule()
    {
        var references = typeof(FarmController).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name);

        Assert.Contains("AgriDrone.Modules.Surveys", references);
    }

    [Fact]
    public void SurveysExposesOneRuntimeRegistrationEntryPoint()
    {
        var registrationMethods = typeof(DependencyInjection)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(
                DependencyInjection.AddSurveysModule))
            .ToArray();

        var registrationMethod = Assert.Single(registrationMethods);
        Assert.Equal(2, registrationMethod.GetParameters().Length);
    }

    [Fact]
    public void ApiControllersDoNotDependOnSurveysInfrastructureOrRepositories()
    {
        var controllers = typeof(FarmController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controller in controllers)
        {
            var dependencyTypes = controller
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Concat(controller.GetFields(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    .Select(field => field.FieldType));

            Assert.DoesNotContain(dependencyTypes, IsForbiddenDependency);
        }
    }

    private static bool IsForbiddenDependency(Type dependencyType)
    {
        var fullName = dependencyType.FullName ?? string.Empty;
        return fullName.StartsWith(
                   "AgriDrone.Modules.Surveys.Infrastructure.",
                   StringComparison.Ordinal) ||
               fullName.EndsWith(
                   "SurveyServiceRepository",
                   StringComparison.Ordinal);
    }
}
