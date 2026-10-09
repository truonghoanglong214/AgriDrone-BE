using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetFieldNotes;

internal sealed class GetFieldNotesQueryHandler(
    IDroneMissionRepository missions,
    IMissionFieldNoteRepository notes,
    ISystemManagerAccessService managerAccess,
    IExecutionContext executionContext)
    : IRequestHandler<GetFieldNotesQuery, Result<IReadOnlyList<MissionFieldNoteResult>>>
{
    public async Task<Result<IReadOnlyList<MissionFieldNoteResult>>> Handle(
        GetFieldNotesQuery request, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
            return Result.Failure<IReadOnlyList<MissionFieldNoteResult>>(
                MissionError.CurrentUserRequired());
        var access = await managerAccess.ResolveFarmAccessAsync(request.FarmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
            return Result.Failure<IReadOnlyList<MissionFieldNoteResult>>(
                AppError.Forbidden("MissionFieldNote.FarmAccessDenied",
                    "The manager is not assigned and flight-qualified for this Farm."));
        var mission = await missions.GetByIdAsync(request.MissionId, tenantId,
            request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<IReadOnlyList<MissionFieldNoteResult>>(
                MissionError.NotFound(request.MissionId));
        if (mission.SurveyOrderId is null)
            return Result.Failure<IReadOnlyList<MissionFieldNoteResult>>(
                AppError.Conflict("MissionFieldNote.MissionNotOrderBound",
                    "Field notes are only available for order-bound Missions."));

        var stored = await notes.ListAsync(tenantId, request.FarmId,
            request.MissionId, cancellationToken);
        return Result.Success<IReadOnlyList<MissionFieldNoteResult>>(
            stored.Select(note => new MissionFieldNoteResult(
                note.Id, note.OperationId, note.CreatedBy, note.Text,
                note.ObservedAt, note.ReceivedAt)).ToArray());
    }
}
