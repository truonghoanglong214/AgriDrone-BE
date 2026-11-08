namespace AgriDrone.Modules.Plants.Application.Features.GetFarmPlants;

public sealed record FarmPlantItem(
    Guid Id,
    string PlantCode,
    Guid? ZoneId,
    int? Row,
    int? Col,
    double? Lat,
    double? Lon,
    string LifecycleStatus);
