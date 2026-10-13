using AgriDrone.IntegrationContracts.HarvestReadiness;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Surveys;

namespace AgriDrone.IntegrationContracts.Messaging.Validation;

public static class V2BusinessContractValidators
{
    public static IReadOnlyList<string> Validate(
        BaselineMappingCandidatesApprovedV2? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.ApprovalId, "ApprovalId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.MissionId, "MissionId"),
            (payload?.FarmId, "FarmId"));

        if (payload is null)
        {
            return errors;
        }

        RequiredText(payload.AlgorithmVersion, "AlgorithmVersion", errors);
        RequiredItems(payload.Zones, "Zones", errors);
        OptionalId(payload.ExpectedCurrentFarmBaseMapVersionId, "ExpectedCurrentFarmBaseMapVersionId", errors);
        if (payload.Zones is not null)
        {
            if (payload.Zones.Select(zone => zone.ZoneId).Distinct().Count() != payload.Zones.Count)
            {
                errors.Add("ZoneId values must be unique.");
            }

            foreach (var zone in payload.Zones)
            {
                RequiredId(zone.ZoneId, "ZoneId", errors);
                RequiredItems(zone.Candidates, "Candidates", errors);
                OptionalId(zone.ExpectedCurrentZoneMapVersionId, "ExpectedCurrentZoneMapVersionId", errors);
                PositiveMeasurement(zone.RowSpacingM, "RowSpacingM", errors);
                PositiveMeasurement(zone.PlantSpacingM, "PlantSpacingM", errors);
                if (!double.IsFinite(zone.GridBearingDeg) || zone.GridBearingDeg is < 0 or >= 360)
                {
                    errors.Add("GridBearingDeg must be in the range [0, 360).");
                }

                if (zone.Candidates is not null)
                {
                    foreach (var candidate in zone.Candidates)
                    {
                        RequiredId(candidate.ObservationId, "ObservationId", errors);
                        OptionalId(candidate.ResolvedPlantId, "ResolvedPlantId", errors);
                        if (candidate.Latitude is < -90 or > 90 || !double.IsFinite(candidate.Latitude))
                        {
                            errors.Add("Candidate Latitude is out of range.");
                        }

                        if (candidate.Longitude is < -180 or > 180 || !double.IsFinite(candidate.Longitude))
                        {
                            errors.Add("Candidate Longitude is out of range.");
                        }

                        if (candidate.PositionConfidence is < 0 or > 1 || !double.IsFinite(candidate.PositionConfidence))
                        {
                            errors.Add("Candidate PositionConfidence must be in the range [0, 1].");
                        }
                    }
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(FarmBaseMapPublishedV2? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.PublicationId, "PublicationId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.SourceMissionId, "SourceMissionId"),
            (payload?.FarmId, "FarmId"),
            (payload?.FarmBaseMapVersionId, "FarmBaseMapVersionId"));

        if (payload is null)
        {
            return errors;
        }

        Positive(payload.VersionNumber, "VersionNumber", errors);
        Utc(payload.PublishedAt, "PublishedAt", errors);
        RequiredItems(payload.Zones, "Zones", errors);
        if (payload.Zones is not null)
        {
            if (payload.Zones.Select(zone => zone.ZoneId).Distinct().Count() != payload.Zones.Count)
            {
                errors.Add("ZoneId values must be unique.");
            }

            foreach (var zone in payload.Zones)
            {
                RequiredId(zone.ZoneId, "ZoneId", errors);
                RequiredId(zone.ZoneMapVersionId, "ZoneMapVersionId", errors);
                Positive(zone.VersionNumber, "Zone VersionNumber", errors);
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(HealthObservationsReadyV2? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.HandoffId, "HandoffId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.MissionId, "MissionId"),
            (payload?.FarmId, "FarmId"),
            (payload?.FarmBaseMapVersionId, "FarmBaseMapVersionId"),
            (payload?.JobId, "JobId"),
            (payload?.ModelVersionId, "ModelVersionId"));

        if (payload is null)
        {
            return errors;
        }

        RequiredText(payload.ModelVersion, "ModelVersion", errors);
        RequiredItems(payload.Observations, "Observations", errors);
        if (payload.Observations is not null)
        {
            foreach (var observation in payload.Observations)
            {
                RequiredId(observation.ObservationId, "ObservationId", errors);
                RequiredId(observation.ZoneId, "ZoneId", errors);
                RequiredId(observation.PlantId, "PlantId", errors);
                RequiredId(observation.MediaAssetId, "MediaAssetId", errors);
                Positive(observation.ObservationVersion, "ObservationVersion", errors);
                Utc(observation.ObservedAt, "ObservedAt", errors);
                RequiredText(observation.ConditionCode, "ConditionCode", errors);
                RequiredText(observation.ConditionDefinitionVersion, "ConditionDefinitionVersion", errors);
                RequiredText(observation.HealthLevelCode, "HealthLevelCode", errors);
                Range(observation.SeverityPercent, 0, 100, "SeverityPercent", errors);
                Range(observation.Confidence, 0, 1, "Confidence", errors);
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(
        HarvestReadinessAssessmentsReadyV2? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.HandoffId, "HandoffId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.MissionId, "MissionId"),
            (payload?.FarmId, "FarmId"),
            (payload?.FarmBaseMapVersionId, "FarmBaseMapVersionId"),
            (payload?.JobId, "JobId"),
            (payload?.ModelVersionId, "ModelVersionId"));

        if (payload is null)
        {
            return errors;
        }

        RequiredText(payload.ModelVersion, "ModelVersion", errors);
        RequiredText(payload.CriteriaVersion, "CriteriaVersion", errors);
        RequiredText(payload.AssessmentGranularity, "AssessmentGranularity", errors);
        RequiredItems(payload.Assessments, "Assessments", errors);
        if (payload.Assessments is not null)
        {
            foreach (var assessment in payload.Assessments)
            {
                RequiredId(assessment.AssessmentId, "AssessmentId", errors);
                RequiredId(assessment.ZoneId, "ZoneId", errors);
                OptionalId(assessment.PlantId, "PlantId", errors);
                RequiredText(assessment.AiAssessment, "AiAssessment", errors);
                if (assessment.Confidence.HasValue)
                {
                    Range(assessment.Confidence.Value, 0, 1, "Confidence", errors);
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(
        SurveyResultReviewStateChangedV2? payload)
    {
        var errors = RequiredIds(
            (payload?.CausationId, "CausationId"),
            (payload?.HandoffId, "HandoffId"),
            (payload?.SurveyOrderId, "SurveyOrderId"),
            (payload?.MissionId, "MissionId"),
            (payload?.FarmId, "FarmId"),
            (payload?.SurveyResultId, "SurveyResultId"));

        if (payload is null)
        {
            return errors;
        }

        RequiredText(payload.ServiceType, "ServiceType", errors);
        RequiredText(payload.State, "State", errors);
        Positive(payload.ReviewVersion, "ReviewVersion", errors);
        Utc(payload.ChangedAt, "ChangedAt", errors);
        if (payload.TotalItems < 0 || payload.PendingItems < 0 || payload.CorrectedItems < 0)
        {
            errors.Add("Review item counts cannot be negative.");
        }

        if (payload.PendingItems > payload.TotalItems || payload.CorrectedItems > payload.TotalItems)
        {
            errors.Add("Review item counts cannot exceed TotalItems.");
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

    private static void RequiredId(Guid value, string name, List<string> errors)
    {
        if (value == Guid.Empty)
        {
            errors.Add($"{name} is required.");
        }
    }

    private static void RequiredText(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
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

    private static void RequiredItems<T>(IReadOnlyCollection<T>? values, string name, List<string> errors)
    {
        if (values is null || values.Count == 0)
        {
            errors.Add($"{name} must contain at least one item.");
        }
    }

    private static void Positive(long value, string name, List<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"{name} must be greater than zero.");
        }
    }

    private static void PositiveMeasurement(double value, string name, List<string> errors)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            errors.Add($"{name} must be a finite value greater than zero.");
        }
    }

    private static void Range(
        decimal value,
        decimal minimum,
        decimal maximum,
        string name,
        List<string> errors)
    {
        if (value < minimum || value > maximum)
        {
            errors.Add($"{name} must be in the range [{minimum}, {maximum}].");
        }
    }

    private static void Utc(DateTimeOffset value, string name, List<string> errors)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            errors.Add($"{name} must be a non-default UTC timestamp.");
        }
    }
}
