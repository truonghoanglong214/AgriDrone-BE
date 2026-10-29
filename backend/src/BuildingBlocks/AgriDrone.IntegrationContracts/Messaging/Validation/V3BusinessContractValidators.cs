using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;

namespace AgriDrone.IntegrationContracts.Messaging.Validation;

public static class V3BusinessContractValidators
{
    private static readonly HashSet<string> CandidateBoundaryClassifications =
    [
        BoundaryClassificationsV3.InBoundary,
        BoundaryClassificationsV3.OutOfBoundary,
        BoundaryClassificationsV3.NeedsReview
    ];

    private static readonly HashSet<string> ExceptionBoundaryClassifications =
    [
        BoundaryClassificationsV3.OutOfBoundary,
        BoundaryClassificationsV3.NeedsReview
    ];

    public static IReadOnlyList<string> Validate(
        BaselineMappingCandidatesReadyV3? payload)
    {
        var errors = RequiredIds(
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.MissionId, "MissionId"),
            (payload?.FarmId, "FarmId"));

        if (payload is null)
        {
            return errors;
        }

        OptionalId(payload.CausationId, "CausationId", errors);
        OptionalId(payload.HandoffId, "HandoffId", errors);
        OptionalId(payload.FarmBoundaryVersionId, "FarmBoundaryVersionId", errors);
        OptionalId(payload.JobId, "JobId", errors);
        OptionalId(payload.ModelVersionId, "ModelVersionId", errors);
        OptionalId(payload.ExpectedCurrentFarmBaseMapVersionId,
            "ExpectedCurrentFarmBaseMapVersionId", errors);
        RequiredText(payload.ModelVersion, "ModelVersion", errors);
        RequiredText(payload.AlgorithmVersion, "AlgorithmVersion", errors);
        RequiredText(payload.BoundaryPolicyVersion, "BoundaryPolicyVersion", errors);
        if (payload.ProducedAt.HasValue)
        {
            Utc(payload.ProducedAt.Value, "ProducedAt", errors);
        }
        Bearing(payload.GridBearingDeg, "GridBearingDeg", errors);
        PositiveMeasurement(payload.RowSpacingM, "RowSpacingM", errors);
        PositiveMeasurement(payload.PlantSpacingM, "PlantSpacingM", errors);
        RequiredItems(payload.Candidates, "Candidates", errors);

        if (payload.Candidates is null)
        {
            return errors;
        }

        var providedCandidateIds = payload.Candidates
            .Where(candidate => candidate.CandidateId.HasValue && candidate.CandidateId.Value != Guid.Empty)
            .Select(candidate => candidate.CandidateId!.Value)
            .ToList();
        Unique(providedCandidateIds, "CandidateId", errors);

        var candidateCount = payload.Candidates.Count;
        Maximum(candidateCount, IntegrationContractLimits.MaximumMappingCandidateCount,
            "Mapping candidates", errors);

