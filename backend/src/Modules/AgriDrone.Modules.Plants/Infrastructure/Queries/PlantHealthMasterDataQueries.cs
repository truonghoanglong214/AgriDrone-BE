using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class PlantHealthMasterDataQueries(PlantsDbContext context)
    : IPlantHealthMasterDataQueries
{
    public async Task<PlantHealthMasterDataResponse> GetActiveAsync(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default)
    {
        var labels = await context.PlantConditions
            .AsNoTracking()
            .Where(condition =>
                condition.IsActive &&
                condition.PlantHealthLabelKind != null &&
                condition.DatasetVersion != null &&
                condition.AnnotationRuleVersion != null &&
                condition.TeamLabelLimitation != null)
            .OrderBy(condition => condition.PlantHealthLabelKind)
            .ThenBy(condition => condition.Code)
            .Select(condition => new PlantHealthLabelResponse(
                condition.Id,
                condition.Code,
                condition.Name,
                condition.ScientificName,
                condition.ConditionType,
                condition.PlantHealthLabelKind!.Value,
                condition.RevisionNumber,
                condition.DatasetVersion!,
                condition.AnnotationRuleVersion!,
                condition.TeamLabelLimitation!))
            .ToListAsync(cancellationToken);

        var guidance = await context.SourcedGuidance
            .AsNoTracking()
            .Where(item =>
                item.PlantConditionId != null &&
                item.PlantConditionRevisionNumber != null &&
                item.PlantCondition != null &&
                item.PlantCondition.IsActive &&
                item.PlantCondition.PlantHealthLabelKind ==
                    PlantHealthLabelKind.SuspectedDisease &&
                item.Status == SourcedGuidanceStatus.Active &&
                item.EffectiveFrom <= evaluatedAt &&
                (!item.EffectiveTo.HasValue || evaluatedAt < item.EffectiveTo.Value))
            .OrderBy(item => item.SuspectedClassCode)
            .ThenBy(item => item.Code)
            .ThenByDescending(item => item.VersionNumber)
            .Select(ToResponseExpression)
            .ToListAsync(cancellationToken);

        return new PlantHealthMasterDataResponse(
            labels,
            guidance,
            evaluatedAt,
            "PRELIMINARY",
            "Team-labelled suspected classes only; this catalogue does not represent an expert diagnosis.");
    }

    public async Task<IReadOnlyList<SourcedGuidanceResponse>> GetGuidanceHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await context.SourcedGuidance
            .AsNoTracking()
            .Where(item =>
                item.PlantConditionId != null &&
                item.PlantConditionRevisionNumber != null)
            .OrderBy(item => item.Code)
            .ThenByDescending(item => item.VersionNumber)
            .Select(ToResponseExpression)
            .ToListAsync(cancellationToken);
    }

    private static readonly System.Linq.Expressions.Expression<
        Func<SourcedGuidance, SourcedGuidanceResponse>> ToResponseExpression =
        item => new SourcedGuidanceResponse(
            item.Id,
            item.Code,
            item.VersionNumber,
            item.PlantConditionId!.Value,
            item.PlantConditionRevisionNumber!.Value,
            item.SuspectedClassCode,
            item.SourceTitle,
            item.SourceReference,
            item.GuidanceText,
            item.Status,
            item.EffectiveFrom,
            item.EffectiveTo,
            item.Version);
}
