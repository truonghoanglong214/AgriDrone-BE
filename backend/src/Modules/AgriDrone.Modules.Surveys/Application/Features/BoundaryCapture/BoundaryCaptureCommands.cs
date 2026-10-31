using AgriDrone.Modules.Surveys.Application.Abstractions.BoundaryCapture;
using AgriDrone.Modules.Surveys.Application.Abstractions.Orders;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedInfrastructure.Auditing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace AgriDrone.Modules.Surveys.Application.Features.BoundaryCapture;

public sealed record StartBoundaryCaptureCommand(
    Guid SurveyOrderId,
    Guid DroneId,
    Guid OperationId,
    uint ExpectedOrderVersion)
    : IRequest<Result<BoundaryCaptureResponse>>;

public sealed record BoundaryCaptureSampleInput(
    Guid ClientOperationId,
    int SequenceNumber,
    decimal Latitude,
    decimal Longitude,
    decimal HorizontalAccuracyMeters,
    DateTimeOffset CapturedAt);

public sealed record SyncBoundaryCaptureSamplesCommand(
    Guid SurveyOrderId,
    Guid SessionId,
    uint ExpectedSessionVersion,
    IReadOnlyCollection<BoundaryCaptureSampleInput> Samples)
    : IRequest<Result<BoundaryCaptureResponse>>;

public sealed record FinalizeBoundaryCaptureCommand(
    Guid SurveyOrderId,
    Guid SessionId,
    string EvidenceReference,
    uint ExpectedOrderVersion,
    uint ExpectedSessionVersion,
    int? ExpectedCurrentBoundaryVersionNumber)
    : IRequest<Result<BoundaryCaptureResponse>>;

public sealed record AbortBoundaryCaptureCommand(
    Guid SurveyOrderId,
    Guid SessionId,
    string Reason,
    uint ExpectedSessionVersion)
    : IRequest<Result<BoundaryCaptureResponse>>;

/// <summary>
/// Typed BE2-to-BE1 handoff. Route geometry remains in BE2; this command records
/// its immutable identity/version and the server-computed safety-inset verdict.
/// </summary>
public sealed record ApproveBaselineRoutePlanCommand(
    Guid SurveyOrderId,
    Guid SessionId,
    Guid FarmBoundaryVersionId,
    Guid RoutePlanId,
    int RoutePlanVersion,
    decimal SafetyInsetMeters,
    bool IsWithinSafetyInset,
    uint ExpectedOrderVersion)
    : IRequest<Result<BoundaryCaptureResponse>>;

public sealed record BoundaryCaptureSampleResponse(
    Guid Id,
    Guid ClientOperationId,
    int SequenceNumber,
    decimal Latitude,
    decimal Longitude,
    decimal HorizontalAccuracyMeters,
    DateTimeOffset CapturedAt,
    DateTimeOffset ReceivedAt);

public sealed record BoundaryCaptureResponse(
    Guid SurveyOrderId,
    Guid SessionId,
    Guid StartOperationId,
    Guid FarmId,
    Guid AppointmentId,
    uint AppointmentVersion,
    Guid SystemManagerProfileId,
    Guid DroneId,
    BoundaryCaptureSessionStatus Status,
    Guid? FarmBoundaryVersionId,
    Guid? BaselineRoutePlanId,
    SurveyOrderStatus OrderStatus,
    uint OrderVersion,
    uint SessionVersion,
    IReadOnlyCollection<BoundaryCaptureSampleResponse> Samples);

