using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Messaging.Validation;

public static class V4BusinessContractValidators
{
    public static IReadOnlyList<string> Validate(PlantHealthAnalysisReadyV4? payload)
    {
        List<string> errors = [];
        if (payload is null) return ["Payload is required."];
        foreach (var id in new[] { payload.CausationId, payload.HandoffId, payload.SurveyOrderId,
                     payload.MissionId, payload.FarmId, payload.FarmBoundaryVersionId,
                     payload.FarmBaseMapVersionId, payload.JobId })
            if (id == Guid.Empty) errors.Add("Context identifiers must be non-empty.");
        Utc(payload.ProducedAt, "ProducedAt", errors);
        if (payload.ResultNature != PlantHealthV4Values.ResultNature) errors.Add("ResultNature must be PRELIMINARY_SUSPECTED.");
        Text(payload.TeamLabelLimitation, 2000, "TeamLabelLimitation", errors);
        if (payload.Provenance is not { } provenance) errors.Add("Provenance is required.");
        else
        {
            Text(provenance.DatasetId, 128, "DatasetId", errors);
            Text(provenance.DatasetVersion, 128, "DatasetVersion", errors);
            Text(provenance.AnnotationRuleVersion, 128, "AnnotationRuleVersion", errors);
            Text(provenance.ModelVersion, 128, "ModelVersion", errors);
            Text(provenance.BoundaryPolicyVersion, 128, "BoundaryPolicyVersion", errors);
            if (provenance.ModelVersionId == Guid.Empty) errors.Add("ModelVersionId is required.");
            if (provenance.ThresholdProfileVersion is not null) Text(provenance.ThresholdProfileVersion, 128, "ThresholdProfileVersion", errors);
        }
        if (payload.Findings is null || payload.BoundaryExceptions is null || payload.DiseaseZones is null)
            return [.. errors, "Findings, BoundaryExceptions and DiseaseZones collections are required."];
        if (payload.Findings.Count > IntegrationContractLimits.MaximumHealthObservationCount ||
            payload.BoundaryExceptions.Count > IntegrationContractLimits.MaximumHealthObservationCount ||
            payload.DiseaseZones.Count > IntegrationContractLimits.MaximumHealthObservationCount)
            return [.. errors, "Health collection exceeds the allowed count."];
        Unique(payload.Findings.Select(f => f?.FindingId), "FindingId", errors);
        foreach (var f in payload.Findings)
        {
            if (f is null) { errors.Add("Finding cannot be null."); continue; }
            if (f.FindingId == Guid.Empty || f.FindingVersion <= 0) errors.Add("Finding identity/version is invalid.");
            Reference(f.PlantId, errors); Reference(f.MappingCandidateId, errors);
            Point(f.Position, errors); Utc(f.ObservedAt, "ObservedAt", errors);
            if (f.ObservedAt > payload.ProducedAt) errors.Add("ObservedAt cannot follow ProducedAt.");
            Text(f.SuspectedClassCode, 80, "SuspectedClassCode", errors);
            if (f.ClassificationNature != PlantHealthV4Values.ClassificationNature) errors.Add("ClassificationNature must be SUSPECTED.");
            Confidence(f.Confidence, errors); Text(f.Uncertainty, 1000, "Uncertainty", errors);
            if (f.Limitations is null || f.Limitations.Count > 64) errors.Add("Limitations collection is invalid.");
            else foreach (var limitation in f.Limitations) Text(limitation, 1000, "Limitation", errors);
            Media(f.EvidenceMedia, errors);
        }
        Unique(payload.BoundaryExceptions.Select(e => e?.SourceReferenceId), "Boundary exception source", errors);
        foreach (var e in payload.BoundaryExceptions)
        {
            if (e is null) { errors.Add("Boundary exception cannot be null."); continue; }
            Text(e.SourceReferenceId, 200, "SourceReferenceId", errors); Point(e.OriginalPosition, errors);
            if (e.BoundaryClassification is not ("OUT_OF_BOUNDARY" or "NEEDS_REVIEW")) errors.Add("Invalid boundary exception classification.");
            if (e.MeasuredBoundaryDistanceM < 0 || e.BoundaryThresholdM < 0) errors.Add("Boundary distances must be non-negative.");
            Media(e.EvidenceMedia, errors);
        }
        Unique(payload.DiseaseZones.Select(z => z?.SourceProposalId), "SourceProposalId", errors);
        foreach (var z in payload.DiseaseZones)
        {
            if (z is null) { errors.Add("Zone cannot be null."); continue; }
            Text(z.SourceProposalId, 200, "SourceProposalId", errors);
            Text(z.SuspectedClassCode, 80, "SuspectedClassCode", errors);
            if (z.MembershipVersion <= 0) errors.Add("MembershipVersion must be positive.");
            Polygon(z.Geometry, errors); Media(z.EvidenceMedia, errors);
            if (z.Memberships is null || z.Memberships.Count == 0 || z.Memberships.Count > 10000)
                errors.Add("Zone memberships collection is invalid.");
            else
            {
                Unique(z.Memberships.Select(m => (m?.PlantId, m?.MappingCandidateId)), "Zone member", errors);
                Unique(z.Memberships.Where(m => m?.PlantId is not null).Select(m => m.PlantId), "Zone Plant", errors);
                Unique(z.Memberships.Where(m => m?.MappingCandidateId is not null).Select(m => m.MappingCandidateId), "Zone candidate", errors);
                foreach (var m in z.Memberships)
                {
                    if (m is null) { errors.Add("Zone member cannot be null."); continue; }
                    Reference(m.PlantId, errors); Reference(m.MappingCandidateId, errors);
                    if (!m.PlantId.HasValue && !m.MappingCandidateId.HasValue) errors.Add("Zone member needs a Plant or candidate reference.");
                    Confidence(m.Confidence, errors);
                }
            }
            if (z.GuidanceCandidates is null || z.GuidanceCandidates.Count > 64) errors.Add("Guidance candidates collection is invalid.");
            else foreach (var g in z.GuidanceCandidates)
            {
                if (g is null) { errors.Add("Guidance candidate cannot be null."); continue; }
                if (g.GuidanceId == Guid.Empty || g.VersionNumber <= 0) errors.Add("Guidance identity/version is invalid.");
                Text(g.Code, 80, "Guidance code", errors); Text(g.SourceReference, 1000, "SourceReference", errors);
            }
        }
        return errors;
    }