        foreach (var candidate in payload.Candidates)
        {
            OptionalId(candidate.CandidateId, "CandidateId", errors);
            OptionalId(candidate.SourceObservationId, "SourceObservationId", errors);
            if (candidate.EvidenceMediaAssetIds is not null)
            {
                foreach (var mediaAssetId in candidate.EvidenceMediaAssetIds)
                {
                    RequiredId(mediaAssetId, "EvidenceMediaAssetId", errors);
                }

                Unique(candidate.EvidenceMediaAssetIds,
                    "EvidenceMediaAssetId within a candidate", errors);
            }

            if (candidate.OriginalPosition is not null)
            {
                Point(candidate.OriginalPosition, "OriginalPosition", errors);
            }
            else
            {
                if (candidate.Longitude is < -180 or > 180)
                {
                    errors.Add("Longitude must be in the range [-180, 180].");
                }
                if (candidate.Latitude is < -90 or > 90)
                {
                    errors.Add("Latitude must be in the range [-90, 90].");
                }
                if (candidate.Longitude == 0 && candidate.Latitude == 0)
                {
                    errors.Add("Mapping candidate coordinates (Longitude, Latitude) must be provided.");
                }
            }

            GridPosition(candidate.ProposedRowIndex, candidate.ProposedColumnIndex,
                errors);
            Range(candidate.PositionConfidence, 0, 1,
                "PositionConfidence", errors);

            if (!string.IsNullOrWhiteSpace(candidate.BoundaryClassification))
            {
                Allowed(candidate.BoundaryClassification,
                    CandidateBoundaryClassifications, "BoundaryClassification", errors);
            }

            if (candidate.MeasuredBoundaryDistanceM.HasValue)
            {
                NonNegative(candidate.MeasuredBoundaryDistanceM.Value,
                    "MeasuredBoundaryDistanceM", errors);
            }

            if (candidate.BoundaryThresholdM.HasValue)
            {
                Positive(candidate.BoundaryThresholdM.Value, "BoundaryThresholdM", errors);
            }

            OptionalId(candidate.MatchedPlantId, "MatchedPlantId", errors);
            if (candidate.MatchConfidence.HasValue)
            {
                Range(candidate.MatchConfidence.Value, 0, 1,
                    "MatchConfidence", errors);
            }

            if (candidate.Status is not null &&
                candidate.Status is not ("DETECTED" or "MISSING"))
            {
                errors.Add("Status must be DETECTED or MISSING when provided.");
            }

            if (candidate.Source is { Length: > 100 })
            {
                errors.Add("Source cannot exceed 100 characters.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(FarmBaseMapPublishedV3? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.PublicationId, "PublicationId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.SourceMissionId, "SourceMissionId"),
            (payload?.FarmId, "FarmId"),
            (payload?.FarmBoundaryVersionId, "FarmBoundaryVersionId"),
            (payload?.FarmBaseMapVersionId, "FarmBaseMapVersionId"),
            (payload?.ConfirmedBy, "ConfirmedBy"));

        if (payload is null)
        {
            return errors;
        }

        Positive(payload.VersionNumber, "VersionNumber", errors);
        Positive(payload.ConfirmedSurveyPoleCount, "ConfirmedSurveyPoleCount", errors);
        Utc(payload.PublishedAt, "PublishedAt", errors);
        RequiredItems(payload.PlantMappings, "PlantMappings", errors);
        if (payload.AppliedPlantChangeReportIds is null)
        {
            errors.Add("AppliedPlantChangeReportIds is required; use an empty list when none were applied.");
        }
        else
        {
            foreach (var reportId in payload.AppliedPlantChangeReportIds)
            {
                RequiredId(reportId, "AppliedPlantChangeReportId", errors);
            }

            Unique(payload.AppliedPlantChangeReportIds,
                "AppliedPlantChangeReportId", errors);
        }

        if (payload.PlantMappings is null)
        {
            return errors;
        }

        Unique(payload.PlantMappings.Select(mapping => mapping.PlantId),
            "PlantId", errors);
        var plantCount = payload.PlantMappings.Count;
        Maximum(plantCount, IntegrationContractLimits.MaximumPlantMappingCount,
            "Plant mappings", errors);
        if (plantCount != payload.ConfirmedSurveyPoleCount)
        {
            errors.Add("ConfirmedSurveyPoleCount must equal the total published plant mapping count.");
        }

        foreach (var mapping in payload.PlantMappings)
        {
            RequiredId(mapping.PlantId, "PlantId", errors);
            OptionalId(mapping.SourceCandidateId, "SourceCandidateId", errors);
            OptionalId(mapping.AppliedPlantChangeReportId,
                "AppliedPlantChangeReportId", errors);
            Point(mapping.Position, "Position", errors);
            Positive(mapping.RowIndex, "RowIndex", errors);
            Positive(mapping.ColumnIndex, "ColumnIndex", errors);
            RequiredText(mapping.LifecycleStatus, "LifecycleStatus", errors);
            if (!string.Equals(mapping.LifecycleStatus,
                    PublishedPlantLifecycleStatusesV3.Active,
                    StringComparison.Ordinal))
            {
                errors.Add("Published plant mappings must have ACTIVE LifecycleStatus.");
            }
            if (mapping.AppliedPlantChangeReportId.HasValue &&
                (payload.AppliedPlantChangeReportIds is null ||
                 !payload.AppliedPlantChangeReportIds.Contains(
                     mapping.AppliedPlantChangeReportId.Value)))
            {
                errors.Add("A plant mapping references an undeclared AppliedPlantChangeReportId.");
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(PlantHealthAnalysisReadyV3? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.HandoffId, "HandoffId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.MissionId, "MissionId"),
            (payload?.FarmId, "FarmId"),
            (payload?.FarmBoundaryVersionId, "FarmBoundaryVersionId"),
            (payload?.FarmBaseMapVersionId, "FarmBaseMapVersionId"),
            (payload?.JobId, "JobId"),
            (payload?.ModelVersionId, "ModelVersionId"));

        if (payload is null)
        {
            return errors;
        }

        RequiredText(payload.ModelVersion, "ModelVersion", errors);
        OptionalId(payload.ThresholdProfileId, "ThresholdProfileId", errors);
        if (payload.ThresholdProfileId.HasValue)
        {
            RequiredText(payload.ThresholdProfileVersion,
                "ThresholdProfileVersion", errors);
        }
        else if (!string.IsNullOrWhiteSpace(payload.ThresholdProfileVersion))
        {
            errors.Add("ThresholdProfileVersion requires ThresholdProfileId.");
        }

        RequiredText(payload.BoundaryPolicyVersion, "BoundaryPolicyVersion", errors);
        Utc(payload.ProducedAt, "ProducedAt", errors);
        RequiredItems(payload.Observations, "Observations", errors);
        RequiredCollection(payload.BoundaryExceptions, "BoundaryExceptions", errors);
        RequiredCollection(payload.DiseaseZones, "DiseaseZones", errors);

        var observationCount = payload.Observations?.Count ?? 0;
        Maximum(observationCount, IntegrationContractLimits.MaximumHealthObservationCount,
            "Health observations", errors);
        var analysisItemCount = observationCount +
            (payload.BoundaryExceptions?.Count ?? 0) +
            (payload.DiseaseZones?.Count ?? 0) +
            (payload.DiseaseZones?.Sum(zone => zone.Memberships?.Count ?? 0) ?? 0);
        Maximum(analysisItemCount,
            IntegrationContractLimits.MaximumHealthObservationCount,
            "Plant health analysis items", errors);

        if (payload.Observations is not null)
        {
            Unique(payload.Observations.Select(item => item.ObservationId),
                "ObservationId", errors);
            foreach (var observation in payload.Observations)
            {
                RequiredId(observation.ObservationId, "ObservationId", errors);
                Positive(observation.ObservationVersion, "ObservationVersion", errors);
                RequiredId(observation.ZoneId, "ZoneId", errors);
                OptionalId(observation.PlantId, "PlantId", errors);
                OptionalId(observation.MappingCandidateId, "MappingCandidateId", errors);
                if (!observation.PlantId.HasValue &&
                    !observation.MappingCandidateId.HasValue)
                {
                    errors.Add("A health observation requires PlantId or MappingCandidateId.");
                }

                Point(observation.Position, "Position", errors);
                Utc(observation.ObservedAt, "ObservedAt", errors);
                Condition(observation.ConditionCode,
                    observation.ConditionDefinitionVersion,
                    observation.HealthLevelCode, errors);
                Range(observation.SeverityPercent, 0, 100,
                    "SeverityPercent", errors);
                Range(observation.Confidence, 0, 1, "Confidence", errors);
                Evidence(observation.EvidenceMedia, true, errors);
            }
        }

        if (payload.BoundaryExceptions is not null)
        {
            Unique(payload.BoundaryExceptions.Select(item => item.SourceReferenceId),
                "Boundary exception SourceReferenceId", errors);
            foreach (var exception in payload.BoundaryExceptions)
            {
                RequiredText(exception.SourceReferenceId, "SourceReferenceId", errors);
                Point(exception.OriginalPosition, "OriginalPosition", errors);
                Allowed(exception.BoundaryClassification,
                    ExceptionBoundaryClassifications, "BoundaryClassification", errors);
                NonNegative(exception.MeasuredBoundaryDistanceM,
                    "MeasuredBoundaryDistanceM", errors);
                Positive(exception.BoundaryThresholdM, "BoundaryThresholdM", errors);
                RequiredText(exception.BoundaryPolicyVersion,
                    "BoundaryPolicyVersion", errors);
                if (!string.Equals(exception.BoundaryPolicyVersion,
                        payload.BoundaryPolicyVersion,
                        StringComparison.Ordinal))
                {
                    errors.Add("Boundary exception policy version must match the payload policy version.");
                }
                Evidence(exception.EvidenceMedia, false, errors);
            }
        }

        if (payload.DiseaseZones is not null)
        {
            Unique(payload.DiseaseZones.Select(item => item.SourceProposalId),
                "Disease zone SourceProposalId", errors);
            foreach (var zone in payload.DiseaseZones)
            {
                RequiredText(zone.SourceProposalId, "SourceProposalId", errors);
                Polygon(zone.Geometry, "Geometry", errors);
                Condition(zone.ConditionCode, zone.ConditionDefinitionVersion,
                    zone.HealthLevelCode, errors);
                Positive(zone.MembershipVersion, "MembershipVersion", errors);
                RequiredItems(zone.Memberships, "Memberships", errors);
                RequiredCollection(zone.RecommendationCandidates,
                    "RecommendationCandidates", errors);
                Evidence(zone.EvidenceMedia, true, errors);

                if (zone.Memberships is not null)
                {
                    Unique(zone.Memberships.Select(item =>
                            (item.MappingCandidateId, item.PlantId)),
                        "Disease zone membership identity", errors);
                    foreach (var membership in zone.Memberships)
                    {
                        OptionalId(membership.MappingCandidateId,
                            "MappingCandidateId", errors);
                        OptionalId(membership.PlantId, "PlantId", errors);
                        if (!membership.MappingCandidateId.HasValue &&
                            !membership.PlantId.HasValue)
                        {
                            errors.Add("A disease zone membership requires PlantId or MappingCandidateId.");
                        }
                        Range(membership.Confidence, 0, 1,
                            "Membership Confidence", errors);
                    }
                }

                if (zone.RecommendationCandidates is not null)
                {
                    Unique(zone.RecommendationCandidates.Select(item => item.RecommendationId),
                        "RecommendationId", errors);
                    foreach (var recommendation in zone.RecommendationCandidates)
                    {
                        RequiredId(recommendation.RecommendationId,
                            "RecommendationId", errors);
                        RequiredText(recommendation.Code, "Recommendation Code", errors);
                        Positive(recommendation.VersionNumber,
                            "Recommendation VersionNumber", errors);
                    }
                }
            }
        }

        return errors;
    }

    private static List<string> RequiredIds(params (Guid? Value, string Name)[] values)
    {
        var errors = new List<string>();
        foreach (var (value, name) in values)
        {
            if (!value.HasValue || value.Value == Guid.Empty)
            {
                errors.Add($"{name} is required.");
            }
        }

        return errors;
    }

    private static void Condition(string? code, string? version,
        string? healthLevelCode, List<string> errors)
    {
        RequiredText(code, "ConditionCode", errors);
        RequiredText(version, "ConditionDefinitionVersion", errors);
        RequiredText(healthLevelCode, "HealthLevelCode", errors);
    }

    private static void Evidence(IReadOnlyList<EvidenceMediaReferenceV3>? evidence,
        bool required, List<string> errors)
    {
        if (evidence is null || (required && evidence.Count == 0))
        {
            errors.Add(required
                ? "EvidenceMedia must contain at least one item."
                : "EvidenceMedia is required; use an empty list when none exists.");
            return;
        }

        Unique(evidence.Select(item => item.MediaAssetId),
            "Evidence MediaAssetId", errors);
        foreach (var item in evidence)
        {
            RequiredId(item.MediaAssetId, "MediaAssetId", errors);
            RequiredText(item.Role, "Evidence Role", errors);
        }
    }

    private static void Point(GeoJsonPointV3? point, string name,
        List<string> errors)
    {
        if (point is null || !string.Equals(point.Type, "Point",
                StringComparison.Ordinal) || point.Coordinates is null ||
            point.Coordinates.Count != 2)
        {
            errors.Add($"{name} must be an RFC 7946 GeoJSON Point.");
            return;
        }

        var longitude = point.Coordinates[0];
        var latitude = point.Coordinates[1];
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180 ||
            !double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            errors.Add($"{name} coordinates are outside WGS84 ranges.");
        }
    }

    private static void Polygon(GeoJsonPolygonV3? polygon, string name,
        List<string> errors)
    {
        if (polygon is null || !string.Equals(polygon.Type, "Polygon",
                StringComparison.Ordinal) || polygon.Coordinates is null ||
            polygon.Coordinates.Count == 0)
        {
            errors.Add($"{name} must be an RFC 7946 GeoJSON Polygon.");
            return;
        }

        foreach (var ring in polygon.Coordinates)
        {
            if (ring is null || ring.Count < 4)
            {
                errors.Add($"{name} rings must contain at least four positions.");
                continue;
            }

            foreach (var position in ring)
            {
                Point(new GeoJsonPointV3("Point", position), name, errors);
            }

            var first = ring[0];
            var last = ring[^1];
            if (first.Count != 2 || last.Count != 2 ||
                first[0] != last[0] || first[1] != last[1])
            {
                errors.Add($"{name} rings must be closed.");
            }
        }
    }

    private static void GridPosition(int? row, int? column, List<string> errors)
    {
        if (row.HasValue != column.HasValue)
        {
            errors.Add("ProposedRowIndex and ProposedColumnIndex must be provided together.");
        }

        if (row.HasValue)
        {
            Positive(row.Value, "ProposedRowIndex", errors);
            Positive(column!.Value, "ProposedColumnIndex", errors);
        }
    }

    private static void Bearing(double value, string name, List<string> errors)
    {
        if (!double.IsFinite(value) || value is < 0 or >= 360)
        {
            errors.Add($"{name} must be in the range [0, 360).");
        }
    }

    private static void PositiveMeasurement(double value, string name,
        List<string> errors)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            errors.Add($"{name} must be a finite value greater than zero.");
        }
    }

    private static void Allowed(string? value, HashSet<string> values,
        string name, List<string> errors)
    {
        if (value is null || !values.Contains(value))
        {
            errors.Add($"{name} has an unsupported value.");
        }
    }

    private static void RequiredId(Guid value, string name, List<string> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add($"{name} is required.");
        }
    }

    private static void OptionalId(Guid? value, string name, List<string> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add($"{name} cannot be an empty GUID when provided.");
        }
    }

    private static void RequiredText(string? value, string name,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
        }
    }

    private static void RequiredItems<T>(IReadOnlyCollection<T>? values,
        string name, List<string> errors)
    {
        if (values is null || values.Count == 0)
        {
            errors.Add($"{name} must contain at least one item.");
        }
    }

    private static void RequiredCollection<T>(IReadOnlyCollection<T>? values,
        string name, List<string> errors)
    {
        if (values is null)
        {
            errors.Add($"{name} is required; use an empty list when none exists.");
        }
    }

    private static void Positive(decimal value, string name, List<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"{name} must be greater than zero.");
        }
    }

    private static void NonNegative(decimal value, string name,
        List<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{name} cannot be negative.");
        }
    }

    private static void Range(decimal value, decimal minimum, decimal maximum,
        string name, List<string> errors)
    {
        if (value < minimum || value > maximum)
        {
            errors.Add($"{name} must be in the range [{minimum}, {maximum}].");
        }
    }

    private static void Maximum(int count, int maximum, string name,
        List<string> errors)
    {
        if (count > maximum)
        {
            errors.Add($"{name} cannot exceed {maximum} items.");
        }
    }

    private static void Unique<T>(IEnumerable<T> values, string name,
        List<string> errors) where T : notnull
    {
        var items = values.ToArray();
        if (items.Distinct().Count() != items.Length)
        {
            errors.Add($"{name} values must be unique.");
        }
    }

    private static void Utc(DateTimeOffset value, string name,
        List<string> errors)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            errors.Add($"{name} must be a non-default UTC timestamp.");
        }
    }
}
