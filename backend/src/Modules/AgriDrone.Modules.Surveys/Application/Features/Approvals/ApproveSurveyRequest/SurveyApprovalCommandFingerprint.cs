using System.Security.Cryptography;
using System.Text.Json;

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
            command.Checklist.ContactValid,
            command.Checklist.IsDragonFruitFarm,
            command.Checklist.IsServiceAreaSupported,
            command.Checklist.IsLocationSufficient,
            command.Checklist.IsPreliminaryLegalAndFlightFeasible,
            command.Checklist.IsEligibleSystemManagerAvailable,
            command.Checklist.IsWrittenConsentFeasible,
            command.Checklist.IsFlightPermissionFeasible,
            ChecklistNotes = string.IsNullOrWhiteSpace(command.Checklist.Notes)
                ? null
                : command.Checklist.Notes.Trim(),
            command.ExpectedVersion
        });

        return Convert.ToHexString(SHA256.HashData(canonicalPayload));
    }
}
