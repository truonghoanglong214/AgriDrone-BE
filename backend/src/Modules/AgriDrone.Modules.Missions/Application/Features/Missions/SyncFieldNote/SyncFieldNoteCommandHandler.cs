using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;

internal sealed class SyncFieldNoteCommandHandler(
    IDroneMissionRepository missions,
    IMissionFieldNoteRepository notes,
    ISystemManagerAccessService managerAccess,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider clock)
    : IRequestHandler<SyncFieldNoteCommand, Result<SyncFieldNoteResult>>
{
    public async Task<Result<SyncFieldNoteResult>> Handle(
        SyncFieldNoteCommand request, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return Result.Failure<SyncFieldNoteResult>(MissionError.CurrentUserRequired());

        var access = await managerAccess.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
            return Result.Failure<SyncFieldNoteResult>(AppError.Forbidden(
                "MissionFieldNote.FarmAccessDenied",
                "The manager is not assigned and flight-qualified for this Farm."));

        var mission = await missions.GetByIdAsync(request.MissionId, tenantId,
            request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<SyncFieldNoteResult>(MissionError.NotFound(request.MissionId));
        if (mission.SurveyOrderId is null)
            return Result.Failure<SyncFieldNoteResult>(AppError.Conflict(
                "MissionFieldNote.MissionNotOrderBound",
                "Field note sync requires an order-bound Mission."));

        var existing = await notes.GetByOperationIdAsync(tenantId, request.FarmId,
            request.MissionId, request.OperationId, cancellationToken);
        if (existing is not null)
            return MatchExisting(existing, actorId, request);

        if (mission.Status is MissionStatus.Draft or MissionStatus.Cancelled)
            return Result.Failure<SyncFieldNoteResult>(AppError.Conflict(
                "MissionFieldNote.InvalidStatus",
                $"Mission in status '{mission.Status}' cannot accept a new field note."));

        var now = clock.GetUtcNow();
        if (request.ObservedAt < mission.CreatedAt.AddMinutes(-5) ||
            (mission.Status == MissionStatus.Completed &&
             request.ObservedAt > mission.UpdatedAt))
            return Result.Failure<SyncFieldNoteResult>(AppError.Validation(
                "MissionFieldNote.OutsideMissionTimeline",
                "ObservedAt must refer to this Mission's active timeline."));
        if (request.ObservedAt > now.AddMinutes(5))
            return Result.Failure<SyncFieldNoteResult>(AppError.Validation(
                "MissionFieldNote.FutureObservation",
                "ObservedAt cannot be more than five minutes in the future."));

        var note = MissionFieldNote.Create(tenantId, request.FarmId,
            request.MissionId, request.OperationId, actorId,
            request.Text, request.ObservedAt, now);
        notes.Add(note);
        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            note.MissionId, note.OperationId, note.ObservedAt,
            note.ReceivedAt
        });
        auditWriter.AddUserAction(unitOfWork, tenantId, request.FarmId,
            actorId, executionContext.CorrelationId, nameof(MissionFieldNote),
            note.Id, "SYNC_FIELD_NOTE", null, auditData, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionFieldNoteConflictException)
        {
            var concurrent = await notes.GetByOperationIdAsync(tenantId,
                request.FarmId, request.MissionId, request.OperationId,
                cancellationToken);
            return concurrent is null
                ? Result.Failure<SyncFieldNoteResult>(AppError.Conflict(
                    "MissionFieldNote.ConcurrentSync", "Retry this field note operation."))
                : MatchExisting(concurrent, actorId, request);
        }

        return Result.Success(ToResult(note, false));
    }

    private static Result<SyncFieldNoteResult> MatchExisting(
        MissionFieldNote existing, Guid actorId, SyncFieldNoteCommand request) =>
        existing.Matches(actorId, request.Text, request.ObservedAt)
            ? Result.Success(ToResult(existing, true))
            : Result.Failure<SyncFieldNoteResult>(AppError.Conflict(
                "MissionFieldNote.OperationPayloadConflict",
                "The operation ID was already used with another note payload."));

    private static SyncFieldNoteResult ToResult(MissionFieldNote note, bool reused) =>
        new(note.Id, note.OperationId, note.ObservedAt, note.ReceivedAt, reused);
}
