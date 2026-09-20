using AgriDrone.Modules.Plants;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.Modules.Plants.Infrastructure.Persistence.Configurations;
using AgriDrone.Modules.Plants.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Infrastructure.Plants;

public sealed class PlantRepositoryTests
{
    private static readonly DateTimeOffset RegisteredAt =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    private static readonly Lazy<IModel> RepositoryTestModel =
        new(CreateRepositoryTestModel);

    [Fact]
    public void PlantsModuleRegistersPlantRepositoryAsScopedService()
    {
        var services = new ServiceCollection();
        services.AddPlantsModule(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository =
            scope.ServiceProvider.GetRequiredService<IPlantRepository>();

        Assert.IsType<PlantRepository>(repository);
    }

    [Fact]
    public void AddTracksPlantWithoutSavingChanges()
    {
        using var context = CreateContext();
        var repository = new PlantRepository(context);
        var plant = CreatePlant();

        repository.Add(plant);

        Assert.Equal(EntityState.Added, context.Entry(plant).State);
    }

    [Fact]
    public void AddChangeEventTracksEventWithoutSavingChanges()
    {
        using var context = CreateContext();
        var repository = new PlantRepository(context);
        var changeEvent = PlantChangeEvent.ManualRegistered(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatePoint(),
            rowIndex: null,
            columnIndex: null,
            Guid.NewGuid(),
            "Field inspection",
            RegisteredAt);

        repository.AddChangeEvent(changeEvent);

        Assert.Equal(EntityState.Added, context.Entry(changeEvent).State);
    }

    [Fact]
    public async Task CodeExistsUsesNormalizedCodeAndFarmScope()
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);
        var farmId = Guid.NewGuid();
        var otherFarmId = Guid.NewGuid();
        repository.Add(CreatePlant(farmId: farmId, plantCode: "PLANT-001"));
        repository.Add(
            CreatePlant(farmId: otherFarmId, plantCode: "PLANT-002"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.True(await repository.CodeExistsAsync(
            farmId,
            " plant-001 "));
        Assert.False(await repository.CodeExistsAsync(
            otherFarmId,
            "PLANT-001"));
        Assert.False(await repository.CodeExistsAsync(
            farmId,
            "PLANT-002"));
    }

    [Theory]
    [InlineData(PlantLifecycleStatus.Active)]
    [InlineData(PlantLifecycleStatus.Missing)]
    [InlineData(PlantLifecycleStatus.Removed)]
    [InlineData(PlantLifecycleStatus.Dead)]
    [InlineData(PlantLifecycleStatus.Inactive)]
    public async Task CodeExistsIncludesEveryLifecycleStatus(
        PlantLifecycleStatus lifecycleStatus)
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);
        var farmId = Guid.NewGuid();
        var plant = CreatePlant(farmId: farmId);
        SetLifecycleStatus(plant, lifecycleStatus);
        repository.Add(plant);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.True(await repository.CodeExistsAsync(
            farmId,
            plant.PlantCode));
    }

    [Theory]
    [InlineData(PlantLifecycleStatus.Active, true)]
    [InlineData(PlantLifecycleStatus.Missing, true)]
    [InlineData(PlantLifecycleStatus.Removed, false)]
    [InlineData(PlantLifecycleStatus.Dead, false)]
    [InlineData(PlantLifecycleStatus.Inactive, false)]
    public async Task GridPositionExistsUsesLifecycleRule(
        PlantLifecycleStatus lifecycleStatus,
        bool expected)
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);
        var zoneId = Guid.NewGuid();
        var plant = CreatePlant(
            zoneId: zoneId,
            mapVersionId: Guid.NewGuid(),
            rowIndex: 2,
            columnIndex: 3);
        SetLifecycleStatus(plant, lifecycleStatus);
        repository.Add(plant);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var actual = await repository.GridPositionExistsAsync(
            zoneId,
            rowIndex: 2,
            columnIndex: 3);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GridPositionExistsUsesZoneScope()
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);
        var zoneId = Guid.NewGuid();
        var otherZoneId = Guid.NewGuid();
        repository.Add(
            CreatePlant(
                zoneId: zoneId,
                mapVersionId: Guid.NewGuid(),
                rowIndex: 2,
                columnIndex: 3));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.True(await repository.GridPositionExistsAsync(
            zoneId,
            rowIndex: 2,
            columnIndex: 3));
        Assert.False(await repository.GridPositionExistsAsync(
            otherZoneId,
            rowIndex: 2,
            columnIndex: 3));
        Assert.False(await repository.GridPositionExistsAsync(
            zoneId,
            rowIndex: 2,
            columnIndex: 4));
    }

    [Theory]
    [InlineData(0, 1, "rowIndex")]
    [InlineData(-1, 1, "rowIndex")]
    [InlineData(1, 0, "columnIndex")]
    [InlineData(1, -1, "columnIndex")]
    public async Task GridPositionExistsRejectsNonPositiveIndex(
        int rowIndex,
        int columnIndex,
        string expectedParameterName)
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => repository.GridPositionExistsAsync(
                Guid.NewGuid(),
                rowIndex,
                columnIndex));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    public async Task GetByIdUsesPlantFarmAndZoneScope()
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var plant = CreatePlant(farmId: farmId, zoneId: zoneId);
        repository.Add(plant);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.NotNull(await repository.GetByIdAsync(
            farmId,
            zoneId,
            plant.Id));
        Assert.Null(await repository.GetByIdAsync(
            Guid.NewGuid(),
            zoneId,
            plant.Id));
        Assert.Null(await repository.GetByIdAsync(
            farmId,
            Guid.NewGuid(),
            plant.Id));
        Assert.Null(await repository.GetByIdAsync(
            farmId,
            zoneId,
            Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdsReturnsOnlyRequestedPlantsInFarmAndZone()
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);
        var farmId = Guid.NewGuid();
        var zoneId = Guid.NewGuid();
        var first = CreatePlant(
            farmId: farmId,
            zoneId: zoneId,
            plantCode: "PLANT-001");
        var second = CreatePlant(
            farmId: farmId,
            zoneId: zoneId,
            plantCode: "PLANT-002");
        var otherZone = CreatePlant(
            farmId: farmId,
            zoneId: Guid.NewGuid(),
            plantCode: "PLANT-003");
        repository.Add(first);
        repository.Add(second);
        repository.Add(otherZone);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByIdsAsync(
            farmId,
            zoneId,
            [first.Id, first.Id, second.Id, otherZone.Id]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, plant => plant.Id == first.Id);
        Assert.Contains(result, plant => plant.Id == second.Id);
    }

    [Fact]
    public async Task GetByIdsReturnsEmptyWithoutQueryingForEmptyInput()
    {
        await using var context = CreateContext();
        var repository = new PlantRepository(context);

        var result = await repository.GetByIdsAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            []);

        Assert.Empty(result);
    }

    [Fact]
    public void PlantModelRetainsCodeAndActiveGridUniqueIndexes()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Plant));
        Assert.NotNull(entityType);

        var codeIndex = Assert.Single(
            entityType.GetIndexes(),
            index => index.GetDatabaseName() == "uq_plants_farm_code");
        Assert.True(codeIndex.IsUnique);
        Assert.Equal(
            [nameof(Plant.FarmId), nameof(Plant.PlantCode)],
            codeIndex.Properties.Select(property => property.Name));

        var gridIndex = Assert.Single(
            entityType.GetIndexes(),
            index => index.GetDatabaseName() ==
                "ux_plants_active_zone_grid_position");
        Assert.True(gridIndex.IsUnique);
        Assert.Equal(
            [nameof(Plant.ZoneId), nameof(Plant.RowIndex), nameof(Plant.ColumnIndex)],
            gridIndex.Properties.Select(property => property.Name));
        Assert.Contains("ACTIVE", gridIndex.GetFilter());
        Assert.Contains("MISSING", gridIndex.GetFilter());
    }

    private static PlantsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlantsDbContext>()
            .UseInMemoryDatabase($"plant-repository-{Guid.NewGuid():N}")
            .UseModel(RepositoryTestModel.Value)
            .Options;

        return new PlantsDbContext(options);
    }

    private static Plant CreatePlant(
        Guid? farmId = null,
        Guid? zoneId = null,
        string plantCode = "PLANT-001",
        Guid? mapVersionId = null,
        int? rowIndex = null,
        int? columnIndex = null) =>
        Plant.RegisterManually(
            Guid.NewGuid(),
            farmId ?? Guid.NewGuid(),
            zoneId ?? Guid.NewGuid(),
            plantCode,
            CreatePoint(),
            mapVersionId,
            rowIndex,
            columnIndex,
            locationAccuracyM: 1m,
            Guid.NewGuid(),
            RegisteredAt);

    private static Point CreatePoint() =>
        new(106.700981, 10.776889)
        {
            SRID = 4326
        };

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

    private static IModel CreateRepositoryTestModel()
    {
        var options =
            new DbContextOptionsBuilder<RepositoryModelDbContext>()
                .UseInMemoryDatabase("plant-repository-model")
                .Options;

        using var context = new RepositoryModelDbContext(options);
        return context.Model;
    }

    private sealed class RepositoryModelDbContext(
        DbContextOptions<RepositoryModelDbContext> options)
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
}
