namespace AgriDrone.Modules.Plants.Domain.Mapping;

public enum PlantChangeType
{
    NewPlant,
    MissingPlant,
    RemovedPlant,
    DeadPlant,
    DetectionError,
    MappingDifference
}
