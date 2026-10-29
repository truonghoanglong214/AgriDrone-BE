using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using MediatR;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetAllPreflightChecklistDefinitions;

internal sealed class GetAllPreflightChecklistDefinitionsQueryHandler(
    IPreflightChecklistRepository checklistRepository)
    : IRequestHandler<
        GetAllPreflightChecklistDefinitionsQuery,
        Result<IReadOnlyList<PreflightChecklistDefinitionResponse>>>
{
    public async Task<Result<IReadOnlyList<PreflightChecklistDefinitionResponse>>> Handle(
        GetAllPreflightChecklistDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var definitions = await checklistRepository.GetAllDefinitionsAsync(
            cancellationToken);

        var response = definitions.Select(definition =>
            new PreflightChecklistDefinitionResponse(
                definition.Id,
                definition.Code,
                definition.VersionNumber,
                definition.Status,
                definition.Items.RootElement.Clone(),
                definition.EffectiveFrom,
                definition.RetiredAt,
                definition.CreatedBy,
                definition.CreatedAt))
            .ToArray();

        return Result.Success<IReadOnlyList<PreflightChecklistDefinitionResponse>>(
            response);
    }
}
