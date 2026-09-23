using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json;
using AgriDrone.Api.Controllers;
using AgriDrone.Api.Legacy;
using AgriDrone.SharedInfrastructure.Authentication;
using AgriDrone.Modules.Farms.Domain.Farms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.ArchitectureTests;

public sealed class LegacyEndpointSafetyTests
{
    public static TheoryData<Type, string> DisabledMutationEndpoints => new()
    {
        { typeof(AuthController), nameof(AuthController.Register) },
        { typeof(TenantInvitationController), nameof(TenantInvitationController.InviteTenantAdmin) },
        { typeof(TenantInvitationController), nameof(TenantInvitationController.InviteTenantMember) },
        { typeof(TenantMembershipController), nameof(TenantMembershipController.UpdateRole) },
        { typeof(TenantMembershipController), nameof(TenantMembershipController.UpdateStatus) },
        { typeof(TenantOwnershipController), nameof(TenantOwnershipController.TransferOwnership) },
        { typeof(FarmController), nameof(FarmController.CreateFarm) },
        { typeof(FarmController), nameof(FarmController.AssignFarmMember) },
        { typeof(FarmController), nameof(FarmController.RevokeFarmMemberAssignment) },
        { typeof(DronesController), nameof(DronesController.RegisterDrone) },
        { typeof(DronesController), nameof(DronesController.ChangeStatus) },
        { typeof(DronesController), nameof(DronesController.GetAvailableDrones) },
        { typeof(MissionsController), nameof(MissionsController.CreateMission) },
        { typeof(MissionsController), nameof(MissionsController.ScheduleMission) },
        { typeof(MissionsController), nameof(MissionsController.TransitionMission) },
        { typeof(HarvestCatalogController), nameof(HarvestCatalogController.GetHarvestQualityGrades) },
        { typeof(SystemHarvestQualityGradeController), nameof(SystemHarvestQualityGradeController.Create) },
        { typeof(SystemHarvestQualityGradeController), nameof(SystemHarvestQualityGradeController.CreateVersion) },
        { typeof(SystemHarvestQualityGradeController), nameof(SystemHarvestQualityGradeController.Retire) }
    };

    public static TheoryData<Type, string> RetainedEndpoints => new()
    {
        { typeof(AuthController), nameof(AuthController.Login) },
        { typeof(AuthController), nameof(AuthController.ForgotPassword) },
        { typeof(AuthController), nameof(AuthController.ResetPassword) },
        { typeof(AuthController), nameof(AuthController.SelectTenant) },
        { typeof(TenantInvitationController), nameof(TenantInvitationController.PreviewTenantInvitation) },
        { typeof(TenantInvitationController), nameof(TenantInvitationController.AcceptTenantInvitation) },
        { typeof(FarmController), nameof(FarmController.GetFarmById) },
        { typeof(MissionsController), nameof(MissionsController.GetMissionDetails) }
    };

    [Theory]
    [MemberData(nameof(DisabledMutationEndpoints))]
    public void ObsoleteBusinessEntryPointsAreMarkedForStableGoneResponse(
        Type controllerType,
        string methodName)
    {
        var method = controllerType.GetMethod(methodName);

        Assert.NotNull(method);
        var legacy = Assert.Single(
            method.GetCustomAttributes(
                    typeof(LegacyEndpointAttribute),
                    inherit: true)
                .Cast<LegacyEndpointAttribute>());
        Assert.NotEmpty(legacy.RouteName);
        Assert.NotEmpty(legacy.Replacement);
    }

    [Theory]
    [MemberData(nameof(RetainedEndpoints))]
    public void RequiredCompatibilityAndReadEndpointsRemainAvailable(
        Type controllerType,
        string methodName)
    {
        var method = controllerType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Empty(method.GetCustomAttributes(
            typeof(LegacyEndpointAttribute),
            inherit: true));
    }

    [Fact]
    public void LegacyRouteMetricLabelsAreUniqueAndBounded()
    {
        var routeNames = typeof(AuthController).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetCustomAttributes(
                    typeof(LegacyEndpointAttribute),
                    inherit: true)
                .Cast<LegacyEndpointAttribute>())
            .Select(attribute => attribute.RouteName)
            .ToArray();

