using System.Reflection;
using AgriDrone.Api.Controllers;
using AgriDrone.Database.Mapping;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Persistence;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class Be1CorePhase0ArchitectureTests
{
    [Theory]
    [InlineData(typeof(ISurveyApprovalUnitOfWork))]
    [InlineData(typeof(IMappingPublicationUnitOfWork))]
    [InlineData(typeof(ISurveyResultPublicationUnitOfWork))]
    public void AtomicBusinessBoundariesExposeOneTransactionSeam(Type boundaryType)
    {
        Assert.True(boundaryType.IsInterface);
        Assert.True(typeof(IUnitOfWork).IsAssignableFrom(boundaryType));

        var transactionMethod = Assert.Single(
            boundaryType.GetMethods(),
            method => method.Name == "ExecuteInTransactionAsync");
        Assert.True(transactionMethod.IsGenericMethodDefinition);
        Assert.Equal(2, transactionMethod.GetParameters().Length);
    }

    [Fact]
    public void ApiControllersDoNotDependOnSurveysPersistence()
    {
        var controllers = typeof(FarmController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type));

        foreach (var controller in controllers)
        {
            var dependencyTypes = controller
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Concat(controller
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(field => field.FieldType));

            Assert.DoesNotContain(dependencyTypes, type =>
                string.Equals(
                    type.FullName,
                    "AgriDrone.Modules.Surveys.Infrastructure.Persistence.SurveysDbContext",
                    StringComparison.Ordinal));
        }
    }

    [Fact]
    public void SurveysModuleDoesNotReferenceApiOrCrossModuleDatabase()
    {
        var references = typeof(SurveyOrder).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain("AgriDrone.Api", references);
        Assert.DoesNotContain("AgriDrone.Database", references);
    }

    [Theory]
    [InlineData(typeof(SurveyRequest), typeof(SurveyRequestStatus))]
    [InlineData(typeof(SurveyOrder), typeof(SurveyOrderStatus))]
    [InlineData(typeof(SurveyAppointment), typeof(SurveyAppointmentStatus))]
    [InlineData(typeof(SurveyPayment), typeof(SurveyPaymentStatus))]
    [InlineData(typeof(PriceAdjustment), typeof(PriceAdjustmentStatus))]
    [InlineData(typeof(SurveyResult), typeof(SurveyResultStatus))]
    public void SurveyAggregatesOwnTheirStateTransitionRules(
        Type aggregateType,
        Type statusType)
    {
        var transitionMethod = aggregateType.GetMethod(
            "CanTransition",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [statusType, statusType],
            modifiers: null);

        Assert.NotNull(transitionMethod);
        Assert.Equal(aggregateType, transitionMethod.DeclaringType);
    }

    [Fact]
    public void SurveysModuleDoesNotExposeCentralizedStateMachineType()
    {
        var legacyStateMachine = typeof(SurveyOrder).Assembly.GetType(
            "AgriDrone.Modules.Surveys.Domain.SurveyStateTransitions");

        Assert.Null(legacyStateMachine);
    }

    [Fact]
    public void V2QueryContractsArePortsWithoutPersistenceDependency()
    {
        Assert.True(typeof(ISurveyOrderOperationalContextQuery).IsInterface);
        var references = typeof(ISurveyOrderOperationalContextQuery).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, reference =>
            reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, reference =>
            reference.StartsWith("AgriDrone.Modules.", StringComparison.Ordinal));
    }

    [Fact]
    public void PhaseZeroDoesNotOpenSurveyHttpSurface()
    {
        var surveyControllers = typeof(FarmController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.Name.Contains("Survey", StringComparison.Ordinal));

        Assert.Empty(surveyControllers);
    }
}
