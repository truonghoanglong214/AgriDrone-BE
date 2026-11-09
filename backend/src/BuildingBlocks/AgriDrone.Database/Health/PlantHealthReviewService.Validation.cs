using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Plants.Domain.Health;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Health;

internal sealed partial class PlantHealthReviewService
{
    private sealed record PositionCheck(bool Covered, double Distance);

    private static HealthReviewDecision Decision(string? value, params HealthReviewDecision[] allowed)
    {
        if (!Enum.TryParse<HealthReviewDecision>(value, out var decision) || value != decision.ToString() || !allowed.Contains(decision))
            Invalid("InvalidDecision", "This decision is not supported for the selected item.");
        return decision;
    }

    private static void Evidence(Session s, string? reason, IReadOnlyList<Guid>? media)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000) Invalid("InvalidRequest", "Reason is required and must be at most 2000 characters.");
        if (media is null || media.Count > 128 || media.Contains(Guid.Empty) || media.Distinct().Count() != media.Count)
            Invalid("InvalidEvidence", "EvidenceMediaIds must be a unique collection of at most 128 non-empty IDs.");
        if (media.Any(id => !s.AllowedMedia.Contains(id))) Invalid("InvalidEvidence", "Evidence must belong to the source Mission/Tenant/Farm.");
    }

    private static bool IsAccepted(PlantHealthReviewEntry? entry) => entry?.Decision is HealthReviewDecision.Approve or HealthReviewDecision.Correct;
    private static bool RuleValid(Session s)
    {
        var classes = s.Conditions.Values.Where(c => c.IsActive && c.PlantHealthLabelKind == PlantHealthLabelKind.SuspectedDisease).ToArray();
        return classes.Length is >= 2 and <= 3 && classes.All(c => c.DatasetVersion == s.Source.Provenance.DatasetVersion &&
            c.AnnotationRuleVersion == s.Source.Provenance.AnnotationRuleVersion);
    }
    private static bool ConditionValid(Session s, HealthReviewSnapshot snapshot) =>
        snapshot.ConditionId is Guid id && s.Conditions.TryGetValue(id, out var condition) && condition.IsActive &&
        condition.PlantHealthLabelKind == PlantHealthLabelKind.SuspectedDisease && condition.Code == snapshot.ClassCode &&
        condition.RevisionNumber == snapshot.ConditionRevision && condition.DatasetVersion == s.Source.Provenance.DatasetVersion &&
        condition.AnnotationRuleVersion == snapshot.AnnotationRuleVersion && snapshot.AnnotationRuleVersion == s.Source.Provenance.AnnotationRuleVersion;

    private static PlantCondition Condition(Session s, string? classCode)
    {
        var active = s.Conditions.Values.Where(c => c.IsActive && c.PlantHealthLabelKind == PlantHealthLabelKind.SuspectedDisease).ToArray();
        if (active.Length is < 2 or > 3) Invalid("CatalogueMismatch", "Configure two or three active suspected disease classes.");
        var condition = active.SingleOrDefault(c => string.Equals(c.Code, classCode?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (condition is null || condition.DatasetVersion != s.Source.Provenance.DatasetVersion ||
            condition.AnnotationRuleVersion != s.Source.Provenance.AnnotationRuleVersion)
            Invalid("CatalogueMismatch", "The class or dataset/annotation revision does not match the configured catalogue.");
        return condition;
    }

    private static void PlantAssociation(Session s, Guid? plantId)
    {
        if (plantId is not Guid id || !s.Plants.TryGetValue(id, out var plant) || plant.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId)
            Invalid("InvalidAssociation", "An existing Plant with matching capture Farm/map evidence is required.");
    }

    private static Point Point(IReadOnlyList<double>? coordinates)
    {
        if (coordinates is not { Count: 2 } || !double.IsFinite(coordinates[0]) || !double.IsFinite(coordinates[1]) ||
            coordinates[0] is < -180 or > 180 || coordinates[1] is < -90 or > 90)
            Invalid("InvalidGeometry", "A valid WGS84 Point is required.");
        return new(coordinates[0], coordinates[1]) { SRID = 4326 };
    }

    private static Polygon Polygon(GeoJsonPolygonV3? source)
    {
        if (source?.Type != "Polygon" || source.Coordinates is not { Count: > 0 and <= 128 })
            Invalid("InvalidGeometry", "A valid WGS84 Polygon is required.");
        var rings = new List<LinearRing>();
        foreach (var ring in source.Coordinates)
        {
            if (ring is not { Count: >= 4 and <= 10000 }) Invalid("InvalidGeometry", "Polygon rings must be closed and bounded in size.");
            var points = ring.Select(c => Point(c).Coordinate).ToArray();
            if (!points[0].Equals2D(points[^1])) Invalid("InvalidGeometry", "Polygon rings must be closed.");
            rings.Add(new LinearRing(points) { SRID = 4326 });
        }
        var polygon = new Polygon(rings[0], rings.Skip(1).ToArray()) { SRID = 4326 };
        if (!polygon.IsValid || polygon.IsEmpty) Invalid("InvalidGeometry", "Polygon topology is invalid.");
        return polygon;
    }

    private async Task CheckPositionAsync(Session s, Point position, bool hasEvidence, CancellationToken token)
    {
        var check = await _db.Database.SqlQuery<PositionCheck>($"""
            SELECT ST_Covers(geometry, {position}) AS "Covered",
                ST_Distance(ST_Boundary(geometry)::geography, {position}::geography) AS "Distance"
            FROM farm.farm_boundaries WHERE id = {s.Batch.FarmBoundaryVersionId}
              AND tenant_id = {s.Order.TenantId} AND farm_id = {s.Order.FarmId}
            """).SingleOrDefaultAsync(token);
        if (check is null || !check.Covered) Invalid("InvalidGeometry", "Reviewed position must be inside the capture FarmBoundary.");
        if (check.Distance <= (double)AgriDrone.IntegrationContracts.Health.PlantHealthV4Values.BoundaryReviewThresholdMeters && !hasEvidence)
            Invalid("InvalidEvidence", "Near-boundary decisions require supporting evidence.");
    }

    private static List<string> FindingIssues(Session s, Guid id)
    {
        var review = Current(s, HealthReviewKind.Finding, id);
        if (review is null) return ["Unreviewed"];
        if (review.Decision == HealthReviewDecision.Ambiguous) return ["Ambiguous"];
        if (!RuleValid(s)) return ["AnnotationRuleChanged"];
        if (!IsAccepted(review)) return [];
        var value = review.ReadSnapshot();
        List<string> issues = [];
        if (!ConditionValid(s, value)) issues.Add("CatalogueChanged");
        if (value.PlantId is not Guid plantId || !s.Plants.TryGetValue(plantId, out var plant) || plant.FarmBaseMapVersionId != s.Batch.FarmBaseMapVersionId)
            issues.Add("PlantAssociationChanged");
        return issues;
    }

    private static List<string> BoundaryIssues(Session s, Guid id)
    {
        var entry = Current(s, HealthReviewKind.Boundary, id);
        if (entry is null || entry.Decision == HealthReviewDecision.NeedsEvidence) return ["UnresolvedBoundaryException"];
        var value = entry.ReadSnapshot();
        if (value.BoundFindingId is not Guid findingId || !s.Findings.ContainsKey(findingId) ||
            Current(s, HealthReviewKind.Finding, findingId) is not { } findingReview || findingReview.Id != value.BoundReviewId)
            return ["StaleBoundaryAssociation"];
        if (entry.Decision == HealthReviewDecision.ExcludeEvidence)
            return findingReview.Decision is HealthReviewDecision.Reject or HealthReviewDecision.NonDisease ? [] : ["InvalidExclusion"];
        return IsAccepted(findingReview) && FindingIssues(s, findingId).Count == 0 ? [] : ["InvalidBoundaryResolution"];
    }

    private static List<string> ZoneIssues(Session s, DiseaseZone zone, bool includeGuidance = true)
    {
        var review = Current(s, HealthReviewKind.Zone, zone.Id);
        if (review is null) return ["UnreviewedZone"];
        if (review.Decision == HealthReviewDecision.Reject) return [];
        if (review.Decision == HealthReviewDecision.Ambiguous) return ["AmbiguousZone"];
        var value = review.ReadSnapshot();
        List<string> issues = [];
        if (!ConditionValid(s, value)) issues.Add("CatalogueChanged");
        if (value.Members is not { Count: > 0 }) issues.Add("UnresolvedZoneMembers");
        else foreach (var member in value.Members)
        {
            var findingReview = Current(s, HealthReviewKind.Finding, member.FindingId);
            if (!s.Findings.TryGetValue(member.FindingId, out var finding) || finding.SourceVersion != member.SourceVersion ||
                findingReview?.Id != member.ReviewId || !IsAccepted(findingReview) ||
                findingReview!.ReadSnapshot().ClassCode != value.ClassCode || findingReview.ReadSnapshot().PlantId != member.PlantId ||
                FindingIssues(s, member.FindingId).Count != 0)
                issues.Add("StaleMemberReview");
        }
        if (!includeGuidance) return issues.Distinct().ToList();
        var guidance = Current(s, HealthReviewKind.Guidance, zone.Id);
        if (guidance is null || guidance.ReadSnapshot().BoundReviewId != review.Id || guidance.Decision == HealthReviewDecision.NeedsEvidence)
            issues.Add("MissingGuidanceDecision");
        else if (guidance.Decision == HealthReviewDecision.Select && !GuidanceValid(s, value, guidance.ReadSnapshot().Guidance))
            issues.Add("GuidanceNotApplicable");
        return issues.Distinct().ToList();
    }

    private static bool GuidanceValid(Session s, HealthReviewSnapshot zone, ReviewedGuidanceSnapshot? snapshot) =>
        snapshot is not null && s.Guidance.TryGetValue(snapshot.Id, out var entry) && entry.IsEffectiveAt(s.Now) &&
        entry.Code == snapshot.Code && entry.VersionNumber == snapshot.VersionNumber && entry.SourceReference == snapshot.SourceReference &&
        entry.SuspectedClassCode == zone.ClassCode && entry.SuspectedClassCode == snapshot.SuspectedClassCode &&
        entry.PlantConditionId == zone.ConditionId && entry.PlantConditionRevisionNumber == zone.ConditionRevision &&
        entry.PlantConditionId == snapshot.PlantConditionId && entry.PlantConditionRevisionNumber == snapshot.PlantConditionRevisionNumber &&
        entry.EffectiveFrom == snapshot.EffectiveFrom && entry.EffectiveTo == snapshot.EffectiveTo;
}
