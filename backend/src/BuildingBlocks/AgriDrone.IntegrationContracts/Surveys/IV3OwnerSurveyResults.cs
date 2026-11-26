using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Surveys;

public interface IV3OwnerSurveyResults
{
    Task<IReadOnlyList<V3OwnerSurveySummary>?> GetHistoryAsync(
        Guid tenantId, Guid farmId, CancellationToken cancellationToken);

    Task<V3OwnerSurveyResult?> GetAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken);

    Task<V3OwnerSurveyComparison?> CompareAsync(
        Guid tenantId, Guid farmId, Guid fromOrderId, Guid toOrderId, CancellationToken cancellationToken);

    Task<Uri?> GetImageAsync(Guid tenantId, Guid orderId, Guid mediaId, CancellationToken cancellationToken);
}

public sealed record V3OwnerSurveySummary(
    Guid OrderId, Guid PublicationId, Guid FarmId, string ServiceCode,
    Guid MapVersionId, Guid BaselineMapVersionId, DateTimeOffset PublishedAt, DateTimeOffset ClaimDeadlineExclusiveUtc);

public sealed record V3OwnerSurveyResult(
    V3OwnerSurveySummary Summary, int VerifiedPoleCount, string? BoundaryGeoJson,
    IReadOnlyList<V3OwnerSurveyPole> Poles, string HealthAssessmentStatus, string? LimitationNotice,
    IReadOnlyList<V3OwnerSurveyFinding> Findings, IReadOnlyList<V3OwnerSurveyZone> Zones, IReadOnlyList<Guid> ImageIds);

public sealed record V3OwnerSurveyPole(
    Guid PlantProfileId, string PlantCode, double Longitude, double Latitude, int? Row, int? Column);

public sealed record V3OwnerSurveyFinding(
    Guid FindingId, Guid PlantProfileId, GeoJsonPointV3 Position, DateTimeOffset ObservedAt,
    string SuspectedClassCode, decimal Confidence, string Uncertainty, IReadOnlyList<string> Limitations);

public sealed record V3OwnerSurveyZone(
    string SourceProposalId, string SuspectedClassCode, GeoJsonPolygonV3 Geometry,
    IReadOnlyList<Guid> FindingIds, Guid? GuidanceId, int? GuidanceVersion);

public sealed record V3OwnerSurveyComparison(
    V3OwnerSurveySummary From, V3OwnerSurveySummary To,
    IReadOnlyList<V3OwnerSurveyPole> AddedPoles, IReadOnlyList<V3OwnerSurveyPole> MissingPoles,
    IReadOnlyList<V3OwnerSurveyPoleChange> ChangedPoles, IReadOnlyList<V3OwnerSurveyHealthChange> HealthChanges);

public sealed record V3OwnerSurveyPoleChange(V3OwnerSurveyPole From, V3OwnerSurveyPole To);

public sealed record V3OwnerSurveyHealthChange(
    Guid PlantProfileId, IReadOnlyList<string> FromSuspectedClasses, IReadOnlyList<string> ToSuspectedClasses);
