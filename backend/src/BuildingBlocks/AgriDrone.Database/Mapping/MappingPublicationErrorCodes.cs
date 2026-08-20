namespace AgriDrone.Database.Mapping;

internal static class MappingPublicationErrorCodes
{
    public const string ActorRequired = "MAPPING_ACTOR_REQUIRED";
    public const string AccessDenied = "MAPPING_ACCESS_DENIED";
    public const string FarmNotFound = "MAPPING_FARM_NOT_FOUND";
    public const string FarmInactive = "MAPPING_FARM_INACTIVE";
    public const string ZoneNotFound = "MAPPING_ZONE_NOT_FOUND";
    public const string ZoneInactive = "MAPPING_ZONE_INACTIVE";
    public const string MissionNotFound = "MAPPING_MISSION_NOT_FOUND";
    public const string MissionInvalid = "MAPPING_MISSION_INVALID";
    public const string SnapshotStale = "MAPPING_SNAPSHOT_STALE";
    public const string ApprovalAlreadyUsed = "MAPPING_APPROVAL_ALREADY_USED";
    public const string PlantNotFound = "MAPPING_PLANT_NOT_FOUND";
    public const string PlantInvalid = "MAPPING_PLANT_INVALID";
    public const string UnknownHealthMissing = "MAPPING_UNKNOWN_HEALTH_MISSING";
    public const string MeasurementOutOfRange = "MAPPING_MEASUREMENT_OUT_OF_RANGE";
}