internal abstract class BoundaryCaptureHandlerBase(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    IBoundaryCaptureRepository repository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
{
    protected ISurveyBoundaryVerificationUnitOfWork UnitOfWork { get; } = unitOfWork;
    protected IBoundaryCaptureRepository Repository { get; } = repository;
    protected ISurveyBoundaryVerificationOutbox Outbox { get; } = outbox;
    protected IAuditWriter AuditWriter { get; } = auditWriter;
    protected IExecutionContext ExecutionContext { get; } = executionContext;
    protected TimeProvider TimeProvider { get; } = timeProvider;

    protected bool TryGetActor(out Guid actorId)
    {
        actorId = ExecutionContext.ActorId ?? Guid.Empty;
        return ExecutionContext.IsInitialized &&
               actorId != Guid.Empty &&
               ExecutionContext.CorrelationId != Guid.Empty;
    }

    protected static BoundaryCaptureResponse ToResponse(
        SurveyOrder order,
        BoundaryCaptureSession session) => new(
            order.Id,
            session.Id,
            session.StartOperationId,
            session.FarmId,
            session.AppointmentId,
            session.AppointmentVersion,
            session.SystemManagerProfileId,
            session.DroneId,
            session.Status,
            session.FarmBoundaryVersionId,
            order.BaselineRoutePlanId,
            order.Status,
            order.Version,
            session.Version,
            session.Samples
                .OrderBy(item => item.SequenceNumber)
                .Select(item => new BoundaryCaptureSampleResponse(
                    item.Id,
                    item.ClientOperationId,
                    item.SequenceNumber,
                    item.Latitude,
                    item.Longitude,
                    item.HorizontalAccuracyMeters,
                    item.CapturedAt,
                    item.ReceivedAt))
                .ToArray());

    protected void RecordChange(
        SurveyOrder order,
        BoundaryCaptureSession session,
        Guid actorId,
        string changeType,
        DateTimeOffset changedAt)
    {
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            captureSessionId = session.Id,
            captureStatus = session.Status.ToString(),
            orderStatus = order.Status.ToString(),
            boundaryId = session.FarmBoundaryVersionId,
            routePlanId = order.BaselineRoutePlanId,
            sampleCount = session.Samples.Count
        });
        AuditWriter.AddUserAction(
            UnitOfWork,
            order.TenantId,
            order.FarmId,
            actorId,
            ExecutionContext.CorrelationId,
            nameof(BoundaryCaptureSession),
            session.Id,
            changeType,
            oldData: null,
            newData,
            changedAt);

        var payload = new SurveyBoundaryCaptureChangedV1(
            order.Id,
            session.Id,
            order.TenantId,
            order.FarmId,
            session.AppointmentId,
            session.DroneId,
            session.Status.ToString(),
            session.FarmBoundaryVersionId,
            order.BaselineRoutePlanId,
            order.Status.ToString(),
            session.Samples.Count,
            changeType,
            changedAt);
        Outbox.Add(
            IntegrationEventEnvelopeFactory.Create(
                IntegrationEventDescriptors.SurveyBoundaryCaptureChangedV1,
                Guid.CreateVersion7(),
                ExecutionContext.CorrelationId,
                order.TenantId,
                actorId,
                changedAt,
                payload),
            order.Id.ToString("D"));
    }

    protected static Result<BoundaryCaptureResponse> Conflict() =>
        Result.Failure<BoundaryCaptureResponse>(
            BoundaryCaptureError.PersistenceConflict());
}

internal sealed class StartBoundaryCaptureCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    IBoundaryCaptureRepository repository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : BoundaryCaptureHandlerBase(unitOfWork, repository, outbox, auditWriter, executionContext, timeProvider),
      IRequestHandler<StartBoundaryCaptureCommand, Result<BoundaryCaptureResponse>>
{
    public async Task<Result<BoundaryCaptureResponse>> Handle(
        StartBoundaryCaptureCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId))
        {
            return Result.Failure<BoundaryCaptureResponse>(
                BoundaryCaptureError.Forbidden());
        }

        var now = TimeProvider.GetUtcNow();
        try
        {
            return await UnitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var state = await Repository.GetForStartAsync(
                    command.SurveyOrderId,
                    actorId,
                    command.DroneId,
                    command.OperationId,
                    now,
                    token);
                if (state.IsFailure)
                {
                    return Result.Failure<BoundaryCaptureResponse>(state.Error);
                }

                try
                {
                    state.Value.Order.EnsureVersion(command.ExpectedOrderVersion);
                    if (state.Value.ExistingSession is not null)
                    {
                        if (state.Value.ExistingSession.DroneId != command.DroneId)
                        {
                            return Result.Failure<BoundaryCaptureResponse>(
                                BoundaryCaptureError.InvalidState(
                                    "A start operation ID cannot be reused with a different drone."));
                        }

                        return Result.Success(ToResponse(
                            state.Value.Order,
                            state.Value.ExistingSession));
                    }

                    var session = BoundaryCaptureSession.Start(
                        state.Value.Order.Id,
                        command.OperationId,
                        state.Value.Order.FarmId,
                        state.Value.Appointment.Id,
                        state.Value.Appointment.Version,
                        state.Value.SystemManagerProfileId,
                        command.DroneId,
                        actorId,
                        now);
                    Repository.Add(session);
                    RecordChange(order: state.Value.Order, session, actorId,
                        "START_BOUNDARY_CAPTURE", now);
                    await UnitOfWork.FlushAsync(token);
                    return Result.Success(ToResponse(state.Value.Order, session));
                }
                catch (Exception exception) when (exception is
                    SurveyDomainException or ArgumentException or InvalidOperationException)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        BoundaryCaptureError.InvalidState(exception.Message));
                }
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict();
        }
    }
}

