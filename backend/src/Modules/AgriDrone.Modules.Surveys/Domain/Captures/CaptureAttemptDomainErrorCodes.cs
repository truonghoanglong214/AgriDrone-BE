namespace AgriDrone.Modules.Surveys.Domain;

public static class CaptureAttemptDomainErrorCodes
{
    public const string InvalidTransition = "CaptureAttempt.InvalidTransition";
    public const string AttemptTerminal = "CaptureAttempt.AttemptTerminal";
    public const string VersionConflict = "CaptureAttempt.VersionConflict";
    public const string OperationConflict = "CaptureAttempt.OperationConflict";
    public const string CrossOrderParentMismatch = "CaptureAttempt.CrossOrderParentMismatch";
    public const string InvalidParentStatus = "CaptureAttempt.InvalidParentStatus";
    public const string ReflightApprovalRequired = "CaptureAttempt.ReflightApprovalRequired";
    public const string BoundMissionRequired = "CaptureAttempt.BoundMissionRequired";
    public const string CancellationNotAllowed = "CaptureAttempt.CancellationNotAllowed";
    public const string PreflightEvidenceRequired = "CaptureAttempt.PreflightEvidenceRequired";
    public const string FlightCompletionEvidenceRequired = "CaptureAttempt.FlightCompletionEvidenceRequired";
    public const string FlightFailureEvidenceRequired = "CaptureAttempt.FlightFailureEvidenceRequired";
    public const string UploadManifestEvidenceRequired = "CaptureAttempt.UploadManifestEvidenceRequired";
    public const string InputApprovalRequired = "CaptureAttempt.InputApprovalRequired";
    public const string ProcessingReceiptRequired = "CaptureAttempt.ProcessingReceiptRequired";
    public const string EvidenceIdentityMismatch = "CaptureAttempt.EvidenceIdentityMismatch";
}
