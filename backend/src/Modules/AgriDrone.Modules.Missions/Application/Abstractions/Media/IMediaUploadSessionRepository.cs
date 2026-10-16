using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

internal interface IMediaUploadSessionRepository
{
    Task<MediaUploadSession?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<MediaUploadSession?> GetByOperationIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<MediaUploadSession?> GetActiveByChecksumAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        string checksum,
        CancellationToken cancellationToken = default);

    void Add(MediaUploadSession session);
}
