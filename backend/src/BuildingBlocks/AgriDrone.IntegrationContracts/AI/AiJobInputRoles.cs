namespace AgriDrone.IntegrationContracts.AI;

public static class AiJobInputRoles
{
    public const string RawVideo = "RAW_VIDEO";

    public const string RawImage = "RAW_IMAGE";

    public const string Telemetry = "TELEMETRY";

    public const string ReferenceMap = "REFERENCE_MAP";

    public const string PlantReferenceSnapshot =
        "PLANT_REFERENCE_SNAPSHOT";

    public static bool IsSupported(string? role) =>
        role is
            RawVideo or
            RawImage or
            Telemetry or
            ReferenceMap or
            PlantReferenceSnapshot;
}