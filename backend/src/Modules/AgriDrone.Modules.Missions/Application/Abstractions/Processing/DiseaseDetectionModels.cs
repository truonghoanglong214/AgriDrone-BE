namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

/// <summary>
/// A single disease detection instance with GPS coordinates, cropped image and annotated image info.
/// </summary>
public sealed record DiseaseDetectionItem(
    string Id,
    int ClassId,
    string ClassName,
    double Lat,
    double Lon,
    double Confidence,
    double BboxX,
    double BboxY,
    double BboxW,
    double BboxH,
    string ImageName,
    string? CropImageUrl,
    string? AnnotatedImageUrl = null);

/// <summary>
/// Result of running disease detection across mission photos.
/// </summary>
public sealed record DetectMissionDiseasesResult(
    bool IsSuccess,
    Guid MissionId,
    string ModelName,
    int TotalImagesScanned,
    int TotalDetections,
    IReadOnlyDictionary<string, int> DiseaseSummary,
    string? CsvDownloadUrl,
    IReadOnlyList<DiseaseDetectionItem> Detections,
    IReadOnlyList<string>? AnnotatedImageUrls = null,
    string? ErrorMessage = null);
