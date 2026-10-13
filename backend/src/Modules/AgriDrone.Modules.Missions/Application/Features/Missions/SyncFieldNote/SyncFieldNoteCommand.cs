using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;

public sealed record SyncFieldNoteCommand(
    Guid FarmId, Guid MissionId, Guid OperationId,
    string Text, DateTimeOffset ObservedAt,
    string? IncidentType = null, string? IncidentOutcome = null,
    string? RecoveryDecision = null, string? EvidenceReference = null)
    : IRequest<Result<SyncFieldNoteResult>>;