    private static void Text(string? value, int limit, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit) errors.Add($"{name} is required and must be at most {limit} characters.");
    }
    private static void Utc(DateTimeOffset value, string name, List<string> errors)
    {
        if (value == default || value.Offset != TimeSpan.Zero) errors.Add($"{name} must be non-default UTC.");
    }
    private static void Reference(Guid? value, List<string> errors)
    {
        if (value == Guid.Empty) errors.Add("Optional reference cannot be empty.");
    }
    private static void Confidence(decimal value, List<string> errors)
    {
        if (value is < 0 or > 1) errors.Add("Confidence must be between 0 and 1.");
    }
    private static void Unique<T>(IEnumerable<T> values, string name, List<string> errors)
    {
        var list = values.ToList();
        if (list.Distinct().Count() != list.Count) errors.Add($"{name} must be unique.");
    }
    private static void Point(GeoJsonPointV3? point, List<string> errors)
    {
        if (point is null || point.Type != "Point" || !Coordinates(point.Coordinates)) errors.Add("Invalid WGS84 Point.");
    }
    private static bool Coordinates(IReadOnlyList<double>? coordinates) => coordinates is { Count: 2 } &&
        double.IsFinite(coordinates[0]) && double.IsFinite(coordinates[1]) &&
        coordinates[0] is >= -180 and <= 180 && coordinates[1] is >= -90 and <= 90;
    private static void Polygon(GeoJsonPolygonV3? polygon, List<string> errors)
    {
        if (polygon?.Type != "Polygon" || polygon.Coordinates is null || polygon.Coordinates.Count is < 1 or > 128)
        { errors.Add("Invalid WGS84 Polygon."); return; }
        foreach (var ring in polygon.Coordinates)
            if (ring is null || ring.Count is < 4 or > 10000 || ring.Any(p => !Coordinates(p)) ||
                !ring[0].SequenceEqual(ring[^1])) errors.Add("Polygon rings must be valid and closed.");
    }
    private static void Media(IReadOnlyList<EvidenceMediaReferenceV4>? media, List<string> errors)
    {
        if (media is null || media.Count > 128) { errors.Add("Evidence media collection is invalid."); return; }
        foreach (var m in media)
        {
            if (m is null || m.MediaAssetId == Guid.Empty) errors.Add("Invalid evidence media reference.");
            else Text(m.Role, 100, "Media role", errors);
        }
    }
}
