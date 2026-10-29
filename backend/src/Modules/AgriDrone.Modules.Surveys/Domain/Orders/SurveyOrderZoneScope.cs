using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrderZoneScope : Entity
{
    private SurveyOrderZoneScope() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid ZoneId { get; private set; }
    public long ZoneVersion { get; private set; }
    public Polygon BoundarySnapshot { get; private set; } = null!;
    public DateTimeOffset CapturedAt { get; private set; }

    internal static SurveyOrderZoneScope Create(
        Guid surveyOrderId,
        Guid farmId,
        Guid zoneId,
        long zoneVersion,
        Polygon boundary,
        DateTimeOffset capturedAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.Utc(capturedAt);
        ArgumentOutOfRangeException.ThrowIfLessThan(zoneVersion, 1);
        ArgumentNullException.ThrowIfNull(boundary);

        var snapshot = (Polygon)boundary.Copy();
        snapshot.SRID = 4326;

        return new SurveyOrderZoneScope
        {
            Id = Guid.NewGuid(),
            SurveyOrderId = surveyOrderId,
            FarmId = farmId,
            ZoneId = zoneId,
            ZoneVersion = zoneVersion,
            BoundarySnapshot = snapshot,
            CapturedAt = capturedAt
        };
    }
}