internal sealed class SyncBoundaryCaptureSamplesCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    IBoundaryCaptureRepository repository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : BoundaryCaptureHandlerBase(unitOfWork, repository, outbox, auditWriter, executionContext, timeProvider),
      IRequestHandler<SyncBoundaryCaptureSamplesCommand, Result<BoundaryCaptureResponse>>
{
    public async Task<Result<BoundaryCaptureResponse>> Handle(
        SyncBoundaryCaptureSamplesCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId))
        {
            return Result.Failure<BoundaryCaptureResponse>(BoundaryCaptureError.Forbidden());
        }

        if (command.Samples.Count == 0)
        {
            return Result.Failure<BoundaryCaptureResponse>(
                BoundaryCaptureError.InvalidPolygon("At least one sample is required."));
        }

        var now = TimeProvider.GetUtcNow();
        try
        {
            return await UnitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var state = await Repository.GetForMutationAsync(
                    command.SurveyOrderId,
                    command.SessionId,
                    actorId,
                    now,
                    token);
                if (state.IsFailure)
                {
                    return Result.Failure<BoundaryCaptureResponse>(state.Error);
                }

                var session = state.Value.Session;
                if (!state.Value.IsAppointmentCurrent)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        BoundaryCaptureError.AppointmentWindowClosed());
                }


                if (session.Version != command.ExpectedSessionVersion)
                {
                    return Conflict();
                }

                try
                {
                    var versionBeforeSync = session.Version;
                    foreach (var sample in command.Samples.OrderBy(item => item.SequenceNumber))
                    {
                        session.AddSample(
                            sample.ClientOperationId,
                            sample.SequenceNumber,
                            sample.Latitude,
                            sample.Longitude,
                            sample.HorizontalAccuracyMeters,
                            sample.CapturedAt,
                            now);
                    }

                    if (session.Version == versionBeforeSync)
                    {
                        return Result.Success(ToResponse(
                            state.Value.Order,
                            session));
                    }

                    RecordChange(state.Value.Order, session, actorId,
                        "SYNC_BOUNDARY_CAPTURE_SAMPLES", now);
                    await UnitOfWork.FlushAsync(token);
                    return Result.Success(ToResponse(state.Value.Order, session));
                }
                catch (Exception exception) when (exception is
                    ArgumentException or InvalidOperationException)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        BoundaryCaptureError.InvalidState(exception.Message));
                }
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict();
        }
    }
}

internal sealed class FinalizeBoundaryCaptureCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    IBoundaryCaptureRepository repository,
    ISurveyOrderBoundaryRepository boundaryRepository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : BoundaryCaptureHandlerBase(unitOfWork, repository, outbox, auditWriter, executionContext, timeProvider),
      IRequestHandler<FinalizeBoundaryCaptureCommand, Result<BoundaryCaptureResponse>>
{
    public async Task<Result<BoundaryCaptureResponse>> Handle(
        FinalizeBoundaryCaptureCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId))
        {
            return Result.Failure<BoundaryCaptureResponse>(BoundaryCaptureError.Forbidden());
        }

        var now = TimeProvider.GetUtcNow();
        try
        {
            return await UnitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var state = await Repository.GetForMutationAsync(
                    command.SurveyOrderId,
                    command.SessionId,
                    actorId,
                    now,
                    token);
                if (state.IsFailure)
                {
                    return Result.Failure<BoundaryCaptureResponse>(state.Error);
                }

                var order = state.Value.Order;
                var session = state.Value.Session;
                if (!state.Value.IsAppointmentCurrent)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        BoundaryCaptureError.AppointmentWindowClosed());
                }


                if (state.Value.CurrentBoundaryVersionNumber !=
                    command.ExpectedCurrentBoundaryVersionNumber)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        SurveyBoundaryVerificationError.BoundaryVersionConflict());
                }

                Polygon polygon;
                try
                {
                    order.EnsureVersion(command.ExpectedOrderVersion);
                    polygon = CreatePolygon(session.Samples);
                }
                catch (Exception exception) when (exception is
                    ArgumentException or InvalidOperationException or SurveyDomainException)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        BoundaryCaptureError.InvalidPolygon(exception.Message));
                }

                var boundary = await boundaryRepository.StageApprovedBoundaryAsync(
                    order,
                    polygon,
                    command.ExpectedCurrentBoundaryVersionNumber,
                    actorId,
                    "Finalized ordered drone-GNSS boundary capture",
                    command.EvidenceReference,
                    now,
                    token);
                if (boundary.IsFailure)
                {
                    return Result.Failure<BoundaryCaptureResponse>(boundary.Error);
                }

                try
                {
                    session.Finalize(
                        boundary.Value.FarmBoundaryId,
                        command.EvidenceReference,
                        actorId,
                        now,
                        command.ExpectedSessionVersion);
                    order.RecordBoundaryCapture(
                        session.Id,
                        boundary.Value.FarmBoundaryId,
                        actorId,
                        now);
                    RecordChange(order, session, actorId,
                        "FINALIZE_BOUNDARY_CAPTURE", now);
                    await UnitOfWork.FlushAsync(token);
                    return Result.Success(ToResponse(order, session));
                }
                catch (Exception exception) when (exception is
                    ArgumentException or InvalidOperationException or SurveyDomainException)
                {
                    return Result.Failure<BoundaryCaptureResponse>(
                        BoundaryCaptureError.InvalidState(exception.Message));
                }
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict();
        }
    }

    private static Polygon CreatePolygon(
        IReadOnlyCollection<BoundaryCaptureSample> samples)
    {
        var ordered = samples.OrderBy(item => item.SequenceNumber).ToArray();
        if (ordered.Length < 3)
        {
            throw new InvalidOperationException(
                "At least three ordered GNSS samples are required.");
        }

        var coordinates = ordered
            .Select(item => new Coordinate(
                decimal.ToDouble(item.Longitude),
                decimal.ToDouble(item.Latitude)))
            .ToList();
        if (coordinates.DistinctBy(item => (item.X, item.Y)).Count() < 3)
        {
            throw new InvalidOperationException(
                "At least three distinct GNSS points are required.");
        }

        coordinates.Add(coordinates[0].Copy());
        var polygon = new GeometryFactory(
            new PrecisionModel(),
            4326).CreatePolygon(coordinates.ToArray());
        if (!polygon.IsValid || polygon.IsEmpty || polygon.Area <= 0)
        {
            throw new InvalidOperationException(
                "Ordered GNSS samples do not form a valid simple polygon.");
        }

        return polygon;
    }
}

internal sealed class AbortBoundaryCaptureCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    IBoundaryCaptureRepository repository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : BoundaryCaptureHandlerBase(unitOfWork, repository, outbox, auditWriter, executionContext, timeProvider),
      IRequestHandler<AbortBoundaryCaptureCommand, Result<BoundaryCaptureResponse>>
{
    public async Task<Result<BoundaryCaptureResponse>> Handle(
        AbortBoundaryCaptureCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId))
        {
            return Result.Failure<BoundaryCaptureResponse>(BoundaryCaptureError.Forbidden());
        }

        var now = TimeProvider.GetUtcNow();
        return await UnitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var state = await Repository.GetForMutationAsync(
                command.SurveyOrderId,
                command.SessionId,
                actorId,
                now,
                token);
            if (state.IsFailure)
            {
                return Result.Failure<BoundaryCaptureResponse>(state.Error);
            }

            try
            {
                state.Value.Session.Abort(
                    command.Reason,
                    actorId,
                    now,
                    command.ExpectedSessionVersion);
                RecordChange(state.Value.Order, state.Value.Session, actorId,
                    "ABORT_BOUNDARY_CAPTURE", now);
                await UnitOfWork.FlushAsync(token);
                return Result.Success(ToResponse(
                    state.Value.Order,
                    state.Value.Session));
            }
            catch (InvalidOperationException exception)
            {
                return Result.Failure<BoundaryCaptureResponse>(
                    BoundaryCaptureError.InvalidState(exception.Message));
            }
        }, cancellationToken);
    }
}

internal sealed class ApproveBaselineRoutePlanCommandHandler(
    ISurveyBoundaryVerificationUnitOfWork unitOfWork,
    IBoundaryCaptureRepository repository,
    ISurveyBoundaryVerificationOutbox outbox,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : BoundaryCaptureHandlerBase(unitOfWork, repository, outbox, auditWriter, executionContext, timeProvider),
      IRequestHandler<ApproveBaselineRoutePlanCommand, Result<BoundaryCaptureResponse>>
{
    public async Task<Result<BoundaryCaptureResponse>> Handle(
        ApproveBaselineRoutePlanCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId))
        {
            return Result.Failure<BoundaryCaptureResponse>(BoundaryCaptureError.Forbidden());
        }

        if (!command.IsWithinSafetyInset)
        {
            return Result.Failure<BoundaryCaptureResponse>(
                BaselineRouteError.OutsideSafetyInset());
        }

        var now = TimeProvider.GetUtcNow();
        return await UnitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var state = await Repository.GetForMutationAsync(
                command.SurveyOrderId,
                command.SessionId,
                actorId,
                now,
                token);
            if (state.IsFailure)
            {
                return Result.Failure<BoundaryCaptureResponse>(state.Error);
            }

            var order = state.Value.Order;
            var session = state.Value.Session;
            if (!state.Value.IsAppointmentCurrent)
            {
                return Result.Failure<BoundaryCaptureResponse>(
                    BoundaryCaptureError.AppointmentWindowClosed());
            }


            if (session.Status != BoundaryCaptureSessionStatus.Finalized ||
                session.FarmBoundaryVersionId != command.FarmBoundaryVersionId ||
                order.FarmBoundaryVersionId != command.FarmBoundaryVersionId ||
                order.BoundaryCaptureSessionId != command.SessionId)
            {
                return Result.Failure<BoundaryCaptureResponse>(
                    BaselineRouteError.StaleReference());
            }

            try
            {
                order.EnsureVersion(command.ExpectedOrderVersion);
                var route = BaselineRoutePlanReference.RecordGenerated(
                    order.Id,
                    session.Id,
                    command.FarmBoundaryVersionId,
                    command.RoutePlanId,
                    command.RoutePlanVersion,
                    command.SafetyInsetMeters,
                    command.IsWithinSafetyInset,
                    now);
                route.Approve(actorId, now);
                Repository.Add(route);
                order.ApproveBaselineRoute(
                    command.RoutePlanId,
                    session.Id,
                    command.FarmBoundaryVersionId,
                    now);
                RecordChange(order, session, actorId,
                    "APPROVE_BASELINE_ROUTE", now);
                await UnitOfWork.FlushAsync(token);
                return Result.Success(ToResponse(order, session));
            }
            catch (Exception exception) when (exception is
                ArgumentException or InvalidOperationException or SurveyDomainException)
            {
                return Result.Failure<BoundaryCaptureResponse>(
                    BoundaryCaptureError.InvalidState(exception.Message));
            }
        }, cancellationToken);
    }
}
