using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

internal static class SurveyRequestReviewAuditSnapshot
{
    public static JsonDocument Create(
        SurveyRequest request,
        Guid? reviewId = null,
        string? checklistVersion = null) =>
        JsonSerializer.SerializeToDocument(new
        {
            request.RequestNumber,
            request.Kind,
            request.Status,
            request.TenantId,
            request.FarmId,
            request.SurveyServiceId,
            request.Version,
            ReviewId = reviewId,
            ChecklistVersion = checklistVersion
        });
}
