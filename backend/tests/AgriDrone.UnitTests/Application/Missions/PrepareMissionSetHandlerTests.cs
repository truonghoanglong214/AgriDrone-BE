using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Xunit;

namespace AgriDrone.UnitTests.Application.Missions;

public sealed class PrepareMissionSetHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 27, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FirstSurveyCreatesOnlyBaselineMission()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var command = fixture.CreateCommand(
            baselineStart: Now.AddHours(2),
            baselineEnd: Now.AddHours(3));

        var result = await fixture.Handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.ReusedExistingSet);
        Assert.Single(result.Value.Missions);
        Assert.Single(fixture.Repository.Added);
        Assert.Single(fixture.UnitOfWork.Audits);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);

        var baseline = Assert.Single(
            fixture.Repository.Added,
            mission => mission.Purpose == MissionPurpose.BaselineMapping);
        Assert.Equal(MissionStatus.Scheduled, baseline.Status);
        Assert.Null(baseline.SourceMapVersionId);
        Assert.False(baseline.RequiresBaselineCompletion);
        Assert.Equal(fixture.OrderId, baseline.SurveyOrderId);
        Assert.Equal(fixture.ScopeZoneIds, baseline.ScopeZoneIds);
        Assert.Equal(fixture.FarmBoundaryVersionId, baseline.FarmBoundaryVersionId);
    }

    [Fact]
    public async Task PaidServiceIsAddedOnlyAfterBaselinePublicationAndPayment()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var baseline = await fixture.Handler.Handle(
            fixture.CreateCommand(baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)), CancellationToken.None);
        Assert.True(baseline.IsSuccess);
        Assert.Single(fixture.Repository.Added);

        var publishedMapId = Guid.NewGuid();
        fixture.PlanningQuery.Context = fixture.PlanningQuery.Context with
        {
            RequiresBaselineMapping = false,
            CurrentBaseMapVersionId = publishedMapId
        };
        var paid = await fixture.Handler.Handle(
            fixture.CreateCommand(serviceStart: Now.AddHours(4),
                serviceEnd: Now.AddHours(5)), CancellationToken.None);

        Assert.True(paid.IsSuccess);
        Assert.Equal(2, paid.Value.Missions.Count);
        Assert.Equal(2, fixture.Repository.Added.Count);
        var service = Assert.Single(fixture.Repository.Added,
            mission => mission.Purpose == MissionPurpose.PlantHealth);
        Assert.Equal(publishedMapId, service.SourceMapVersionId);
        Assert.Equal(fixture.FarmBoundaryVersionId, service.FarmBoundaryVersionId);
        Assert.False(service.RequiresBaselineCompletion);
        Assert.Equal(MissionStatus.Scheduled, service.Status);
    }

    [Fact]
    public async Task ChangedBoundaryAfterBaselineRequiresRecovery()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var baseline = await fixture.Handler.Handle(
            fixture.CreateCommand(baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)), CancellationToken.None);
        Assert.True(baseline.IsSuccess);

        fixture.PlanningQuery.Context = fixture.PlanningQuery.Context with
        {
            RequiresBaselineMapping = false,
            CurrentBaseMapVersionId = Guid.NewGuid(),
            FarmBoundaryVersionId = Guid.NewGuid()
        };
        var paid = await fixture.Handler.Handle(
            fixture.CreateCommand(serviceStart: Now.AddHours(4),
                serviceEnd: Now.AddHours(5)), CancellationToken.None);

        Assert.True(paid.IsFailure);
        Assert.Equal("MissionPlanning.ExistingMissionSetRequiresRecovery", paid.Error.Code);
        Assert.Single(fixture.Repository.Added);
    }

    [Fact]
    public async Task ChangedZonePlanAfterBaselineRequiresRecovery()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var baseline = await fixture.Handler.Handle(
            fixture.CreateCommand(baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)), CancellationToken.None);
        Assert.True(baseline.IsSuccess);

        fixture.PlanningQuery.Context = fixture.PlanningQuery.Context with
        {
            RequiresBaselineMapping = false,
            CurrentBaseMapVersionId = Guid.NewGuid(),
            ScopeZoneIds = [Guid.NewGuid()]
        };
        var paid = await fixture.Handler.Handle(
            fixture.CreateCommand(serviceStart: Now.AddHours(4),
                serviceEnd: Now.AddHours(5)), CancellationToken.None);

        Assert.True(paid.IsFailure);
        Assert.Equal("MissionPlanning.ExistingMissionSetRequiresRecovery", paid.Error.Code);
        Assert.Single(fixture.Repository.Added);
    }

    [Fact]
    public async Task PublishedBaselineWithoutPaymentStillCannotCreatePaidMission()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var baseline = await fixture.Handler.Handle(
            fixture.CreateCommand(baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)), CancellationToken.None);
        Assert.True(baseline.IsSuccess);

        fixture.PlanningQuery.Context = fixture.PlanningQuery.Context with
        {
            RequiresBaselineMapping = false,
            CurrentBaseMapVersionId = Guid.NewGuid(),
            IsReadyForOperations = false,
            IsEligibleForPlanning = false,
            IsReadyToSchedule = false,
            ReadinessFailureCode = "PaymentNotConfirmed"
        };
        var paid = await fixture.Handler.Handle(
            fixture.CreateCommand(serviceStart: Now.AddHours(4),
                serviceEnd: Now.AddHours(5)), CancellationToken.None);

        Assert.True(paid.IsFailure);
        Assert.Equal("MissionPlanning.OrderNotReady", paid.Error.Code);
        Assert.Single(fixture.Repository.Added);
        Assert.DoesNotContain(fixture.Repository.Added,
            mission => mission.Purpose == MissionPurpose.PlantHealth);
    }

    [Fact]
    public async Task ExistingBaseMapCreatesOnlySelectedServiceMission()
    {
        var fixture = new Fixture(
            requiresBaselineMapping: false,
            selectedService: SurveyServiceType.HarvestReadiness);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                serviceStart: Now.AddHours(2),
                serviceEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var mission = Assert.Single(fixture.Repository.Added);
        Assert.Equal(MissionPurpose.HarvestReadiness, mission.Purpose);
        Assert.Equal(fixture.CurrentBaseMapVersionId, mission.SourceMapVersionId);
        Assert.False(mission.RequiresBaselineCompletion);
        Assert.Equal(MissionStatus.Scheduled, mission.Status);
        Assert.Single(fixture.UnitOfWork.Audits);
    }

    [Fact]
    public async Task RetryReturnsExistingBaselineWithoutWritingAgain()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        fixture.Repository.Seed(fixture.CreateExistingMission(
            MissionPurpose.BaselineMapping,
            Now.AddHours(2), Now.AddHours(3)));

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ReusedExistingSet);
        Assert.Single(result.Value.Missions);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.DroneQueries.AvailabilityCallCount);
    }

    [Fact]
    public async Task BaselineCanBeDraftWithoutEitherAppointmentWindow()
    {
        var fixture = new Fixture(requiresBaselineMapping: true,
            isReadyForOperations: false, isEligibleForPlanning: true,
            isReadyToSchedule: false);
        var command = fixture.CreateCommand();

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Missions);
        Assert.All(result.Value.Missions, mission =>
        {
            Assert.Equal(MissionStatus.Draft, mission.Status);
            Assert.Null(mission.ScheduledAt);
            Assert.Null(mission.ScheduledEndAt);
        });
        Assert.Equal(0, fixture.DroneQueries.AvailabilityCallCount);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task BaselineDraftCanBeScheduledWithoutServiceWindow()
    {
        var fixture = new Fixture(requiresBaselineMapping: true,
            isReadyForOperations: false, isEligibleForPlanning: true,
            isReadyToSchedule: false);
        var draft = await fixture.Handler.Handle(fixture.CreateCommand(),
            CancellationToken.None);
        Assert.True(draft.IsSuccess);
        var missionId = Assert.Single(draft.Value.Missions).MissionId;

        fixture.PlanningQuery.Context = fixture.PlanningQuery.Context with
        {
            IsReadyToSchedule = true
        };
        var scheduled = await fixture.Handler.Handle(
            fixture.CreateCommand(baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)), CancellationToken.None);

        Assert.True(scheduled.IsSuccess);
        Assert.Equal(missionId, Assert.Single(scheduled.Value.Missions).MissionId);
        Assert.Equal(MissionStatus.Scheduled,
            Assert.Single(fixture.Repository.Added).Status);
        Assert.Equal(2, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task FirstSurveySchedulesBaselineWithoutCreatingPaidServiceDraft()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(MissionStatus.Scheduled,
            Assert.Single(result.Value.Missions).Status);
        Assert.DoesNotContain(result.Value.Missions,
            mission => mission.Purpose == MissionPurpose.PlantHealth);
        Assert.Equal(1, fixture.DroneQueries.AvailabilityCallCount);
    }

    [Fact]
    public async Task PaidDraftIsScheduledWhenAppointmentIsConfirmed()
    {
        var fixture = new Fixture(requiresBaselineMapping: false,
            isReadyForOperations: false, isEligibleForPlanning: true,
            isReadyToSchedule: false);
        var prepared = await fixture.Handler.Handle(fixture.CreateCommand(),
            CancellationToken.None);
        Assert.True(prepared.IsSuccess);
        Assert.Equal(MissionStatus.Draft, Assert.Single(fixture.Repository.Added).Status);

        fixture.PlanningQuery.Context = fixture.PlanningQuery.Context with
        {
            IsReadyToSchedule = true
        };
        var scheduled = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(scheduled.IsSuccess);
        Assert.Single(fixture.Repository.Added);
        Assert.Equal(MissionStatus.Scheduled, Assert.Single(scheduled.Value.Missions).Status);
        Assert.Equal(1, fixture.DroneQueries.AvailabilityCallCount);
        Assert.Equal(2, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnpaidRepeatOrderCannotPrepareOrReserveDrone()
    {
        var fixture = new Fixture(requiresBaselineMapping: false,
            isReadyForOperations: false, isEligibleForPlanning: false,
            isReadyToSchedule: false);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.OrderNotReady", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.DroneQueries.AvailabilityCallCount);
    }

    [Fact]
    public async Task RetryIsRejectedWhenOrderIsNoLongerReady()
    {
        var fixture = new Fixture(
            requiresBaselineMapping: false,
            isReadyForOperations: false);
        fixture.Repository.Seed(fixture.CreateExistingMission(
            MissionPurpose.PlantHealth,
            Now.AddHours(2),
            Now.AddHours(3)));

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.OrderNotReady", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task CancelledMissionCannotBeReturnedAsPreparedSet()
    {
        var fixture = new Fixture(requiresBaselineMapping: false);
        var mission = fixture.CreateExistingMission(
            MissionPurpose.PlantHealth,
            Now.AddHours(2),
            Now.AddHours(3));
        mission.Cancel(Now.AddMinutes(1));
        fixture.Repository.Seed(mission);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "MissionPlanning.ExistingMissionSetRequiresRecovery",
            result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task StaleBaseMapMissionCannotBeReturnedAsPreparedSet()
    {
        var fixture = new Fixture(requiresBaselineMapping: false);
        fixture.Repository.Seed(fixture.CreateExistingMission(
            MissionPurpose.PlantHealth,
            Now.AddHours(2),
            Now.AddHours(3),
            sourceMapVersionId: Guid.NewGuid()));

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "MissionPlanning.ExistingMissionSetRequiresRecovery",
            result.Error.Code);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConcurrentWinnerIsReturnedAsAnIdempotentSuccess()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        var winner = new[] { fixture.CreateExistingMission(
            MissionPurpose.BaselineMapping,
            Now.AddHours(2), Now.AddHours(3)) };
        fixture.UnitOfWork.BeforeSaveFailure = () =>
            fixture.Repository.ReplaceStored(winner);
        fixture.UnitOfWork.SaveException = new MissionSetConflictException(
            new InvalidOperationException("Unique order-purpose conflict."));

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ReusedExistingSet);
        Assert.Equal(
            winner.Select(mission => mission.Id).Order(),
            result.Value.Missions.Select(mission => mission.MissionId).Order());
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConcurrentDroneReservationReturnsAvailabilityConflict()
    {
        var fixture = new Fixture(requiresBaselineMapping: false);
        fixture.UnitOfWork.SaveException = new MissionScheduleConflictException(
            new InvalidOperationException("Overlapping drone schedule."));

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.DroneNotAvailable", result.Error.Code);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ConcurrentCancelledWinnerIsNotReturnedAsSuccessfulRetry()
    {
        var fixture = new Fixture(requiresBaselineMapping: false);
        var winner = fixture.CreateExistingMission(
            MissionPurpose.PlantHealth,
            Now.AddHours(2),
            Now.AddHours(3));
        winner.Cancel(Now.AddMinutes(1));
        fixture.UnitOfWork.BeforeSaveFailure = () =>
            fixture.Repository.ReplaceStored([winner]);
        fixture.UnitOfWork.SaveException = new MissionSetConflictException(
            new InvalidOperationException("Unique order-purpose conflict."));

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.ConcurrentPreparation", result.Error.Code);
    }

    [Fact]
    public async Task OrderNotReadyFailsBeforeDroneOrMissionWrites()
    {
        var fixture = new Fixture(
            requiresBaselineMapping: false,
            isReadyForOperations: false);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                serviceStart: Now.AddHours(2),
                serviceEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.OrderNotReady", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
        Assert.Equal(0, fixture.DroneQueries.AvailabilityCallCount);
    }

    [Fact]
    public async Task MissingBe1PlanningAdapterReturnsStableFailure()
    {
        var fixture = new Fixture(
            requiresBaselineMapping: false,
            planningQuery: new UnavailablePlanningQuery());

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                serviceStart: Now.AddHours(2),
                serviceEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "MissionPlanning.OrderContextUnavailable",
            result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnassignedOrUnqualifiedManagerCannotPrepareMissions()
    {
        var fixture = new Fixture(
            requiresBaselineMapping: false,
            managerAllowed: false);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                serviceStart: Now.AddHours(2),
                serviceEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.FarmAccessDenied", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task UnavailableDroneRejectsWholeMissionSet()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);
        fixture.DroneQueries.IsAvailable = false;

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                baselineStart: Now.AddHours(2),
                baselineEnd: Now.AddHours(3)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.DroneNotAvailable", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
        Assert.Equal(0, fixture.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ScheduleOutsideConfirmedAppointmentIsRejected()
    {
        var fixture = new Fixture(requiresBaselineMapping: false);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(
                serviceStart: Now.AddMinutes(30),
                serviceEnd: Now.AddHours(2)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "MissionPlanning.ScheduleOutsideAppointment",
            result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
    }

    [Fact]
    public async Task ServiceWindowIsRejectedWhileBaselineIsRequired()
    {
        var fixture = new Fixture(requiresBaselineMapping: true);

        var result = await fixture.Handler.Handle(
            fixture.CreateCommand(Now.AddHours(2), Now.AddHours(3),
                Now.AddHours(4), Now.AddHours(5)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.ServiceWindowNotAllowed", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
    }

    [Fact]
    public async Task ScheduledPaidServiceRequiresServiceWindow()
    {
        var fixture = new Fixture(requiresBaselineMapping: false);
        var result = await fixture.Handler.Handle(fixture.CreateCommand(),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MissionPlanning.ServiceWindowRequired", result.Error.Code);
        Assert.Empty(fixture.Repository.Added);
    }

    private sealed class Fixture
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _farmId = Guid.NewGuid();
        private readonly Guid _actorId = Guid.NewGuid();
        private readonly Guid _operationId = Guid.NewGuid();

        public Fixture(
            bool requiresBaselineMapping,
            SurveyServiceType selectedService = SurveyServiceType.PlantHealth,
            bool isReadyForOperations = true,
            bool? isEligibleForPlanning = null,
            bool? isReadyToSchedule = null,
            bool managerAllowed = true,
            ISurveyOrderMissionPlanningQuery? planningQuery = null)
        {
            OrderId = Guid.NewGuid();
            DroneId = Guid.NewGuid();
            CurrentBaseMapVersionId = requiresBaselineMapping
                ? null
                : Guid.NewGuid();
            ScopeZoneIds = [Guid.NewGuid(), Guid.NewGuid()];
            Repository = new RecordingMissionRepository();
            UnitOfWork = new RecordingMissionsUnitOfWork();
            DroneQueries = new RecordingDroneQueries(DroneId);

            var context = new SurveyOrderMissionPlanningContext(
                OrderId,
                _tenantId,
                _farmId,
                selectedService,
                requiresBaselineMapping,
                CurrentBaseMapVersionId,
                ScopeZoneIds,
                Now.AddHours(1),
                Now.AddHours(8),
                isReadyForOperations,
                isReadyForOperations ? null : "Appointment is not confirmed",
                isEligibleForPlanning ?? isReadyForOperations,
                isReadyToSchedule ?? isReadyForOperations,
                FarmBoundaryVersionId);

            PlanningQuery = new StubPlanningQuery(context);
            Handler = new PrepareMissionSetCommandHandler(
                planningQuery ?? PlanningQuery,
                managerAllowed
                    ? new AllowedManagerAccessService(_tenantId, _farmId)
                    : new DeniedManagerAccessService(),
                Repository,
                DroneQueries,
                UnitOfWork,
                new RecordingAuditWriter(),
                new TestExecutionContext(_actorId),
                new FixedTimeProvider(Now));
        }

        public Guid OrderId { get; }

        public Guid DroneId { get; }

        public Guid? CurrentBaseMapVersionId { get; }

        public Guid[] ScopeZoneIds { get; }

        public Guid FarmBoundaryVersionId { get; } = Guid.NewGuid();

        public RecordingMissionRepository Repository { get; }

        public RecordingMissionsUnitOfWork UnitOfWork { get; }

        public RecordingDroneQueries DroneQueries { get; }

        public PrepareMissionSetCommandHandler Handler { get; }

        public StubPlanningQuery PlanningQuery { get; }

        public PrepareMissionSetCommand CreateCommand(
            DateTimeOffset? serviceStart = null,
            DateTimeOffset? serviceEnd = null,
            DateTimeOffset? baselineStart = null,
            DateTimeOffset? baselineEnd = null) =>
            new(
                OrderId,
                DroneId,
                _operationId,
                serviceStart.HasValue && serviceEnd.HasValue
                    ? new MissionScheduleWindow(serviceStart.Value, serviceEnd.Value)
                    : null,
                baselineStart.HasValue && baselineEnd.HasValue
                    ? new MissionScheduleWindow(
                        baselineStart.Value,
                        baselineEnd.Value)
                    : null);

        public DroneMission CreateExistingMission(
            MissionPurpose purpose,
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            bool requiresBaselineCompletion = false,
            Guid? sourceMapVersionId = null)
        {
            using var parameters = JsonDocument.Parse("{}");
            var mission = DroneMission.CreateOrderBound(
                OrderId,
                _tenantId,
                _farmId,
                ScopeZoneIds,
                FarmBoundaryVersionId,
                DroneId,
                _actorId,
                $"EXISTING-{purpose}",
                purpose,
                _operationId,
                purpose == MissionPurpose.BaselineMapping ||
                requiresBaselineCompletion
                    ? null
                    : sourceMapVersionId ?? CurrentBaseMapVersionId,
                requiresBaselineCompletion,
                parameters,
                _actorId,
                Now);
            mission.Schedule(startAt, endAt, Now);
            return mission;
        }
    }

    private sealed class StubPlanningQuery(
        SurveyOrderMissionPlanningContext context)
        : ISurveyOrderMissionPlanningQuery
    {
        public SurveyOrderMissionPlanningContext Context { get; set; } = context;

        public SurveyOrderMissionPlanningContext? ServiceContext { get; set; }

        public Task<SurveyOrderMissionPlanningContext?> GetAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyOrderMissionPlanningContext?>(Context);

        public Task<SurveyOrderMissionPlanningContext?> GetForPurposeAsync(
            Guid surveyOrderId, MissionPurpose purpose,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SurveyOrderMissionPlanningContext?>(
                ServiceContext ?? Context);
    }

    private sealed class UnavailablePlanningQuery
        : ISurveyOrderMissionPlanningQuery
    {
        public Task<SurveyOrderMissionPlanningContext?> GetAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<SurveyOrderMissionPlanningContext?>(
                new SurveyOrderMissionPlanningUnavailableException());
    }

    private sealed class AllowedManagerAccessService(
        Guid tenantId,
        Guid farmId) : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
            Guid requestedFarmId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(farmId, requestedFarmId);
            return Task.FromResult(SystemManagerFarmAccess.Allowed(
                tenantId,
                farmId,
                Guid.NewGuid()));
        }
    }

    private sealed class DeniedManagerAccessService
        : ISystemManagerAccessService
    {
        public Task<SystemManagerFarmAccess> ResolveFarmAccessAsync(
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SystemManagerFarmAccess.Denied(
                "The manager is not assigned or flight-qualified."));
    }

    private sealed class RecordingMissionRepository : IDroneMissionRepository
    {
        private readonly List<DroneMission> _missions = [];

        public List<DroneMission> Added { get; } = [];

        public void Seed(params DroneMission[] missions) =>
            _missions.AddRange(missions);

        public void ReplaceStored(IEnumerable<DroneMission> missions)
        {
            _missions.Clear();
            _missions.AddRange(missions);
        }

        public Task<DroneMission?> GetByIdAsync(
            Guid missionId,
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_missions.SingleOrDefault(mission =>
                mission.Id == missionId &&
                mission.TenantId == tenantId &&
                mission.FarmId == farmId));

        public Task<bool> CodeExistsAsync(
            Guid farmId,
            string missionCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_missions.Any(mission =>
                mission.FarmId == farmId &&
                mission.MissionCode == missionCode));

        public Task<IReadOnlyList<DroneMission>> GetBySurveyOrderIdAsync(
            Guid surveyOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneMission>>(
                _missions
                    .Where(mission => mission.SurveyOrderId == surveyOrderId)
                    .ToArray());

        public void Add(DroneMission mission)
        {
            Added.Add(mission);
            _missions.Add(mission);
        }
    }

    private sealed class RecordingDroneQueries(Guid availableDroneId)
        : IDroneQueries
    {
        public bool IsAvailable { get; set; } = true;

        public int AvailabilityCallCount { get; private set; }

        public Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableAsync(
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default)
        {
            AvailabilityCallCount++;
            IReadOnlyList<AvailableDroneResponse> result = IsAvailable
                ? [new AvailableDroneResponse(
                    availableDroneId,
                    "SYS-01",
                    "Survey Drone",
                    null,
                    null,
                    JsonSerializer.Deserialize<JsonElement>(
                        """{"capabilities":["baseline_mapping","plant_health","harvest_readiness"]}"""),
                    null,
                    null,
                    null,
                    DroneStatus.Available,
                    null)]
                : [];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<DroneRegistryItemResponse>> GetRegistryAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DroneRegistryItemResponse>>([]);
    }

    private sealed class RecordingMissionsUnitOfWork : IMissionsUnitOfWork
    {
        public List<AuditLog> Audits { get; } = [];

        public int SaveCount { get; private set; }

        public Action? BeforeSaveFailure { get; set; }

        public Exception? SaveException { get; set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (SaveException is not null)
            {
                BeforeSaveFailure?.Invoke();
                return Task.FromException<int>(SaveException);
            }

            return Task.FromResult(1);
        }

        public void AddAuditLog(AuditLog auditLog) => Audits.Add(auditLog);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
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
            DateTimeOffset createdAt) =>
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
            throw new Xunit.Sdk.XunitException(
                "Order-bound Missions require tenant-scoped auditing.");
    }

    private sealed class TestExecutionContext(Guid actorId) : IExecutionContext
    {
        public bool IsInitialized => true;
        public Guid? TenantId => null;
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
