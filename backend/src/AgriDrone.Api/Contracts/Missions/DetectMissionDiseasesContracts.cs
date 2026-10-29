using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record DetectMissionDiseasesRequest(
    double ConfidenceThreshold = 0.25,
    JsonElement? PolygonGeoJson = null);

public sealed record DiseaseDetectionItemResponse(
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

public sealed record DetectMissionDiseasesResponse(
    Guid MissionId,
    string ModelName,
    int TotalImagesScanned,
    int TotalDetections,
    IReadOnlyDictionary<string, int> DiseaseSummary,
    string? CsvDownloadUrl,
    IReadOnlyList<DiseaseDetectionItemResponse> Detections,
    IReadOnlyList<string>? AnnotatedImageUrls = null);
