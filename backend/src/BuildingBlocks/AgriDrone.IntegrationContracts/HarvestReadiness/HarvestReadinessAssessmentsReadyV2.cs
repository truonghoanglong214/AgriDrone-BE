namespace AgriDrone.IntegrationContracts.HarvestReadiness;

public sealed record HarvestReadinessAssessmentsReadyV2(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid FarmBaseMapVersionId,
    Guid JobId,
    Guid ModelVersionId,
    string ModelVersion,
    string CriteriaVersion,
    string AssessmentGranularity,
    IReadOnlyList<HarvestReadinessAssessmentV2> Assessments);

public sealed record HarvestReadinessAssessmentV2(
    Guid AssessmentId,
    Guid ZoneId,
    Guid? PlantId,
    string AiAssessment,
    decimal? Confidence,
    IReadOnlyDictionary<string, string> VisibleIndicators,
    IReadOnlyList<Guid> EvidenceMediaAssetIds);
