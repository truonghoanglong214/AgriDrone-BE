using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal interface IHarvestReadinessCriterionQueries
{
    Task<HarvestReadinessCriterionVersion?> GetEffectiveAsync(
        string code,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HarvestReadinessCriterionVersion>> GetHistoryAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HarvestReadinessAssessmentCriterionSnapshot>>
        GetResultSnapshotsAsync(
            Guid surveyResultId,
            CancellationToken cancellationToken = default);
}

internal sealed record HarvestReadinessCriterionVersion(
    Guid CriterionId,
    string Code,
    int VersionNumber,
    string Name,
    string Description,
    HarvestReadinessGranularity Granularity,
    HarvestReadinessCriterionStatus Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

internal sealed record HarvestReadinessAssessmentCriterionSnapshot(
    Guid AssessmentId,
    Guid SurveyResultId,
    Guid? CriterionId,
    string CriteriaCode,
    int? CriteriaVersionNumber,
    string CriteriaStatus,
    string AssessmentGranularity,
    Guid? AiModelVersionId,
    Guid? AiThresholdProfileId);
