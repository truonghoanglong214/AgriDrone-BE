using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetFieldNotes;

public sealed record GetFieldNotesQuery(Guid FarmId, Guid MissionId)
    : IRequest<Result<IReadOnlyList<MissionFieldNoteResult>>>;
