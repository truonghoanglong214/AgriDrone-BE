using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;

internal sealed class ManagePreflightChecklistDefinitionCommandHandler(
    IPreflightChecklistRepository checklistRepository,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<ManagePreflightChecklistDefinitionCommand, Result<ManagePreflightChecklistDefinitionResult>>
{
    public async Task<Result<ManagePreflightChecklistDefinitionResult>> Handle(
        ManagePreflightChecklistDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                AppError.Unauthorized("MissionPreflight.ActorRequired", "An authenticated SystemAdmin is required."));
        }

        var now = timeProvider.GetUtcNow();
        var definitionError = PreflightChecklistPolicy.ValidateDefinition(
            request.Items.RootElement);
        if (definitionError is not null)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                definitionError);
        }

        const string code = "DRONE_PRE_FLIGHT";
        var nextVersion = await checklistRepository.GetNextVersionNumberAsync(
            code,
            cancellationToken);
        await checklistRepository.RetireActiveDefinitionsAsync(
            code,
            now,
            cancellationToken);

        using var items = JsonDocument.Parse(request.Items.RootElement.GetRawText());
        var definition = PreflightChecklistDefinition.CreateActive(
            code,
            nextVersion,
            items,
            actorId,
            now);
        checklistRepository.AddDefinition(definition);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            definition.Code,
            definition.VersionNumber,
            definition.Items
        });
        auditWriter.AddSystemAdminAction(
            unitOfWork,
            actorId,
            executionContext.CorrelationId,
            nameof(PreflightChecklistDefinition),
            definition.Id,
            "ACTIVATE_PREFLIGHT_CHECKLIST_DEFINITION",
            null,
            auditData,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PreflightChecklistConflictException)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                AppError.Conflict(
                    "MissionPreflight.ConcurrentDefinitionUpdate",
                    "Another administrator changed the active checklist. Reload and retry."));
        }
        return Result.Success(new ManagePreflightChecklistDefinitionResult(
            definition.Id,
            definition.Code,
            definition.VersionNumber,
            definition.EffectiveFrom));
    }
}
