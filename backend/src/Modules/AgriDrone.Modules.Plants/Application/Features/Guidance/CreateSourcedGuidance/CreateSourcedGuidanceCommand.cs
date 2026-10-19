using AgriDrone.Modules.Plants.Application.Features.GetPlantHealthMasterData;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Plants.Application.Features.Guidance.CreateSourcedGuidance;

public sealed record CreateSourcedGuidanceCommand(
    string Code,
    Guid PlantConditionId,
    string SourceTitle,
    string SourceReference,
    string GuidanceText,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo)
    : IRequest<Result<SourcedGuidanceResponse>>;
