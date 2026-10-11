using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal static class SurveyApprovalReplaySnapshot
{
    public static JsonDocument Create(ApproveSurveyRequestResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonSerializer.SerializeToDocument(response);
    }

    public static Result<ApproveSurveyRequestResponse> Restore(
        SurveyApprovalIdempotencyRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.ResultSnapshot is null || !record.CompletedAt.HasValue)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.IdempotencyResultUnavailable());
        }

        try
        {
            var response = record.ResultSnapshot.Deserialize<
                ApproveSurveyRequestResponse>();
            return response is null
                ? Result.Failure<ApproveSurveyRequestResponse>(
                    SurveyApprovalError.IdempotencyResultUnavailable())
                : Result.Success(response);
        }
        catch (JsonException)
        {
            return Result.Failure<ApproveSurveyRequestResponse>(
                SurveyApprovalError.IdempotencyResultUnavailable());
        }
    }
}
