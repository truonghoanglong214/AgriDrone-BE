namespace AgriDrone.IntegrationContracts.Farms;

public interface ISurveyRequestFarmReferenceQuery
{
    Task<SurveyRequestFarmReference?> GetAsync(
        Guid farmId,
        CancellationToken cancellationToken = default);
}

public sealed record SurveyRequestFarmReference(
    Guid TenantId,
    Guid FarmId,
    bool IsActive,
    string Name,
    string? Address,
    decimal? AreaHectares,
    double? Longitude,
    double? Latitude,
    int? MapSrid);
