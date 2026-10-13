using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class HarvestReadinessAssessment : Entity
{
    private HarvestReadinessAssessment() { }

    public Guid SurveyResultId { get; private set; }
    public Guid SurveyOrderId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid? PlantId { get; private set; }
    public Guid? MissionId { get; private set; }
    public Guid? HarvestReadinessCriterionId { get; private set; }
    public string? CriteriaCode { get; private set; }
    public int? CriteriaVersionNumber { get; private set; }
    public string? CriteriaStatusSnapshot { get; private set; }
    public Guid? AiModelVersionId { get; private set; }
    public Guid? AiThresholdProfileId { get; private set; }
    public string AssessmentGranularity { get; private set; } = null!;
    public string CriteriaVersion { get; private set; } = null!;
    public JsonDocument VisibleIndicators { get; private set; } = null!;
    public string AiAssessment { get; private set; } = null!;
    public decimal? AiConfidence { get; private set; }
    public string? CorrectedAssessment { get; private set; }
    public JsonDocument Evidence { get; private set; } = null!;
    public HarvestReadinessReviewStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyResult SurveyResult { get; private set; } = null!;
    public HarvestReadinessCriterion? Criterion { get; private set; }

    public static HarvestReadinessAssessment CreatePending(
        Guid surveyResultId,
        Guid surveyOrderId,
        Guid farmId,
        Guid? plantId,
        Guid? missionId,
        HarvestReadinessCriterion criterion,
        Guid? aiModelVersionId,
        Guid? aiThresholdProfileId,
        JsonDocument visibleIndicators,
        string aiAssessment,
        decimal? aiConfidence,
        JsonDocument evidence,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyResultId);
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(farmId);
        EnsureOptionalId(plantId, nameof(plantId));
        EnsureOptionalId(missionId, nameof(missionId));
        EnsureOptionalId(aiModelVersionId, nameof(aiModelVersionId));
        EnsureOptionalId(aiThresholdProfileId, nameof(aiThresholdProfileId));
        ArgumentNullException.ThrowIfNull(criterion);
        ArgumentNullException.ThrowIfNull(visibleIndicators);
        ArgumentNullException.ThrowIfNull(evidence);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(aiAssessment);
        var normalizedAiAssessment = aiAssessment.Trim();
        if (normalizedAiAssessment.Length > 100)
        {
            throw new ArgumentException(
                "AI assessment cannot exceed 100 characters.",
                nameof(aiAssessment));
        }

        if (!criterion.IsAvailableAt(createdAt))
        {
            throw new HarvestReadinessCriterionDomainException(
                HarvestReadinessCriterionErrorCodes.CriterionNotAvailable,
                $"Criterion '{criterion.Code}' version {criterion.VersionNumber} is not available at '{createdAt:O}'.");
        }

        if (criterion.Granularity == HarvestReadinessGranularity.Plant &&
            !plantId.HasValue)
        {
            throw new ArgumentException(
                "Plant-level assessment requires a plant identifier.",
                nameof(plantId));
        }

        if (criterion.Granularity == HarvestReadinessGranularity.Farm &&
            plantId.HasValue)
        {
            throw new ArgumentException(
                "Farm-level assessment cannot target a single plant.",
                nameof(plantId));
        }

        if (aiThresholdProfileId.HasValue && !aiModelVersionId.HasValue)
        {
            throw new ArgumentException(
                "AI threshold provenance requires an AI model version.",
                nameof(aiThresholdProfileId));
        }

        if (aiConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aiConfidence),
                aiConfidence,
                "AI confidence must be between zero and one.");
        }

        return new HarvestReadinessAssessment
        {
            Id = Guid.NewGuid(),
            SurveyResultId = surveyResultId,
            SurveyOrderId = surveyOrderId,
            FarmId = farmId,
            PlantId = plantId,
            MissionId = missionId,
            HarvestReadinessCriterionId = criterion.Id,
            CriteriaCode = criterion.Code,
            CriteriaVersionNumber = criterion.VersionNumber,
            CriteriaStatusSnapshot = criterion.Status.ToString().ToUpperInvariant(),
            AiModelVersionId = aiModelVersionId,
            AiThresholdProfileId = aiThresholdProfileId,
            AssessmentGranularity = criterion.Granularity.ToString().ToUpperInvariant(),
            CriteriaVersion = $"{criterion.Code}:v{criterion.VersionNumber}",
            VisibleIndicators = Clone(visibleIndicators),
            AiAssessment = normalizedAiAssessment,
            AiConfidence = aiConfidence,
            Evidence = Clone(evidence),
            Status = HarvestReadinessReviewStatus.Pending,
            CreatedAt = createdAt
        };
    }

    private static void EnsureOptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Identifier cannot be empty when provided.",
                parameterName);
        }
    }

    private static JsonDocument Clone(JsonDocument document) =>
        JsonDocument.Parse(document.RootElement.GetRawText());
}
