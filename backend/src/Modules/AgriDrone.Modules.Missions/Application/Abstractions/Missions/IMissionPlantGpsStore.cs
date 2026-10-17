namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public interface IMissionPlantGpsStore
{
    Task<bool> ReplaceAsync(Guid tenantId, Guid farmId, Guid missionId,
        IReadOnlyList<MissionPlantGpsRow> rows, CancellationToken cancellationToken);
}

public sealed record MissionPlantGpsRow(string TreeId, int Row, int Col,
    decimal Latitude, decimal Longitude, decimal Confidence, string Status,
    string Source);
