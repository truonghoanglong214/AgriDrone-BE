using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class MissionFieldNoteRepository(MissionsDbContext db)
    : IMissionFieldNoteRepository
{
    public Task<MissionFieldNote?> GetByOperationIdAsync(
        Guid tenantId, Guid farmId, Guid missionId, Guid operationId,
        CancellationToken cancellationToken = default) =>
        db.MissionFieldNotes.AsNoTracking().SingleOrDefaultAsync(note =>
            note.TenantId == tenantId && note.FarmId == farmId &&
            note.MissionId == missionId && note.OperationId == operationId,
            cancellationToken);

    public async Task<IReadOnlyList<MissionFieldNote>> ListAsync(
        Guid tenantId, Guid farmId, Guid missionId,
        CancellationToken cancellationToken = default) =>
        await db.MissionFieldNotes.AsNoTracking().Where(note =>
                note.TenantId == tenantId && note.FarmId == farmId &&
                note.MissionId == missionId)
            .OrderBy(note => note.ObservedAt).ThenBy(note => note.Id)
            .ToListAsync(cancellationToken);

    public void Add(MissionFieldNote note) => db.MissionFieldNotes.Add(note);
}
