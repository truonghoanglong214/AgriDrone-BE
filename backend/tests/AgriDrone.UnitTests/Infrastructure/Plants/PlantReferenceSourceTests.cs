using System.Runtime.CompilerServices;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.Modules.Plants;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Plants.Infrastructure.Queries;
using AgriDrone.SharedInfrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Plants;

public sealed class PlantReferenceSourceTests
{
    private static readonly DateTimeOffset RegisteredAt =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<IModel> ReferenceTestModel =
        new(CreateReferenceTestModel);

    [Fact]
    public void PlantsModuleRegistersPlantReferenceSourceAsScopedService()
    {
        var services = new ServiceCollection();

        services.AddPlantsModule(CreateConfiguration());

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IPlantReferenceSource));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(PlantReferenceSource), descriptor.ImplementationType);
    }

    [Fact]
    public async Task LoadReturnsOnlyEligiblePlantsInRequestedScopeAndStableOrder()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var mapVersionId = Guid.NewGuid();
        var farmReferences = new FakeFarmReferenceQuery(
            tenantId,
            farmId,
            zoneId,
            mapVersionId);
        var source = new PlantReferenceSource(context, farmReferences);

        var second = CreatePlant(
            farmId,
            zoneId,
            mapVersionId,
            "PLANT-002",
            rowIndex: 2,
            columnIndex: 1,
            longitude: 106.2,
            latitude: 10.2,
            locationAccuracyM: 1.25m);
        var first = CreatePlant(
            farmId,
            zoneId,
            mapVersionId,
            "PLANT-001",
            rowIndex: 1,
            columnIndex: 2,
            longitude: 106.1,
            latitude: 10.1,
            locationAccuracyM: 0.75m);
        SetLifecycleStatus(first, PlantLifecycleStatus.Missing);

        context.Plants.AddRange(
            second,
            first,
            WithLifecycle(
                CreatePlant(
                    farmId,
                    zoneId,
                    mapVersionId,
                    "REMOVED",
                    3,
                    1),
                PlantLifecycleStatus.Removed),
            CreatePlant(
                farmId,
                zoneId,
                Guid.NewGuid(),
                "OTHER-MAP",
                4,
                1),
            CreatePlant(
                Guid.NewGuid(),
                zoneId,
                mapVersionId,
                "OTHER-FARM",
                5,
                1),
            CreatePlant(
                farmId,
                Guid.NewGuid(),
                mapVersionId,
                "OTHER-ZONE",
                6,
                1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await source.LoadActiveByZoneAsync(
            tenantId,
            farmId,
            zoneId,
            mapVersionId);

        Assert.Collection(
            result,
            reference =>
            {
                Assert.Equal(first.Id, reference.PlantId);
                Assert.Equal("MISSING", reference.LifecycleStatus);
                Assert.Equal(1, reference.RowIndex);
                Assert.Equal(2, reference.ColumnIndex);
                Assert.Equal(10.1, reference.Latitude);
                Assert.Equal(106.1, reference.Longitude);
                Assert.Equal(0.75, reference.LocationAccuracyM);
            },
            reference =>
            {
                Assert.Equal(second.Id, reference.PlantId);
                Assert.Equal("ACTIVE", reference.LifecycleStatus);
                Assert.Equal(2, reference.RowIndex);
                Assert.Equal(1, reference.ColumnIndex);
                Assert.Equal(10.2, reference.Latitude);
                Assert.Equal(106.2, reference.Longitude);
                Assert.Equal(1.25, reference.LocationAccuracyM);
            });
        Assert.All(result, reference =>
        {
            Assert.Equal(farmId, reference.FarmId);
            Assert.Equal(zoneId, reference.ZoneId);
            Assert.Equal(mapVersionId, reference.MapVersionId);
        });
        Assert.Equal(1, farmReferences.MapCheckCallCount);
    }

    [Fact]
    public async Task LoadReturnsEmptyWhenMapDoesNotBelongToTenantScope()
    {
        await using var context = CreateContext();
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var mapVersionId = Guid.NewGuid();
        context.Plants.Add(
            CreatePlant(
                farmId,
                zoneId,
                mapVersionId,
                "PLANT-001",
                1,
                1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var farmReferences = new FakeFarmReferenceQuery(
            Guid.NewGuid(),
            farmId,
            zoneId,
            mapVersionId);
        var source = new PlantReferenceSource(context, farmReferences);

        var result = await source.LoadActiveByZoneAsync(
            Guid.NewGuid(),
            farmId,
            zoneId,
            mapVersionId);

        Assert.Empty(result);
        Assert.Equal(1, farmReferences.MapCheckCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task LoadRejectsEmptyScopeIdentifier(int emptyIndex)
    {
        await using var context = CreateContext();
        var identifiers = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
        identifiers[emptyIndex] = Guid.Empty;
        var farmReferences = new FakeFarmReferenceQuery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        var source = new PlantReferenceSource(context, farmReferences);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            source.LoadActiveByZoneAsync(
                identifiers[0],
                identifiers[1],
                identifiers[2],
                identifiers[3]));

        Assert.Equal(0, farmReferences.MapCheckCallCount);
    }

    [Fact]
    public void PlantReferenceContractIsSealedAndInitOnly()
    {
        var contractType = typeof(PlantReferenceV1);

        Assert.True(contractType.IsSealed);
        Assert.All(
            contractType.GetProperties(),
            property =>
            {
                Assert.NotNull(property.SetMethod);
                Assert.Contains(
                    typeof(IsExternalInit),
                    property.SetMethod.ReturnParameter
                        .GetRequiredCustomModifiers());
            });
        Assert.DoesNotContain(
            contractType.GetProperties(),
            property => property.PropertyType == typeof(Plant));
    }

    private static PlantsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlantsDbContext>()
            .UseInMemoryDatabase($"plant-reference-{Guid.NewGuid():N}")
            .UseModel(ReferenceTestModel.Value)
            .Options;

        return new PlantsDbContext(options);
    }

    private static Plant CreatePlant(
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        string plantCode,
        int rowIndex,
        int columnIndex,
        double longitude = 106.7,
        double latitude = 10.7,
        decimal? locationAccuracyM = 1m) =>
        Plant.RegisterManually(
            Guid.NewGuid(),
            farmId,
            zoneId,
            plantCode,
            new Point(longitude, latitude) { SRID = 4326 },
            mapVersionId,
            rowIndex,
            columnIndex,
            locationAccuracyM,
            Guid.NewGuid(),
            RegisteredAt);

    private static Plant WithLifecycle(
        Plant plant,
        PlantLifecycleStatus lifecycleStatus)
    {
        SetLifecycleStatus(plant, lifecycleStatus);
        return plant;
    }

    private static void SetLifecycleStatus(
        Plant plant,
        PlantLifecycleStatus lifecycleStatus)
    {
        var property = typeof(Plant).GetProperty(
            nameof(Plant.LifecycleStatus));
        Assert.NotNull(property);
        property.SetValue(plant, lifecycleStatus);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgriDrone"] =
                    "Host=localhost;Database=agridrone;Username=test;Password=test"
            })
            .Build();

    private static IModel CreateReferenceTestModel()
    {
        var options =
            new DbContextOptionsBuilder<ReferenceModelDbContext>()
                .UseInMemoryDatabase("plant-reference-model")
                .Options;

        using var context = new ReferenceModelDbContext(options);
        return context.Model;
    }

    private sealed class ReferenceModelDbContext(
        DbContextOptions<ReferenceModelDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Plant>()
                .Ignore(plant => plant.Scans);
            modelBuilder.Entity<HealthLevel>()
                .Ignore(level => level.PlantScans)
                .Ignore(level => level.ConditionDetections)
                .Ignore(level => level.CorrectedScanVerifications)
                .Ignore(level => level.CorrectedDetectionReviews);

            modelBuilder.ApplyConfiguration(new HealthLevelConfiguration());
            modelBuilder.ApplyConfiguration(new PlantConfiguration());
            modelBuilder.ApplyConfiguration(
                new PlantChangeEventConfiguration());
        }
    }

    private sealed class FakeFarmReferenceQuery(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId) : IMissionPlanningReferenceQuery
    {
        public int MapCheckCallCount { get; private set; }

        public Task<bool> IsActiveZoneAsync(
            Guid requestedTenantId,
            Guid requestedFarmId,
            Guid requestedZoneId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsConfirmedMapVersionAsync(
            Guid requestedTenantId,
            Guid requestedFarmId,
            Guid requestedZoneId,
            Guid requestedMapVersionId,
            CancellationToken cancellationToken = default)
        {
            MapCheckCallCount++;
            return Task.FromResult(
                requestedTenantId == tenantId &&
                requestedFarmId == farmId &&
                requestedZoneId == zoneId &&
                requestedMapVersionId == mapVersionId);
        }
    }
}
