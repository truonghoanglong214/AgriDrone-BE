using System.Text.Json;
using System.Text.Json.Nodes;
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
        const string code = "DRONE_PRE_FLIGHT";
        var activeDefinition = await checklistRepository.GetActiveDefinitionAsync(
            code,
            cancellationToken);
        if (request.CreateOnly && activeDefinition is not null)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                AppError.Conflict(
                    "MissionPreflight.ChecklistAlreadyExists",
                    "The shared drone preflight checklist has already been created."));
        }

        if (!request.CreateOnly && request.Items is null && activeDefinition is null)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                AppError.NotFound(
                    "MissionPreflight.ChecklistNotFound",
                    "Create the shared preflight checklist before changing its conditions."));
        }

        JsonDocument composedDocument;
        try
        {
            composedDocument = request.Items is not null
                ? JsonDocument.Parse(request.Items.RootElement.GetRawText())
                : ComposeItems(activeDefinition!, request.ItemsToAdd, request.KeysToRemove);
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                AppError.Validation(
                    "MissionPreflight.InvalidConditionChange",
                    exception.Message));
        }

        using var composedItems = composedDocument;
        var definitionError = PreflightChecklistPolicy.ValidateDefinition(
            composedItems.RootElement);
        if (definitionError is not null)
        {
            return Result.Failure<ManagePreflightChecklistDefinitionResult>(
                definitionError);
        }

        var nextVersion = await checklistRepository.GetNextVersionNumberAsync(
            code,
            cancellationToken);
        await checklistRepository.RetireActiveDefinitionsAsync(
            code,
            now,
            cancellationToken);

        var definition = PreflightChecklistDefinition.CreateActive(
            code,
            nextVersion,
            composedItems,
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

    private static JsonDocument ComposeItems(
        PreflightChecklistDefinition activeDefinition,
        JsonDocument? itemsToAdd,
        IReadOnlyList<string>? keysToRemove)
    {
        var items = JsonNode.Parse(activeDefinition.Items.RootElement.GetRawText())
            as JsonArray ?? [];
        var keys = keysToRemove?.ToHashSet(StringComparer.Ordinal) ?? [];
        var foundKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var index = items.Count - 1; index >= 0; index--)
        {
            var key = items[index]?["key"]?.GetValue<string>();
            if (key is not null && keys.Contains(key))
            {
                items.RemoveAt(index);
                foundKeys.Add(key);
            }
        }

        if (keys.Except(foundKeys, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException(
                "Every condition key to remove must exist in the active checklist.");
        }

        if (itemsToAdd is not null)
        {
            if (itemsToAdd.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "Conditions to add must be a JSON array.");
            }

            foreach (var item in itemsToAdd.RootElement.EnumerateArray())
            {
                items.Add(JsonNode.Parse(item.GetRawText()));
            }
        }

        return JsonDocument.Parse(items.ToJsonString());
    }
}
