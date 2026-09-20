using System.Text.Json;
using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.Modules.Plants.Application.Abstractions.Persistence;
using AgriDrone.Modules.Plants.Application.Features.RegisterPlantManually;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Caching;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;
using Xunit;

namespace AgriDrone.UnitTests.Application.Plants;

public sealed class RegisterPlantManuallyHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid UnknownHealthLevelId = Guid.NewGuid();
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleRegistersPlantWithoutGridAndCreatesChangeEvent()
    {
        var dependencies = CreateDependencies();
        var command = CreateCommand(
            plantCode: " manual-001 ",
            reason: " Found during field inspection. ");

        var result = await dependencies.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var plant = Assert.Single(dependencies.Repository.AddedPlants);
        Assert.Equal("MANUAL-001", plant.PlantCode);
        Assert.Equal(command.FarmId, plant.FarmId);
        Assert.Equal(command.ZoneId, plant.ZoneId);
        Assert.Null(plant.CurrentMapVersionId);
        Assert.Null(plant.RowIndex);
        Assert.Null(plant.ColumnIndex);
        Assert.Equal(UnknownHealthLevelId, plant.CurrentHealthLevelId);
        Assert.Equal(PositionSource.Manual, plant.PositionSource);
        Assert.Equal(PlantLifecycleStatus.Active, plant.LifecycleStatus);
        Assert.Equal(Now, plant.CreatedAt);

        var changeEvent = Assert.Single(
            dependencies.Repository.AddedChangeEvents);
        Assert.Equal(plant.Id, changeEvent.PlantId);
        Assert.Equal(PlantChangeSource.Manual, changeEvent.Source);
        Assert.Equal(ActorId, changeEvent.CreatedBy);
        Assert.Equal(
            "Found during field inspection.",
            changeEvent.Notes);
        Assert.Null(changeEvent.MissionId);
        Assert.Equal(1, dependencies.UnitOfWork.SaveCallCount);
        Assert.Equal(0, dependencies.FarmReferences.MapCheckCallCount);
        Assert.Equal(0, dependencies.Repository.GridCheckCallCount);
        Assert.Equal(0, dependencies.Cache.InvalidationCallCount);

        Assert.Equal(plant.Id, result.Value.PlantId);
        Assert.Equal("MANUAL-001", result.Value.PlantCode);
        Assert.Equal(command.Location.Y, result.Value.Latitude);
        Assert.Equal(command.Location.X, result.Value.Longitude);
    }

    [Fact]
    public async Task HandleRegistersPlantWithConfirmedUnoccupiedGrid()
    {
        var dependencies = CreateDependencies();
        var mapVersionId = Guid.NewGuid();
        var command = CreateCommand(
            mapVersionId: mapVersionId,
            rowIndex: 2,
            columnIndex: 3);

        var result = await dependencies.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var plant = Assert.Single(dependencies.Repository.AddedPlants);
        Assert.Equal(mapVersionId, plant.CurrentMapVersionId);
        Assert.Equal(2, plant.RowIndex);
        Assert.Equal(3, plant.ColumnIndex);
        Assert.Equal(Now, plant.MappedAt);
        Assert.Equal(1, dependencies.FarmReferences.MapCheckCallCount);
        Assert.Equal(1, dependencies.Repository.GridCheckCallCount);
        Assert.Equal(1, dependencies.Cache.InvalidationCallCount);
        Assert.Equal(TenantId, dependencies.Cache.TenantId);
        Assert.Equal(command.FarmId, dependencies.Cache.FarmId);
        Assert.Equal(command.ZoneId, dependencies.Cache.ZoneId);
        Assert.Equal(1, dependencies.Cache.SaveCallCountAtInvalidation);
        Assert.Equal(2, result.Value.RowIndex);
        Assert.Equal(3, result.Value.ColumnIndex);
    }

    [Fact]
    public async Task HandleRejectsMissingActorContext()
    {
        var dependencies = CreateDependencies(hasActor: false);

        var result = await dependencies.Handler.Handle(
            CreateCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("User.ContextRequired", result.Error.Code);
        Assert.Equal(0, dependencies.FarmReferences.ZoneCheckCallCount);
        Assert.Empty(dependencies.Repository.AddedPlants);
    }

    [Fact]
    public async Task HandleRejectsMissingTenantContext()
    {
        var dependencies = CreateDependencies(hasTenant: false);

        var result = await dependencies.Handler.Handle(
            CreateCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Tenant.ContextRequired", result.Error.Code);
        Assert.Equal(0, dependencies.FarmReferences.ZoneCheckCallCount);
    }

    [Fact]
    public async Task HandleTreatsInactiveOrCrossTenantZoneAsNotFound()
    {
        var dependencies = CreateDependencies();
        dependencies.FarmReferences.ActiveZoneExists = false;

        var result = await dependencies.Handler.Handle(
            CreateCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlantRegistration.ZoneNotFound", result.Error.Code);
        Assert.Equal(0, dependencies.Access.ZoneCheckCallCount);
        Assert.Empty(dependencies.Repository.AddedPlants);
    }

    [Fact]
    public async Task HandleRequiresManagerAccessToZone()
    {
        var dependencies = CreateDependencies();
        dependencies.Access.Decision = AccessDecision.Deny(
            AccessDenialReason.FarmRoleInsufficient);

        var result = await dependencies.Handler.Handle(
            CreateCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlantRegistration.AccessDenied", result.Error.Code);
        Assert.Equal(FarmAccessLevel.Manager, dependencies.Access.RequiredAccess);
        Assert.Empty(dependencies.Repository.AddedPlants);
    }

    [Fact]
    public async Task HandleRejectsMapVersionOutsideSelectedZone()
    {
        var dependencies = CreateDependencies();
        dependencies.FarmReferences.ConfirmedMapExists = false;
        var command = CreateCommand(
            mapVersionId: Guid.NewGuid(),
            rowIndex: 1,
            columnIndex: 1);

        var result = await dependencies.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantRegistration.MapVersionNotFound",
            result.Error.Code);
        Assert.Empty(dependencies.Repository.AddedPlants);
    }

    [Fact]
    public async Task HandleRejectsDuplicateCodeBeforeCreatingPlant()
    {
        var dependencies = CreateDependencies();
        dependencies.Repository.CodeExists = true;

        var result = await dependencies.Handler.Handle(
            CreateCommand(plantCode: " duplicate-01 "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantRegistration.CodeAlreadyExists",
            result.Error.Code);
        Assert.Equal("DUPLICATE-01", dependencies.Repository.LastCodeChecked);
        Assert.Empty(dependencies.Repository.AddedPlants);
        Assert.Empty(dependencies.UnitOfWork.AddedAuditLogs);
        Assert.Equal(0, dependencies.AuditWriter.UserActionCallCount);
        Assert.Equal(0, dependencies.Cache.InvalidationCallCount);
        Assert.Equal(0, dependencies.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task HandleRejectsOccupiedGridBeforeCreatingPlant()
    {
        var dependencies = CreateDependencies();
        dependencies.Repository.GridPositionExists = true;
        var command = CreateCommand(
            mapVersionId: Guid.NewGuid(),
            rowIndex: 4,
            columnIndex: 5);

        var result = await dependencies.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantRegistration.GridPositionOccupied",
            result.Error.Code);
        Assert.Empty(dependencies.Repository.AddedPlants);
        Assert.Empty(dependencies.UnitOfWork.AddedAuditLogs);
        Assert.Equal(0, dependencies.AuditWriter.UserActionCallCount);
        Assert.Equal(0, dependencies.Cache.InvalidationCallCount);
        Assert.Equal(0, dependencies.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task HandleFailsClearlyWhenUnknownHealthLevelIsMissing()
    {
        var dependencies = CreateDependencies(hasUnknownHealthLevel: false);

        var result = await dependencies.Handler.Handle(
            CreateCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantRegistration.UnknownHealthLevelMissing",
            result.Error.Code);
        Assert.Empty(dependencies.Repository.AddedPlants);
        Assert.Equal(0, dependencies.UnitOfWork.SaveCallCount);
    }

    [Fact]
    public async Task HandleWritesManualRegistrationAuditInSameSave()
    {
        var dependencies = CreateDependencies();
        var command = CreateCommand(
            plantCode: " audit-001 ",
            mapVersionId: Guid.NewGuid(),
            rowIndex: 2,
            columnIndex: 3,
            reason: " Field verification. ");

        var result = await dependencies.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, dependencies.AuditWriter.UserActionCallCount);
        var audit = Assert.Single(dependencies.UnitOfWork.AddedAuditLogs);
        Assert.Equal(TenantId, audit.TenantId);
        Assert.Equal(command.FarmId, audit.FarmId);
        Assert.Equal(ActorId, audit.ActorId);
        Assert.Equal(
            dependencies.ExecutionContext.CorrelationId,
            audit.CorrelationId);
        Assert.Equal(nameof(Plant), audit.EntityType);
        Assert.Equal(result.Value.PlantId, audit.EntityId);
        Assert.Equal("REGISTER_MANUALLY", audit.Action);
        Assert.Null(audit.OldData);
        Assert.Equal(Now, audit.CreatedAt);
        Assert.Equal(1, dependencies.UnitOfWork.SaveCallCount);

        Assert.NotNull(audit.NewData);
        var data = audit.NewData.RootElement;
        Assert.Equal("AUDIT-001", data.GetProperty("PlantCode").GetString());
        Assert.Equal("Manual", data.GetProperty("PositionSource").GetString());
        Assert.Equal("Active", data.GetProperty("LifecycleStatus").GetString());
        Assert.Equal(
            "Field verification.",
            data.GetProperty("Reason").GetString());
        Assert.Equal(2, data.GetProperty("RowIndex").GetInt32());
        Assert.Equal(3, data.GetProperty("ColumnIndex").GetInt32());
    }

    [Fact]
    public async Task HandleMapsConcurrentCodeConstraintToConflict()
    {
        var dependencies = CreateDependencies();
        dependencies.UnitOfWork.SaveException = CreateDatabaseException(
            PostgresErrorCodes.UniqueViolation,
            "uq_plants_farm_code");

        var result = await dependencies.Handler.Handle(
            CreateCommand(plantCode: " concurrent-01 "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantRegistration.CodeAlreadyExists",
            result.Error.Code);
        Assert.Equal(1, dependencies.UnitOfWork.SaveCallCount);
        Assert.Single(dependencies.Repository.AddedPlants);
        Assert.Single(dependencies.Repository.AddedChangeEvents);
        Assert.Equal(0, dependencies.Cache.InvalidationCallCount);
    }

    [Fact]
    public async Task HandleMapsConcurrentGridConstraintToConflict()
    {
        var dependencies = CreateDependencies();
        dependencies.UnitOfWork.SaveException = CreateDatabaseException(
            PostgresErrorCodes.UniqueViolation,
            "ux_plants_active_zone_grid_position");
        var command = CreateCommand(
            mapVersionId: Guid.NewGuid(),
            rowIndex: 4,
            columnIndex: 5);

        var result = await dependencies.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "PlantRegistration.GridPositionOccupied",
            result.Error.Code);
        Assert.Contains("(4, 5)", result.Error.Description);
        Assert.Equal(1, dependencies.UnitOfWork.SaveCallCount);
        Assert.Equal(0, dependencies.Cache.InvalidationCallCount);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, "uq_unrelated")]
    [InlineData(PostgresErrorCodes.ForeignKeyViolation, "fk_unrelated")]
    public async Task HandleDoesNotHideUnexpectedDatabaseFailure(
        string sqlState,
        string constraintName)
    {
        var dependencies = CreateDependencies();
        var expected = CreateDatabaseException(sqlState, constraintName);
        dependencies.UnitOfWork.SaveException = expected;

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() =>
            dependencies.Handler.Handle(
                CreateCommand(),
                CancellationToken.None));

        Assert.Same(expected, actual);
        Assert.Equal(1, dependencies.UnitOfWork.SaveCallCount);
        Assert.Equal(0, dependencies.Cache.InvalidationCallCount);
    }

    private static HandlerDependencies CreateDependencies(
        bool hasActor = true,
        bool hasTenant = true,
        bool hasUnknownHealthLevel = true)
    {
        var repository = new FakePlantRepository();
        var unitOfWork = new FakePlantsUnitOfWork();
        var farmReferences = new FakeFarmReferenceQuery();
        var healthReferences = new FakeHealthLevelReferenceQuery
        {
            UnknownHealthLevelId = hasUnknownHealthLevel
                ? UnknownHealthLevelId
                : null
        };
        var access = new FakeEffectiveAccessService();
        var auditWriter = new FakeAuditWriter();
        var cache = new FakePlantReferenceCache(
            () => unitOfWork.SaveCallCount);
        var executionContext = new FakeExecutionContext(
            hasTenant ? TenantId : null,
            hasActor ? ActorId : null);
        var handler = new RegisterPlantManuallyHandler(
            repository,
            unitOfWork,
            farmReferences,
            healthReferences,
            access,
            auditWriter,
            cache,
            executionContext,
            new FixedTimeProvider(Now));

        return new HandlerDependencies(
            handler,
            repository,
            unitOfWork,
            farmReferences,
            access,
            auditWriter,
            cache,
            executionContext);
    }

    private static RegisterPlantManuallyCommand CreateCommand(
        string plantCode = "MANUAL-001",
        Guid? mapVersionId = null,
        int? rowIndex = null,
        int? columnIndex = null,
        string reason = "Field inspection") =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            plantCode,
            CreatePoint(),
            mapVersionId,
            rowIndex,
            columnIndex,
            LocationAccuracyM: 1.25m,
            reason);

    private static Point CreatePoint() =>
        new(106.700981, 10.776889)
        {
            SRID = 4326
        };

    private static DbUpdateException CreateDatabaseException(
        string sqlState,
        string constraintName)
    {
        var postgresException = new PostgresException(
            "Database constraint violation.",
            "ERROR",
            "ERROR",
            sqlState,
            constraintName: constraintName);

        return new DbUpdateException(
            "Database update failed.",
            postgresException);
    }

    private sealed record HandlerDependencies(
        RegisterPlantManuallyHandler Handler,
        FakePlantRepository Repository,
        FakePlantsUnitOfWork UnitOfWork,
        FakeFarmReferenceQuery FarmReferences,
        FakeEffectiveAccessService Access,
        FakeAuditWriter AuditWriter,
        FakePlantReferenceCache Cache,
        FakeExecutionContext ExecutionContext);

    private sealed class FakePlantRepository : IPlantRepository
    {
        public bool CodeExists { get; set; }
        public bool GridPositionExists { get; set; }
        public int GridCheckCallCount { get; private set; }
        public string? LastCodeChecked { get; private set; }
        public List<Plant> AddedPlants { get; } = [];
        public List<PlantChangeEvent> AddedChangeEvents { get; } = [];

        public Task<Plant?> GetByIdAsync(
            Guid farmId,
            Guid zoneId,
            Guid plantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Plant?>(null);

        public Task<IReadOnlyList<Plant>> GetByIdsAsync(
            Guid farmId,
            Guid zoneId,
            IReadOnlyCollection<Guid> plantIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Plant>>([]);

        public Task<bool> CodeExistsAsync(
            Guid farmId,
            string plantCode,
            CancellationToken cancellationToken = default)
        {
            LastCodeChecked = plantCode;
            return Task.FromResult(CodeExists);
        }

        public Task<bool> GridPositionExistsAsync(
            Guid zoneId,
            int rowIndex,
            int columnIndex,
            CancellationToken cancellationToken = default)
        {
            GridCheckCallCount++;
            return Task.FromResult(GridPositionExists);
        }

        public void Add(Plant plant) => AddedPlants.Add(plant);

        public void AddChangeEvent(PlantChangeEvent changeEvent) =>
            AddedChangeEvents.Add(changeEvent);
    }

    private sealed class FakePlantsUnitOfWork : IPlantsUnitOfWork
    {
        public int SaveCallCount { get; private set; }
        public Exception? SaveException { get; set; }
        public List<AuditLog> AddedAuditLogs { get; } = [];

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            if (SaveException is not null)
            {
                throw SaveException;
            }

            return Task.FromResult(2);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            await operation(cancellationToken);

        public void AddAuditLog(AuditLog auditLog)
        {
            AddedAuditLogs.Add(auditLog);
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public int UserActionCallCount { get; private set; }

        public void AddUserAction(
            IAuditLogSink sink,
            Guid tenantId,
            Guid? farmId,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt)
        {
            UserActionCallCount++;
            sink.AddAuditLog(AuditLog.ForUserAction(
                tenantId,
                farmId,
                actorId,
                correlationId,
                entityType,
                entityId,
                action,
                oldData,
                newData,
                createdAt));
        }

        public void AddSystemAdminAction(
            IAuditLogSink sink,
            Guid actorId,
            Guid correlationId,
            string entityType,
            Guid entityId,
            string action,
            JsonDocument? oldData,
            JsonDocument? newData,
            DateTimeOffset createdAt) =>
            throw new NotSupportedException();
    }

    private sealed class FakePlantReferenceCache(
        Func<int> getSaveCallCount) : IPlantReferenceCache
    {
        public int InvalidationCallCount { get; private set; }
        public int? SaveCallCountAtInvalidation { get; private set; }
        public Guid? TenantId { get; private set; }
        public Guid? FarmId { get; private set; }
        public Guid? ZoneId { get; private set; }

        public Task<IReadOnlyList<PlantReferenceV1>?> TryGetAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            Guid mapVersionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            Guid mapVersionId,
            IReadOnlyList<PlantReferenceV1> references,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task InvalidateZoneAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            CancellationToken cancellationToken = default)
        {
            InvalidationCallCount++;
            SaveCallCountAtInvalidation = getSaveCallCount();
            TenantId = tenantId;
            FarmId = farmId;
            ZoneId = zoneId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFarmReferenceQuery : IMissionPlanningReferenceQuery
    {
        public bool ActiveZoneExists { get; set; } = true;
        public bool ConfirmedMapExists { get; set; } = true;
        public int ZoneCheckCallCount { get; private set; }
        public int MapCheckCallCount { get; private set; }

        public Task<bool> IsActiveZoneAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            CancellationToken cancellationToken = default)
        {
            ZoneCheckCallCount++;
            return Task.FromResult(ActiveZoneExists);
        }

        public Task<bool> IsConfirmedMapVersionAsync(
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            Guid mapVersionId,
            CancellationToken cancellationToken = default)
        {
            MapCheckCallCount++;
            return Task.FromResult(ConfirmedMapExists);
        }
    }

    private sealed class FakeHealthLevelReferenceQuery
        : IHealthLevelReferenceQuery
    {
        public Guid? UnknownHealthLevelId { get; set; } =
            RegisterPlantManuallyHandlerTests.UnknownHealthLevelId;

        public Task<Guid?> GetActiveUnknownIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(UnknownHealthLevelId);
    }

    private sealed class FakeEffectiveAccessService
        : IEffectiveAccessService
    {
        public AccessDecision Decision { get; set; } = AccessDecision.Allow();
        public int ZoneCheckCallCount { get; private set; }
        public FarmAccessLevel? RequiredAccess { get; private set; }

        public Task<AccessDecision> CheckTenantAsync(
            Guid actorId,
            Guid tenantId,
            TenantAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessDecision> CheckFarmAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AccessDecision> CheckZoneAsync(
            Guid actorId,
            Guid tenantId,
            Guid farmId,
            Guid zoneId,
            FarmAccessLevel requiredAccess,
            CancellationToken cancellationToken = default)
        {
            ZoneCheckCallCount++;
            RequiredAccess = requiredAccess;
            return Task.FromResult(Decision);
        }
    }

    private sealed class FakeExecutionContext(
        Guid? tenantId,
        Guid? actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => tenantId;
        public Guid? ActorId => actorId;
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid? MessageId => null;
        public ExecutionContextSource Source => ExecutionContextSource.Http;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
