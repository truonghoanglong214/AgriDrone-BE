using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;

public sealed record SyncFieldNoteCommand(
    Guid FarmId, Guid MissionId, Guid OperationId,
    string Text, DateTimeOffset ObservedAt)
    : IRequest<Result<SyncFieldNoteResult>>;
