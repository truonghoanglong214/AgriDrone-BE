using AgriDrone.Modules.Plants.Domain.Plants;

namespace AgriDrone.Modules.Plants.Application.Features.RegisterPlantManually;

public sealed record RegisterPlantManuallyResponse(
    Guid PlantId,
    Guid FarmId,
    Guid ZoneId,
    string PlantCode,
    double Latitude,
    double Longitude,
    Guid? MapVersionId,
    int? RowIndex,
    int? ColumnIndex,
    decimal? LocationAccuracyM,
    PositionSource PositionSource,
    PlantLifecycleStatus LifecycleStatus,
    Guid CurrentHealthLevelId,
    DateTimeOffset CreatedAt)
{
    internal static RegisterPlantManuallyResponse From(Plant plant)
    {
        var location = plant.Location ??
            throw new InvalidOperationException(
                "A manually registered plant must have a location.");
        var zoneId = plant.ZoneId ??
            throw new InvalidOperationException(
                "A manually registered plant must belong to a zone.");
        var positionSource = plant.PositionSource ??
            throw new InvalidOperationException(
                "A manually registered plant must have a position source.");

        return new RegisterPlantManuallyResponse(
            plant.Id,
            plant.FarmId,
            zoneId,
            plant.PlantCode,
            Latitude: location.Y,
            Longitude: location.X,
            plant.CurrentMapVersionId,
            plant.RowIndex,
            plant.ColumnIndex,
            plant.LocationAccuracyM,
            positionSource,
            plant.LifecycleStatus,
            plant.CurrentHealthLevelId,
            plant.CreatedAt);
    }
}
