namespace AgriDrone.IntegrationContracts.AI;

public static class AiJobTypes
{
    public const string Mapping = "MAPPING";

    public const string HealthInspection =
        "HEALTH_INSPECTION";

    public const string FrameExtraction =
        "FRAME_EXTRACTION";

    public const string PlantDetection =
        "PLANT_DETECTION";

    public const string PlantMatching =
        "PLANT_MATCHING";

    public const string DiseaseDetection =
        "DISEASE_DETECTION";

    public static bool IsSupported(string? jobType) =>
        jobType is
            Mapping or
            HealthInspection or
            FrameExtraction or
            PlantDetection or
            PlantMatching or
            DiseaseDetection;
}