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
}
