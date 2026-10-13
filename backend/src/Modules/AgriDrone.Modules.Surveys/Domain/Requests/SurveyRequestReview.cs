using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyRequestReview : Entity
{
    private SurveyRequestReview() { }

    public Guid SurveyRequestId { get; private set; }
    public SurveyReviewDecision Decision { get; private set; }
    public JsonDocument ChecklistSnapshot { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public Guid ReviewedBy { get; private set; }
    public DateTimeOffset ReviewedAt { get; private set; }
    public SurveyRequest SurveyRequest { get; private set; } = null!;

    internal static SurveyRequestReview Create(
        Guid surveyRequestId,
        SurveyReviewDecision decision,
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            SurveyRequestId = surveyRequestId,
            Decision = decision,
            ChecklistSnapshot = JsonDocument.Parse(checklistSnapshot.RootElement.GetRawText()),
            Reason = reason,
            ReviewedBy = reviewedBy,
            ReviewedAt = reviewedAt
        };
}