        Assert.Equal(19, routeNames.Length);
        Assert.Equal(routeNames.Length, routeNames.Distinct().Count());
        Assert.All(routeNames, route => Assert.DoesNotContain('{', route));
    }

    [Fact]
    public async Task DisabledGateReturnsStableGoneProblemWithoutCallingHandler()
    {
        var handlerCalled = false;
        var middleware = new LegacyEndpointGateMiddleware(
            _ =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new LegacyFeaturesOptions()),
            NullLogger<LegacyEndpointGateMiddleware>.Instance);
        var context = CreateLegacyContext("farms.create-direct");

        await middleware.InvokeAsync(context);

        Assert.False(handlerCalled);
        Assert.Equal(StatusCodes.Status410Gone, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            LegacyEndpointGateMiddleware.DisabledErrorCode,
            json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(
            "farms.create-direct",
            json.RootElement.GetProperty("legacyRoute").GetString());
    }

    [Theory]
    [InlineData("farms.create-direct")]
    [InlineData("drones.register-tenant-scoped")]
    [InlineData("missions.create-direct")]
    public async Task TenantOwnerCannotInvokeDirectOperationalEntryPoints(
        string routeName)
    {
        var handlerCalled = false;
        var middleware = new LegacyEndpointGateMiddleware(
            _ =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new LegacyFeaturesOptions()),
            NullLogger<LegacyEndpointGateMiddleware>.Instance);
        var context = CreateLegacyContext(routeName);
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(AgriDroneClaimTypes.TenantRole, "OWNER")],
                authenticationType: "test"));

        await middleware.InvokeAsync(context);

        Assert.False(handlerCalled);
        Assert.Equal(StatusCodes.Status410Gone, context.Response.StatusCode);
    }

    [Fact]
    public async Task ExplicitRollbackFlagCanTemporarilyPassThrough()
    {
        var handlerCalled = false;
        var middleware = new LegacyEndpointGateMiddleware(
            context =>
            {
                handlerCalled = true;
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            },
            Options.Create(new LegacyFeaturesOptions
            {
                EnableDeprecatedEndpoints = true
            }),
            NullLogger<LegacyEndpointGateMiddleware>.Instance);
        var context = CreateLegacyContext("missions.create-direct");

        await middleware.InvokeAsync(context);

        Assert.True(handlerCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public void AttemptMetricUsesStableRouteAndActorTags()
    {
        string? observedRoute = null;
        string? observedActor = null;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == LegacyEndpointTelemetry.MeterName &&
                    instrument.Name == LegacyEndpointTelemetry.AttemptCounterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (_, measurement, tags, _) =>
            {
                Assert.Equal(1, measurement);
                foreach (var tag in tags)
                {
                    if (tag.Key == "route")
                    {
                        observedRoute = tag.Value?.ToString();
                    }
                    else if (tag.Key == "actor")
                    {
                        observedActor = tag.Value?.ToString();
                    }
                }
            });
        listener.Start();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(AgriDroneClaimTypes.TenantRole, "OWNER")],
                    authenticationType: "test"))
        };

        LegacyEndpointTelemetry.RecordAttempt(
            context,
            "farms.create-direct",
            allowedByFeatureFlag: false);

        Assert.Equal("farms.create-direct", observedRoute);
        Assert.Equal("owner", observedActor);
    }

    [Fact]
    public void AnyFutureFieldTaskControllerWriteMustBeLegacyGated()
    {
        var violations = typeof(AuthController).Assembly
            .GetTypes()
            .Where(type => type.Name.Contains(
                "FieldTask",
                StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute.GetType().Name is
                    "HttpPostAttribute" or
                    "HttpPutAttribute" or
                    "HttpPatchAttribute" or
                    "HttpDeleteAttribute"))
            .Where(method => method.GetCustomAttributes(
                    typeof(LegacyEndpointAttribute),
                    inherit: true)
                .Length == 0)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void FarmArchiveCompatibilityAdapterHasReadOnlyLegacyDependencies()
    {
        var adapter = typeof(Farm).Assembly.GetType(
            "AgriDrone.Modules.Farms.Infrastructure.Queries." +
            "LegacyReadOnlyFarmArchiveDependencyQuery");

        Assert.NotNull(adapter);
        var constructor = Assert.Single(adapter.GetConstructors());
        var dependencyNames = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.Equal(
            [
                "FarmsDbContext",
                "IMissionArchiveReferenceQuery",
                "IFieldTaskArchiveReferenceQuery",
                "IPlantArchiveReferenceQuery"
            ],
            dependencyNames);
        Assert.DoesNotContain(
            dependencyNames,
            name => name.Contains("Repository", StringComparison.Ordinal) ||
                    name.Contains("Command", StringComparison.Ordinal) ||
                    name.Contains("Harvest", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacyFeaturesAreDisabledByDefault()
    {
        Assert.False(new LegacyFeaturesOptions().EnableDeprecatedEndpoints);
    }

    private static DefaultHttpContext CreateLegacyContext(string routeName)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/legacy-test";
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new LegacyEndpointAttribute(
                    routeName,
                    "Use the replacement workflow.")),
            routeName));
        return context;
    }
}
