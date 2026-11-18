namespace AgriDrone.Modules.Surveys.Domain;

public static class CaptureAttemptAction
{
    public const string CreateInitial = "CreateInitial";
    public const string CreateSuccessor = "CreateSuccessor";
    public const string MarkPreflightReady = "MarkPreflightReady";
    public const string InvalidatePreflight = "InvalidatePreflight";
    public const string StartFlight = "StartFlight";
    public const string RecordFlightCompleted = "RecordFlightCompleted";
    public const string RecordFlightFailed = "RecordFlightFailed";
    public const string MarkUploaded = "MarkUploaded";
    public const string StartProcessing = "StartProcessing";
    public const string AcceptProcessing = "AcceptProcessing";
    public const string RequireReflight = "RequireReflight";
    public const string Cancel = "Cancel";
}
