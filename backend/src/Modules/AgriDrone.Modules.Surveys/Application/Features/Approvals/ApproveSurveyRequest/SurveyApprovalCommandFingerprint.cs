using System.Security.Cryptography;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal static class SurveyApprovalCommandFingerprint
{
    public static string Create(ApproveSurveyRequestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Checklist);

        var canonicalPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            command.SurveyRequestId,
            command.SelectedSystemManagerProfileId,
            Reason = command.Reason.Trim(),
            ChecklistVersion = command.Checklist.Version,
            ChecklistAnswers = SurveyRequestReviewChecklistDefinition
                .CanonicalizeAnswers(command.Checklist.Answers),
            ChecklistNotes = string.IsNullOrWhiteSpace(command.Checklist.Notes)
                ? null
                : command.Checklist.Notes.Trim(),
            command.ExpectedVersion
        });

        return Convert.ToHexString(SHA256.HashData(canonicalPayload));
    }
}
