using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class MediaUploadSessionRepository(
    MissionsDbContext dbContext)
    : IMediaUploadSessionRepository
{
    public Task<MediaUploadSession?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.MediaUploadSessions
            .SingleOrDefaultAsync(
                session =>
                    session.Id == sessionId &&
                    session.TenantId == tenantId &&
                    session.FarmId == farmId &&
                    session.MissionId == missionId,
                cancellationToken);
    }

    public Task<MediaUploadSession?> GetByOperationIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.MediaUploadSessions
            .SingleOrDefaultAsync(
                session =>
                    session.OperationId == operationId &&
                    session.TenantId == tenantId &&
                    session.FarmId == farmId &&
                    session.MissionId == missionId,
                cancellationToken);
    }

    public void Add(MediaUploadSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        dbContext.MediaUploadSessions.Add(session);
    }
}