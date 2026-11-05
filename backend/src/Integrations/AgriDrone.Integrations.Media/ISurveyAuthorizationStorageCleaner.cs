namespace AgriDrone.Integrations.Media;

public interface ISurveyAuthorizationStorageCleaner
{
    Task<IReadOnlyList<string>> ListOrphanedEvidencesAsync(CancellationToken cancellationToken = default);
    Task<int> PurgeOrphanedEvidencesAsync(CancellationToken cancellationToken = default);
}
