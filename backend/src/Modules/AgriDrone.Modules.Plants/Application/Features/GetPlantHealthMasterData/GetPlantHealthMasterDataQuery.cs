using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;

public sealed record GetPlantHealthMasterDataQuery
    : IRequest<Result<PlantHealthMasterDataResponse>>;

public sealed record PlantHealthMasterDataResponse(
    IReadOnlyList<PlantHealthLabelResponse> Labels,
    IReadOnlyList<SourcedGuidanceResponse> Guidance,
    DateTimeOffset EvaluatedAt,
    string ResultNature,
    string ClaimBoundary);

public sealed record PlantHealthLabelResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScientificName,
    ConditionType ConditionType,
    PlantHealthLabelKind LabelKind,
    int RevisionNumber,
    string DatasetVersion,
    string AnnotationRuleVersion,
    string TeamLabelLimitation);

public sealed record SourcedGuidanceResponse(
    Guid Id,
    string Code,
    int VersionNumber,
    Guid PlantConditionId,
    int PlantConditionRevisionNumber,
    string SuspectedClassCode,
    string SourceTitle,
    string SourceReference,
    string GuidanceText,
    SourcedGuidanceStatus Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    uint Version);
