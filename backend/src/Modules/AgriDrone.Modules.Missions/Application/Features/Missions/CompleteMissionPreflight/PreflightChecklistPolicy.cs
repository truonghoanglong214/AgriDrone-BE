using System.Text.Json;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

internal static class PreflightChecklistPolicy
{
    public static AppError? ValidateDefinition(JsonElement items)
    {
        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
        {
            return DefinitionInvalid();
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("key", out var keyValue) ||
                keyValue.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(keyValue.GetString()) ||
                !item.TryGetProperty("prompt", out var promptValue) ||
                promptValue.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(promptValue.GetString()))
            {
                return DefinitionInvalid();
            }

            var key = keyValue.GetString()!.Trim();
            if (key.Length > 100 || !keys.Add(key))
            {
                return DefinitionInvalid();
            }

            if (item.TryGetProperty("required", out var requiredValue) &&
                requiredValue.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                return DefinitionInvalid();
            }
        }

        return null;
    }

    public static AppError? ValidateResponses(
        JsonElement items,
        JsonElement responses)
    {
        var definitionError = ValidateDefinition(items);
        if (definitionError is not null)
        {
            return definitionError;
        }

        if (responses.ValueKind != JsonValueKind.Object)
        {
            return ResponsesInvalid();
        }

        var knownKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var key = item.GetProperty("key").GetString()!.Trim();
            knownKeys.Add(key);
            var required = !item.TryGetProperty("required", out var requiredValue) ||
                           requiredValue.GetBoolean();
            if (required &&
                (!responses.TryGetProperty(key, out var response) ||
                 response.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined))
            {
                return ResponsesInvalid();
            }
        }

        return responses.EnumerateObject().Any(answer => !knownKeys.Contains(answer.Name))
            ? ResponsesInvalid()
            : null;
    }

    private static AppError DefinitionInvalid() =>
        AppError.Validation(
            "MissionPreflight.InvalidDefinition",
            "Every checklist item must have a unique key, a prompt, and an optional boolean required flag.");

    private static AppError ResponsesInvalid() =>
        AppError.Validation(
            "MissionPreflight.InvalidAnswers",
            "Checklist answers must include every required item and cannot contain unknown item keys.");
}
